#Requires -Version 5.1
<#
.SYNOPSIS
    First-time setup for StudentLog.
    Installs the Windows App Runtime if not already present, then launches the app.
#>

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "StudentLog Setup"
Write-Host "================"
Write-Host ""

# ── Windows App Runtime ──────────────────────────────────────────────────────

$runtimePresent = Get-AppxPackage -Name "Microsoft.WindowsAppRuntime.*" `
    -ErrorAction SilentlyContinue | Select-Object -First 1

if ($runtimePresent) {
    Write-Host "[OK] Windows App Runtime is already installed ($($runtimePresent.Version))."
} else {
    Write-Host "Windows App Runtime not found. Downloading installer..."

    $installerUrl  = "https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe"
    $installerPath = "$env:TEMP\WinAppRuntimeInstall.exe"

    try {
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        Write-Host "Installing Windows App Runtime (this may take a moment)..."
        $proc = Start-Process -FilePath $installerPath -ArgumentList "--quiet" -Wait -PassThru
        Remove-Item $installerPath -Force -ErrorAction SilentlyContinue

        if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010) {
            Write-Host "[OK] Windows App Runtime installed successfully."
            if ($proc.ExitCode -eq 3010) {
                Write-Host "     A reboot may be required before running the app."
            }
        } else {
            Write-Warning "Installer exited with code $($proc.ExitCode). The app may not run correctly."
        }
    } catch {
        Write-Warning "Could not download the runtime installer. Check your internet connection."
        Write-Warning "Manual download: https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads"
    }
}

# ── Launch ───────────────────────────────────────────────────────────────────

$appExe = Join-Path $PSScriptRoot "StudentLog\StudentLog.exe"

if (-not (Test-Path $appExe)) {
    Write-Warning "StudentLog.exe not found at: $appExe"
    Write-Warning "Make sure setup.ps1 is in the same folder as the StudentLog directory."
    exit 1
}

Write-Host ""
Write-Host "Setup complete. Launching StudentLog..."
Write-Host ""
Start-Process -FilePath $appExe
