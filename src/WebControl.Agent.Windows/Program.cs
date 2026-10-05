using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Net;
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
builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(
        options =>
        {
            options.Cookie.Name =
                "WebControl.Auth";

            options.Cookie.HttpOnly =
                true;

            options.Cookie.SameSite =
                SameSiteMode.Strict;

            options.Cookie.SecurePolicy =
                CookieSecurePolicy.SameAsRequest;

            options.ExpireTimeSpan =
                TimeSpan.FromHours(12);

            options.SlidingExpiration =
                true;

            options.LoginPath =
                "/login";

            options.Events =
                new CookieAuthenticationEvents
                {
                    OnValidatePrincipal =
                        async context =>
                        {
                            var username =
                                context.Principal?
                                    .Identity?
                                    .Name;

                            var credentialStamp =
                                context.Principal?
                                    .FindFirst(
                                        AdminSessionService
                                            .CredentialStampClaimType)?
                                    .Value;

                            if (string.IsNullOrWhiteSpace(username))
                            {
                                context.RejectPrincipal();

                                await context.HttpContext.SignOutAsync(
                                    CookieAuthenticationDefaults
                                        .AuthenticationScheme);

                                return;
                            }

                            var sessionService =
                                context.HttpContext
                                    .RequestServices
                                    .GetRequiredService<AdminSessionService>();

                            var valid =
                                await sessionService
                                    .IsCredentialStampValidAsync(
                                        username,
                                        credentialStamp,
                                        context.HttpContext
                                            .RequestAborted);

                            if (!valid)
                            {
                                context.RejectPrincipal();

                                await context.HttpContext.SignOutAsync(
                                    CookieAuthenticationDefaults
                                        .AuthenticationScheme);
                            }
                        },

                    OnRedirectToLogin =
                        context =>
                        {
                            if (context.Request.Path
                                .StartsWithSegments("/api"))
                            {
                                context.Response.StatusCode =
                                    StatusCodes.Status401Unauthorized;

                                return Task.CompletedTask;
                            }

                            context.Response.Redirect(
                                context.RedirectUri);

                            return Task.CompletedTask;
                        },

                    OnRedirectToAccessDenied =
                        context =>
                        {
                            if (context.Request.Path
                                .StartsWithSegments("/api"))
                            {
                                context.Response.StatusCode =
                                    StatusCodes.Status403Forbidden;

                                return Task.CompletedTask;
                            }

                            context.Response.Redirect(
                                context.RedirectUri);

                            return Task.CompletedTask;
                        }
                };
        });

builder.Services.AddAuthorization(
    options =>
    {
        options.FallbackPolicy =
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
    });


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
builder.Services.AddSingleton<WebControlAuditRepository>();
builder.Services.AddSingleton<DeviceIdentityRepository>();
builder.Services.AddSingleton<AdminUserRepository>();
builder.Services.AddSingleton<RecoveryCredentialRepository>();
builder.Services.AddSingleton<AdminCredentialService>();
builder.Services.AddSingleton<RecoveryCredentialService>();
builder.Services.AddSingleton<PasswordRecoveryService>();
builder.Services.AddSingleton<AdminSessionService>();
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
    DeviceIdentityInitializationHostedService>();

builder.Services.AddHostedService<
    StateInitializationHostedService>();

builder.Services.AddHostedService<
    TemporaryGrantExpirationHostedService>();

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

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(
        options =>
        {
            options.ListenLocalhost(
                7241,
                listenOptions =>
                    listenOptions.UseHttps());

            options.ListenAnyIP(
                8765);
        });
}
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets()
    .AllowAnonymous();

app.MapRazorPages()
    .WithStaticAssets();

//
// ============================================================
// API - ESTADO YOUTUBE
// ============================================================
//

//
// ============================================================
// API - AUTENTICACION
// ============================================================
//

app.MapPost(
    "/api/auth/login",
    async (
        LoginRequest request,
        HttpContext httpContext,
        AdminCredentialService credentialService,
        AdminSessionService sessionService,
        CancellationToken cancellationToken
    ) =>
    {
        var valid =
            await credentialService.ValidateAsync(
                request.Username,
                request.Password,
                cancellationToken);

        if (!valid)
        {
            return Results.Unauthorized();
        }

        var credentialStamp =
            await sessionService.GetCredentialStampAsync(
                request.Username,
                cancellationToken);

        if (credentialStamp is null)
        {
            return Results.Unauthorized();
        }

        var claims =
            new[]
            {
                new Claim(
                    ClaimTypes.Name,
                    request.Username.Trim()),

                new Claim(
                    ClaimTypes.Role,
                    "Administrator"),

                new Claim(
                    AdminSessionService.CredentialStampClaimType,
                    credentialStamp)
            };

        var identity =
            new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

        var principal =
            new ClaimsPrincipal(
                identity);

        var properties =
            new AuthenticationProperties
            {
                IsPersistent =
                    request.RememberMe
            };

        if (request.RememberMe)
        {
            properties.ExpiresUtc =
                DateTimeOffset.UtcNow
                    .AddDays(30);
        }

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);

        return Results.Ok(
            new
            {
                authenticated = true,
                username =
                    request.Username.Trim(),
                rememberMe =
                    request.RememberMe
            });
    })
    .AllowAnonymous();
