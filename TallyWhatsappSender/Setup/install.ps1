#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Install / uninstall the Tally WhatsApp Integration (DLL + Bridge Service)
.DESCRIPTION
    This script:
      1. Optionally installs the whatsapp-bridge as a Windows service
      2. Copies HTTP-based TDL files to a discoverable location
      3. Creates a desktop shortcut for the bridge executable
      4. Sets up firewall rules for the bridge API port

.PARAMETER Action
    install | uninstall | service-install | service-uninstall | status
    Default: install

.EXAMPLE
    .\install.ps1 -Action install
    .\install.ps1 -Action service-install
    .\install.ps1 -Action uninstall
#>

param(
    [ValidateSet("install","uninstall","service-install","service-uninstall","status")]
    [string]$Action = "install"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Configuration ────────────────────────────────────────────────────────────
$ScriptDir       = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot     = Split-Path -Parent $ScriptDir
$BridgeDir       = Join-Path $ProjectRoot "..\whatsapp-mcp\whatsapp-bridge"
$BridgeExe       = Join-Path $BridgeDir "whatsapp-bridge.exe"
$TdlDir          = Join-Path $ProjectRoot "TDL"
$InstallDir      = "C:\TallyWhatsApp"
$ServiceName     = "TallyWhatsAppBridge"
$ServiceDisplay  = "Tally WhatsApp Bridge Service"
$BridgePort      = 8080

# ─── Helpers ─────────────────────────────────────────────────────────────────
function Write-Step([string]$msg) {
    Write-Host "`n► $msg" -ForegroundColor Cyan
}
function Write-OK([string]$msg) {
    Write-Host "  ✓ $msg" -ForegroundColor Green
}
function Write-Warn([string]$msg) {
    Write-Host "  ⚠ $msg" -ForegroundColor Yellow
}
function Write-Fail([string]$msg) {
    Write-Host "  ✗ $msg" -ForegroundColor Red
}

# ─── Action: status ──────────────────────────────────────────────────────────
function Show-Status {
    Write-Step "Current Status"

    # Service?
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc) {
        Write-OK "Service '$ServiceName' is $($svc.Status)"
    } else {
        Write-Warn "Windows service '$ServiceName' is not installed"
    }

    # Bridge reachable?
    try {
        $resp = Invoke-RestMethod -Uri "http://localhost:$BridgePort/api/health" -TimeoutSec 3
        if ($resp.authenticated) {
            Write-OK "Bridge is running and AUTHENTICATED"
        } else {
            Write-Warn "Bridge is running but NOT authenticated (scan QR code)"
        }
    } catch {
        Write-Warn "Bridge is not reachable on port $BridgePort"
    }
}

# ─── Action: install ─────────────────────────────────────────────────────────
function Install-All {
    Write-Step "Installing Tally WhatsApp Integration"

    # 1. Copy files to install dir
    Write-Step "Copying files to $InstallDir"
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Copy-Item -Path "$TdlDir\*" -Destination "$InstallDir\TDL" -Recurse -Force
    Write-OK "TDL scripts copied"

    if (Test-Path $BridgeExe) {
        Copy-Item -Path $BridgeExe -Destination $InstallDir -Force
        Write-OK "Bridge executable copied"
    } else {
        Write-Warn "Bridge executable not found at $BridgeExe – copy manually after build"
    }

    # 4. Firewall rule for bridge port
    Write-Step "Adding firewall rule for port $BridgePort"
    $existingRule = Get-NetFirewallRule -DisplayName "TallyWhatsApp Bridge" -ErrorAction SilentlyContinue
    if (-not $existingRule) {
        New-NetFirewallRule -DisplayName "TallyWhatsApp Bridge" `
            -Direction Inbound -Protocol TCP -LocalPort $BridgePort -Action Allow | Out-Null
        Write-OK "Firewall rule added (TCP $BridgePort inbound)"
    } else {
        Write-OK "Firewall rule already exists"
    }

    # 5. Desktop shortcut for bridge
    $desktopBridge = Join-Path $InstallDir "whatsapp-bridge.exe"
    if (Test-Path $desktopBridge) {
        $desktop = [System.Environment]::GetFolderPath("CommonDesktopDirectory")
        $wsh = New-Object -ComObject WScript.Shell
        $shortcut = $wsh.CreateShortcut("$desktop\WhatsApp Bridge.lnk")
        $shortcut.TargetPath   = $desktopBridge
        $shortcut.WorkingDirectory = $InstallDir
        $shortcut.Description  = "Start Tally WhatsApp Bridge Service"
        $shortcut.Save()
        Write-OK "Desktop shortcut created"
    }

    Write-Host "`n╔══════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║  Installation Complete!                   ║" -ForegroundColor Green
    Write-Host "╠══════════════════════════════════════════╣" -ForegroundColor Green
    Write-Host "║  Next steps:                              ║" -ForegroundColor Green
    Write-Host "║  1. Start WhatsApp Bridge (desktop icon)  ║" -ForegroundColor Green
    Write-Host "║  2. Scan QR code with your phone          ║" -ForegroundColor Green
    Write-Host "║  3. Load TDL files in Tally (F12)         ║" -ForegroundColor Green
    Write-Host "║     $InstallDir\TDL\            ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════╝" -ForegroundColor Green
}

# ─── Action: uninstall ────────────────────────────────────────────────────────
function Uninstall-All {
    Write-Step "Uninstalling Tally WhatsApp Integration"

    # Stop service if running
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
        Write-OK "Service stopped"
    }

    # Remove firewall rule
    Remove-NetFirewallRule -DisplayName "TallyWhatsApp Bridge" -ErrorAction SilentlyContinue
    Write-OK "Firewall rule removed"

    # Remove desktop shortcut
    $desktop = [System.Environment]::GetFolderPath("CommonDesktopDirectory")
    $lnk = "$desktop\WhatsApp Bridge.lnk"
    if (Test-Path $lnk) { Remove-Item $lnk -Force; Write-OK "Shortcut removed" }

    Write-OK "Uninstall complete. TDL files in $InstallDir were NOT deleted."
}

# ─── Action: service-install ─────────────────────────────────────────────────
function Install-Service {
    Write-Step "Installing Windows Service: $ServiceName"

    if (-not (Test-Path $BridgeExe)) {
        Write-Fail "Bridge executable not found: $BridgeExe"
        exit 1
    }

    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Warn "Service already exists. Reinstalling..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    }

    New-Service -Name $ServiceName `
        -DisplayName $ServiceDisplay `
        -Description "Persistent WhatsApp Web connection for Tally integration" `
        -BinaryPathName "`"$BridgeExe`"" `
        -StartupType Automatic | Out-Null

    Start-Service -Name $ServiceName
    Write-OK "Service installed and started"
    Write-Warn "Remember to scan the QR code on first run!"
}

# ─── Action: service-uninstall ──────────────────────────────────────────────
function Uninstall-Service {
    Write-Step "Removing Windows Service: $ServiceName"
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc) {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Write-OK "Service removed"
    } else {
        Write-Warn "Service not found"
    }
}

# ─── Dispatch ────────────────────────────────────────────────────────────────
switch ($Action) {
    "install"           { Install-All }
    "uninstall"         { Uninstall-All }
    "service-install"   { Install-Service }
    "service-uninstall" { Uninstall-Service }
    "status"            { Show-Status }
}
