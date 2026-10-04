using WebControl.Core.Models;
using WebControl.Core.Services;
using WebControl.Proxy.Models;
using WebControl.Proxy.Services;

Console.Title = "WebControl Proxy Host";

const string YouTubeServiceId = "youtube";

var youtube = new ServiceDefinition(
    YouTubeServiceId,
    "YouTube",
    new[]
    {
        new DomainRule("youtube.com"),
        new DomainRule("*.youtube.com"),

        new DomainRule("youtu.be"),
        new DomainRule("*.youtu.be"),

        new DomainRule("googlevideo.com"),
        new DomainRule("*.googlevideo.com"),

        new DomainRule("ytimg.com"),
        new DomainRule("*.ytimg.com"),

        new DomainRule("youtube-nocookie.com"),
        new DomainRule("*.youtube-nocookie.com"),

        new DomainRule("youtubei.googleapis.com")
    });

var ruleEngine = new RuleEngine(
    new[]
    {
        youtube
    });

//
// Para la primera prueba arrancamos bloqueando YouTube.
//
ruleEngine.SetState(
    YouTubeServiceId,
    ServiceAccessState.Blocked);

var proxyOptions = new ProxyOptions
{
    ListenAddress = "127.0.0.1",
    Port = 8877,
    Backlog = 512,
    ConnectTimeout = TimeSpan.FromSeconds(15)
};

var proxyServer = new WebControlProxyServer(
    ruleEngine,
    proxyOptions);

using var applicationCts =
    new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;

    applicationCts.Cancel();
};

PrintHeader();

var serverTask = proxyServer.RunAsync(
    applicationCts.Token);

//
// Dar tiempo suficiente para detectar inmediatamente
// problemas como "puerto ya utilizado".
//
await Task.Delay(250);

if (serverTask.IsFaulted)
{
    await serverTask;
}

Console.WriteLine();
Console.WriteLine(
    "Proxy iniciado correctamente en 127.0.0.1:8877");

PrintStatus();

Console.WriteLine();
PrintCommands();

while (!applicationCts.IsCancellationRequested)
{
    Console.WriteLine();
    Console.Write("WebControl> ");

    var input =
        Console.ReadLine();

    if (input is null)
    {
        applicationCts.Cancel();
        break;
    }

    var command =
        input.Trim();

    if (string.IsNullOrWhiteSpace(command))
    {
        continue;
    }

    if (command.Equals(
            "status",
            StringComparison.OrdinalIgnoreCase))
    {
        PrintStatus();

        continue;
    }

    if (command.Equals(
            "block",
            StringComparison.OrdinalIgnoreCase))
    {
        ruleEngine.SetState(
            YouTubeServiceId,
            ServiceAccessState.Blocked);

        Console.WriteLine(
            "YouTube BLOQUEADO.");

        continue;
    }

    if (command.Equals(
            "allow",
            StringComparison.OrdinalIgnoreCase))
    {
        ruleEngine.SetState(
            YouTubeServiceId,
            ServiceAccessState.Allowed);

        Console.WriteLine(
            "YouTube PERMITIDO.");

        continue;
    }

    if (command.Equals(
            "revoke",
            StringComparison.OrdinalIgnoreCase))
    {
        ruleEngine.RevokeTemporaryAccess(
            YouTubeServiceId);

        Console.WriteLine(
            "Permiso temporal eliminado.");

        continue;
    }

    if (command.StartsWith(
            "grant ",
            StringComparison.OrdinalIgnoreCase))
    {
        var value =
            command["grant ".Length..]
                .Trim();

        if (!int.TryParse(
                value,
                out var minutes) ||
            minutes <= 0)
        {
            Console.WriteLine(
                "Uso: grant MINUTOS");

            continue;
        }

        var grant =
            ruleEngine.GrantTemporaryAccess(
                YouTubeServiceId,
                TimeSpan.FromMinutes(minutes));

        Console.WriteLine(
            $"Permiso temporal concedido hasta {grant.ExpiresAtUtc.ToLocalTime():HH:mm:ss}.");

        continue;
    }

    if (command.Equals(
            "help",
            StringComparison.OrdinalIgnoreCase))
    {
        PrintCommands();

        continue;
    }

    if (command.Equals(
            "quit",
            StringComparison.OrdinalIgnoreCase) ||
        command.Equals(
            "exit",
            StringComparison.OrdinalIgnoreCase))
    {
        applicationCts.Cancel();

        break;
    }

    Console.WriteLine(
        "Comando desconocido. Usa 'help'.");
}

try
{
    await serverTask;
}
catch (OperationCanceledException)
{
}

Console.WriteLine();
Console.WriteLine(
    "WebControl Proxy finalizado.");

return;

void PrintHeader()
{
    Console.WriteLine(
        "============================================================");

    Console.WriteLine(
        "WEBCONTROL V1 - PROXY HOST");

    Console.WriteLine(
        "============================================================");

    Console.WriteLine();

    Console.WriteLine(
        "Este ejecutable es solamente para desarrollo y diagnostico.");

    Console.WriteLine(
        "No modifica politicas de Windows ni navegadores.");
}

void PrintStatus()
{
    var state =
        ruleEngine.GetState(
            YouTubeServiceId);

    var grant =
        ruleEngine.GetTemporaryGrant(
            YouTubeServiceId);

    Console.WriteLine();
    Console.WriteLine(
        "---------------- ESTADO ----------------");

    Console.WriteLine(
        $"YouTube estado base : {state}");

    if (grant is null)
    {
        Console.WriteLine(
            "Permiso temporal   : Ninguno");
    }
    else
    {
        var remaining =
            grant.ExpiresAtUtc -
            DateTimeOffset.UtcNow;

        Console.WriteLine(
            $"Permiso temporal   : Activo");

        Console.WriteLine(
            $"Expira             : {grant.ExpiresAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

        Console.WriteLine(
            $"Restante aprox.    : {remaining.TotalMinutes:F1} minutos");
    }

    Console.WriteLine(
        "----------------------------------------");
}

void PrintCommands()
{
    Console.WriteLine(
        "Comandos disponibles:");

    Console.WriteLine(
        "  status       Ver estado");

    Console.WriteLine(
        "  block        Bloquear YouTube");

    Console.WriteLine(
        "  allow        Permitir YouTube");

    Console.WriteLine(
        "  grant 1      Permitir temporalmente 1 minuto");

    Console.WriteLine(
        "  grant 15     Permitir temporalmente 15 minutos");

    Console.WriteLine(
        "  grant 30     Permitir temporalmente 30 minutos");

    Console.WriteLine(
        "  grant 60     Permitir temporalmente 60 minutos");

    Console.WriteLine(
        "  revoke       Eliminar permiso temporal");

    Console.WriteLine(
        "  help         Mostrar comandos");

    Console.WriteLine(
        "  quit         Cerrar proxy");
}
