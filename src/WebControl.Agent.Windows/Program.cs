using WebControl.Agent.Windows.Configuration;
using WebControl.Agent.Windows.Services;
using WebControl.Core.Models;
using WebControl.Core.Services;
using WebControl.Proxy.Models;
using WebControl.Proxy.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddSingleton<DomainMatcher>();

builder.Services.AddSingleton<RuleEngine>(serviceProvider =>
{
    var domainMatcher =
        serviceProvider.GetRequiredService<DomainMatcher>();

    var ruleEngine =
        new RuleEngine(
            DefaultServiceDefinitions.Create(),
            domainMatcher);

    //
    // Estado inicial temporal.
    // Más adelante vendrá desde SQLite.
    //
    ruleEngine.SetState(
        "youtube",
        ServiceAccessState.Blocked);

    return ruleEngine;
});

builder.Services.AddSingleton(
    new ProxyOptions
    {
        ListenAddress = "127.0.0.1",
        Port = 8877,
        Backlog = 512,
        ConnectTimeout = TimeSpan.FromSeconds(15)
    });

builder.Services.AddSingleton<ProxyRequestParser>();
builder.Services.AddSingleton<HttpHeaderReader>();
builder.Services.AddSingleton<WebControlProxyServer>();

builder.Services.AddHostedService<WebControlProxyHostedService>();

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
// API WEBCONTROL
// ============================================================
//

app.MapGet(
    "/api/services/youtube",
    (RuleEngine ruleEngine) =>
    {
        var state =
            ruleEngine.GetState("youtube");

        var grant =
            ruleEngine.GetTemporaryGrant("youtube");

        return Results.Ok(
            new
            {
                id = "youtube",
                name = "YouTube",
                baseState = state.ToString(),
                blocked = ruleEngine.IsBlocked(
                    "www.youtube.com"),
                temporaryGrant = grant is null
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

app.MapPost(
    "/api/services/youtube/block",
    (RuleEngine ruleEngine) =>
    {
        ruleEngine.SetState(
            "youtube",
            ServiceAccessState.Blocked);

        ruleEngine.RevokeTemporaryAccess(
            "youtube");

        return Results.Ok(
            new
            {
                id = "youtube",
                state = "Blocked"
            });
    });

app.MapPost(
    "/api/services/youtube/allow",
    (RuleEngine ruleEngine) =>
    {
        ruleEngine.SetState(
            "youtube",
            ServiceAccessState.Allowed);

        return Results.Ok(
            new
            {
                id = "youtube",
                state = "Allowed"
            });
    });

app.MapPost(
    "/api/services/youtube/grant",
    (
        TemporaryGrantRequest request,
        RuleEngine ruleEngine
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

        ruleEngine.SetState(
            "youtube",
            ServiceAccessState.Blocked);

        var grant =
            ruleEngine.GrantTemporaryAccess(
                "youtube",
                TimeSpan.FromMinutes(
                    request.Minutes));

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

app.Run();

internal sealed record TemporaryGrantRequest(
    int Minutes);
