using Microsoft.Data.Sqlite;
using WebControl.Agent.Windows.Configuration;
using WebControl.Agent.Windows.Persistence;
using WebControl.Agent.Windows.Services;
using WebControl.Core.Models;
using WebControl.Core.Services;
using WebControl.Proxy.Models;
using WebControl.Proxy.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

//
// ============================================================
// SQLITE
// ============================================================
//

builder.Services.Configure<WebControlDbOptions>(
    builder.Configuration.GetSection(
        WebControlDbOptions.SectionName));

builder.Services.AddSingleton<WebControlDatabase>();
builder.Services.AddSingleton<WebControlStateRepository>();
builder.Services.AddSingleton<ServiceControlService>();

//
// El orden es importante:
// 1. Crear/verificar SQLite.
// 2. Recuperar estado persistido.
// 3. Arrancar el proxy.
//
builder.Services.AddHostedService<
    DatabaseInitializationHostedService>();

builder.Services.AddHostedService<
    StateInitializationHostedService>();

//
// ============================================================
// CORE
// ============================================================
//

builder.Services.AddSingleton<DomainMatcher>();

builder.Services.AddSingleton<RuleEngine>(
    serviceProvider =>
    {
        var domainMatcher =
            serviceProvider.GetRequiredService<
                DomainMatcher>();

        var ruleEngine =
            new RuleEngine(
                DefaultServiceDefinitions.Create(),
                domainMatcher);

        //
        // Fail-closed durante el arranque.
        // StateInitializationHostedService reemplaza
        // este valor por el persistido en SQLite.
        //
        ruleEngine.SetState(
            "youtube",
            ServiceAccessState.Blocked);

        return ruleEngine;
    });

//
// ============================================================
// PROXY
// ============================================================
//

builder.Services.AddSingleton(
    new ProxyOptions
    {
        ListenAddress = "127.0.0.1",
        Port = 8877,
        Backlog = 512,
        ConnectTimeout =
            TimeSpan.FromSeconds(15)
    });

builder.Services.AddSingleton<
    ProxyRequestParser>();

builder.Services.AddSingleton<
    HttpHeaderReader>();

builder.Services.AddSingleton<
    WebControlProxyServer>();

builder.Services.AddHostedService<
    WebControlProxyHostedService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

//
// ============================================================
// API - ESTADO YOUTUBE
// ============================================================
//

app.MapGet(
    "/api/services/youtube",
    (RuleEngine ruleEngine) =>
    {
        var state =
            ruleEngine.GetState(
                "youtube");

        var grant =
            ruleEngine.GetTemporaryGrant(
                "youtube");

        return Results.Ok(
            new
            {
                id = "youtube",
                name = "YouTube",

                baseState =
                    state.ToString(),

                blocked =
                    ruleEngine.IsBlocked(
                        "www.youtube.com"),

                temporaryGrant =
                    grant is null
                        ? null
                        : new
                        {
                            grantedAtUtc =
                                grant.GrantedAtUtc,

                            expiresAtUtc =
                                grant.ExpiresAtUtc,

                            expiresAtLocal =
                                grant.ExpiresAtUtc
                                    .ToLocalTime()
                        }
            });
    });

//
// ============================================================
// API - BLOCK
// ============================================================
//

app.MapPost(
    "/api/services/youtube/block",
    async (
        ServiceControlService controlService,
        CancellationToken cancellationToken
    ) =>
    {
        await controlService.SetBlockedAsync(
            "youtube",
            cancellationToken);

        return Results.Ok(
            new
            {
                id = "youtube",
                state = "Blocked"
            });
    });

//
// ============================================================
// API - ALLOW
// ============================================================
//

app.MapPost(
    "/api/services/youtube/allow",
    async (
        ServiceControlService controlService,
        CancellationToken cancellationToken
    ) =>
    {
        await controlService.SetAllowedAsync(
            "youtube",
            cancellationToken);

        return Results.Ok(
            new
            {
                id = "youtube",
                state = "Allowed"
            });
    });

//
// ============================================================
// API - GRANT TEMPORAL
// ============================================================
//

app.MapPost(
    "/api/services/youtube/grant",
    async (
        TemporaryGrantRequest request,
        ServiceControlService controlService,
        CancellationToken cancellationToken
    ) =>
    {
        if (request.Minutes <= 0 ||
            request.Minutes > 1440)
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "Minutes must be between 1 and 1440."
                });
        }

        var grant =
            await controlService
                .GrantTemporaryAccessAsync(
                    "youtube",
                    TimeSpan.FromMinutes(
                        request.Minutes),
                    cancellationToken);

        return Results.Ok(
            new
            {
                id = "youtube",
                state = "Blocked",
                temporaryAccess = true,
                minutes = request.Minutes,

                expiresAtUtc =
                    grant.ExpiresAtUtc,

                expiresAtLocal =
                    grant.ExpiresAtUtc
                        .ToLocalTime()
            });
    });

//
// ============================================================
// DIAGNOSTICO SQLITE
// Temporal durante desarrollo.
// ============================================================
//

app.MapGet(
    "/api/diagnostics/database",
    async (
        WebControlDatabase database,
        CancellationToken cancellationToken
    ) =>
    {
        var objects =
            new List<object>();

        await using var connection =
            new SqliteConnection(
                database.ConnectionString);

        await connection.OpenAsync(
            cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                type,
                name
            FROM sqlite_master
            WHERE
                type IN ('table', 'index')
                AND name NOT LIKE 'sqlite_%'
            ORDER BY
                type,
                name;
            """;

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        while (
            await reader.ReadAsync(
                cancellationToken))
        {
            objects.Add(
                new
                {
                    type =
                        reader.GetString(0),

                    name =
                        reader.GetString(1)
                });
        }

        return Results.Ok(
            new
            {
                databasePath =
                    database.DatabasePath,

                objects
            });
    });

app.Run();

internal sealed record TemporaryGrantRequest(
    int Minutes);