app.MapPost(
    "/api/auth/recovery/reset",
    async (
        RecoveryResetRequest request,
        PasswordRecoveryService recoveryService,
        CancellationToken cancellationToken
    ) =>
    {
        try
        {
            var result =
                await recoveryService.ResetAsync(
                    request.Username,
                    request.RecoveryCode,
                    request.NewPassword,
                    cancellationToken);

            if (!result.Success)
            {
                return Results.BadRequest(
                    new
                    {
                        error =
                            "The recovery information is invalid."
                    });
            }

            return Results.Ok(
                new
                {
                    passwordChanged = true,

                    newRecoveryCode =
                        result.NewRecoveryCode
                });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(
                new
                {
                    error =
                        exception.Message
                });
        }
    })
    .AllowAnonymous();
app.MapPost(
    "/api/auth/recovery/generate",
    async (
        HttpContext httpContext,
        AdminUserRepository adminRepository,
        RecoveryCredentialService recoveryService,
        CancellationToken cancellationToken
    ) =>
    {
        var username =
            httpContext.User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
        {
            return Results.Unauthorized();
        }

        var user =
            await adminRepository.GetByUsernameAsync(
                username,
                cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var recoveryCode =
            await recoveryService.GenerateAsync(
                user.Id,
                cancellationToken);

        return Results.Ok(
            new
            {
                username =
                    user.Username,

                recoveryCode,

                message =
                    "Save this recovery code. WebControl will not store the plaintext code."
            });
    })
    .RequireAuthorization();
app.MapPost(
    "/api/auth/logout",
    async (
        HttpContext httpContext
    ) =>
    {
        await httpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.Ok(
            new
            {
                authenticated = false
            });
    });

app.MapGet(
    "/api/auth/me",
    (
        HttpContext httpContext
    ) =>
    {
        if (httpContext.User.Identity?.IsAuthenticated
            != true)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            new
            {
                authenticated = true,
                username =
                    httpContext.User.Identity.Name
            });
    })
    .RequireAuthorization();
//
// ============================================================
// API - CONFIGURACION INICIAL ADMIN
// Solo localhost y solo mientras no exista administrador.
// ============================================================
//

app.MapPost(
    "/api/setup/admin",
    async (
        InitialAdminRequest request,
        HttpContext httpContext,
        AdminUserRepository adminRepository,
        AdminCredentialService credentialService,
        AdminSessionService sessionService,
        CancellationToken cancellationToken
    ) =>
    {
        var remoteIp =
            httpContext.Connection.RemoteIpAddress;

        if (remoteIp is null ||
            !IPAddress.IsLoopback(remoteIp))
        {
            return Results.StatusCode(
                StatusCodes.Status403Forbidden);
        }

        if (await adminRepository.HasAnyAsync(
                cancellationToken))
        {
            return Results.Conflict(
                new
                {
                    error =
                        "WebControl administrator is already configured."
                });
        }

        if (string.IsNullOrWhiteSpace(
                request.Username))
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "Username is required."
                });
        }

        try
        {
            var userId =
                await credentialService.CreateAsync(
                    request.Username,
                    request.Password,
                    cancellationToken);

            return Results.Ok(
                new
                {
                    id = userId,
                    username =
                        request.Username.Trim(),
                    configured = true
                });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(
                new
                {
                    error =
                        exception.Message
                });
        }
    })
    .AllowAnonymous();
// ============================================================
// API - DISPOSITIVO
// ============================================================
//

app.MapGet(
    "/api/device",
    async (
        DeviceIdentityRepository deviceRepository,
        CancellationToken cancellationToken
    ) =>
    {
        var device =
            await deviceRepository.GetOrCreateAsync(
                cancellationToken);

        var agentVersion =
            typeof(DeviceIdentityRecord)
                .Assembly
                .GetName()
                .Version?
                .ToString()
            ?? "unknown";

        return Results.Ok(
            new
            {
                deviceId =
                    device.DeviceId,

                machineName =
                    device.MachineName,

                displayName =
                    device.DisplayName,

                platform =
                    device.Platform,

                installedAtUtc =
                    device.InstalledAtUtc,

                agentVersion
            });
    });
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
// API - HISTORIAL
// ============================================================
//

app.MapGet(
    "/api/history",
    async (
        int? limit,
        WebControlAuditRepository auditRepository,
        CancellationToken cancellationToken
    ) =>
    {
        var requestedLimit =
            Math.Clamp(
                limit ?? 50,
                1,
                200);

        var events =
            await auditRepository.GetRecentAsync(
                requestedLimit,
                cancellationToken);

        return Results.Ok(events);
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






internal sealed record InitialAdminRequest(
    string Username,
    string Password);


internal sealed record LoginRequest(
    string Username,
    string Password,
    bool RememberMe);







internal sealed record RecoveryResetRequest(
    string Username,
    string RecoveryCode,
    string NewPassword);





