clear

$ErrorActionPreference = "Stop"

$ServiceName = "WCNetworkAgent"
$DisplayName = "WebControl Network Agent"
$InstallRoot = "C:\Program Files\WebControl\Agent"
$SourceRoot = Join-Path $PSScriptRoot "..\..\.dev\publish\WCNetworkAgent"

$ExeName = "WebControl.Agent.Windows.exe"
$InstalledExe = Join-Path $InstallRoot $ExeName

$FirewallRuleName = "WebControl Administration LAN"

# ============================================================
# VALIDAR ADMINISTRADOR
# ============================================================

$identity =
    [Security.Principal.WindowsIdentity]::GetCurrent()

$principal =
    New-Object Security.Principal.WindowsPrincipal($identity)

$isAdministrator =
    $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdministrator) {
    throw "La instalacion de WebControl debe ejecutarse como Administrador."
}

# ============================================================
# VALIDAR PUBLICACION
# ============================================================

$SourceRoot =
    [System.IO.Path]::GetFullPath($SourceRoot)

$SourceExe =
    Join-Path $SourceRoot $ExeName

if (-not (Test-Path $SourceExe)) {
    throw "No existe la publicacion esperada: $SourceExe"
}

# ============================================================
# DETENER SERVICIO EXISTENTE
# ============================================================

$existingService =
    Get-Service `
        -Name $ServiceName `
        -ErrorAction SilentlyContinue

if ($null -ne $existingService) {

    if ($existingService.Status -ne "Stopped") {
        Stop-Service `
            -Name $ServiceName `
            -Force

        $existingService.WaitForStatus(
            "Stopped",
            [TimeSpan]::FromSeconds(15))
    }
}

# ============================================================
# INSTALAR ARCHIVOS
# ============================================================

New-Item `
    -ItemType Directory `
    -Path $InstallRoot `
    -Force |
    Out-Null

Get-ChildItem $InstallRoot `
    -Force `
    -ErrorAction SilentlyContinue |
    Remove-Item `
        -Recurse `
        -Force

Copy-Item `
    -Path (Join-Path $SourceRoot "*") `
    -Destination $InstallRoot `
    -Recurse `
    -Force

if (-not (Test-Path $InstalledExe)) {
    throw "No se pudo instalar el ejecutable de WebControl."
}

# ============================================================
# CREAR O ACTUALIZAR SERVICIO
# ============================================================

if ($null -eq $existingService) {

    $result =
        sc.exe create $ServiceName `
            binPath= "`"$InstalledExe`"" `
            start= auto `
            DisplayName= "$DisplayName"

    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo crear el servicio $ServiceName."
    }
}
else {

    $result =
        sc.exe config $ServiceName `
            binPath= "`"$InstalledExe`"" `
            start= auto `
            DisplayName= "$DisplayName"

    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo actualizar el servicio $ServiceName."
    }
}

sc.exe description $ServiceName `
    "WebControl local network protection and administration agent." |
    Out-Null

sc.exe failure $ServiceName `
    reset= 86400 `
    actions= restart/5000/restart/5000/restart/5000 |
    Out-Null

# ============================================================
# FIREWALL
# ============================================================

Get-NetFirewallRule `
    -DisplayName $FirewallRuleName `
    -ErrorAction SilentlyContinue |
    Remove-NetFirewallRule

New-NetFirewallRule `
    -DisplayName $FirewallRuleName `
    -Description "WebControl administration from the local private network." `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort 8765 `
    -Profile Private `
    -RemoteAddress LocalSubnet |
    Out-Null

# ============================================================
# INICIAR SERVICIO
# ============================================================

Start-Service `
    -Name $ServiceName

$service =
    Get-Service `
        -Name $ServiceName

$service.WaitForStatus(
    "Running",
    [TimeSpan]::FromSeconds(15))

# ============================================================
# RESULTADO
# ============================================================

Write-Host ""
Write-Host "WebControl instalado correctamente."
Write-Host ""

Get-CimInstance Win32_Service `
    -Filter "Name='$ServiceName'" |
    Select-Object `
        Name,
        DisplayName,
        State,
        StartMode,
        StartName,
        PathName

Write-Host ""
Write-Host "Administracion LAN:"
Write-Host "http://localhost:8765"

