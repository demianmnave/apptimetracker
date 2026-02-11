# AppTimeTracker Windows Service Installation Script
# Usage: .\install-service.ps1
# Requires: Administrator privileges

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Install", "Uninstall", "Reinstall")]
    [string]$Action = "Install"
)

# Service information
$ServiceName = "AppTimeTracker"
$DisplayName = "App Time Tracker Service"
$Description = "Monitors application usage time for the signed-in user"
$BinaryPath = "$PSScriptRoot\src\bin\Release\net8.0\AppTimeTracker.exe"

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Host "ERROR: This script requires Administrator privileges. Please run as Administrator." -ForegroundColor Red
    exit 1
}

function Install-Service {
    Write-Host "Installing AppTimeTracker Windows Service..." -ForegroundColor Green

    # Check if binary exists
    if (-not (Test-Path $BinaryPath)) {
        Write-Host "ERROR: Binary not found at: $BinaryPath" -ForegroundColor Red
        Write-Host "Please build the project first: dotnet build -c Release" -ForegroundColor Yellow
        exit 1
    }

    # Check if service already exists
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Host "WARNING: Service '$ServiceName' already exists. Please uninstall first or use Reinstall action." -ForegroundColor Yellow
        exit 1
    }

    try {
        # Create the Windows Service
        Write-Host "Creating service: $DisplayName"
        sc.exe create $ServiceName binPath= "$BinaryPath" DisplayName= "$DisplayName" start= auto

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Service created successfully." -ForegroundColor Green
        }
        else {
            Write-Host "ERROR: Failed to create service. Exit code: $LASTEXITCODE" -ForegroundColor Red
            exit 1
        }

        # Set service description
        Write-Host "Setting service description..."
        sc.exe description $ServiceName "$Description"

        # Configure automatic recovery (restart on failure)
        # Format: sc failure <ServiceName> reset=<seconds> actions=<action1>/<delay1>/<action2>/<delay2>/...
        # Actions: restart (1) or run (2)
        # Delays in milliseconds (60000 = 60 seconds)
        Write-Host "Configuring automatic recovery..."
        sc.exe failure $ServiceName reset=86400 actions=restart/60000/restart/60000/restart/60000

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Recovery options configured successfully." -ForegroundColor Green
        }
        else {
            Write-Host "WARNING: Failed to configure recovery options. Exit code: $LASTEXITCODE" -ForegroundColor Yellow
        }

        Write-Host "Installation completed successfully!" -ForegroundColor Green
        Write-Host "`nNext steps:" -ForegroundColor Cyan
        Write-Host "1. Start the service: Start-Service -Name '$ServiceName'" -ForegroundColor Cyan
        Write-Host "2. Check service status: Get-Service -Name '$ServiceName'" -ForegroundColor Cyan
        Write-Host "3. View logs: Get-EventLog -LogName Application -Source AppTimeTracker" -ForegroundColor Cyan
    }
    catch {
        Write-Host "ERROR: Failed to install service: $_" -ForegroundColor Red
        exit 1
    }
}

function Uninstall-Service {
    Write-Host "Uninstalling AppTimeTracker Windows Service..." -ForegroundColor Green

    # Check if service exists
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $existingService) {
        Write-Host "WARNING: Service '$ServiceName' not found." -ForegroundColor Yellow
        exit 0
    }

    # Stop the service if running
    if ($existingService.Status -eq "Running") {
        Write-Host "Stopping service..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    try {
        Write-Host "Removing service..."
        sc.exe delete $ServiceName

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Service uninstalled successfully." -ForegroundColor Green
        }
        else {
            Write-Host "ERROR: Failed to uninstall service. Exit code: $LASTEXITCODE" -ForegroundColor Red
            exit 1
        }
    }
    catch {
        Write-Host "ERROR: Failed to uninstall service: $_" -ForegroundColor Red
        exit 1
    }
}

function Reinstall-Service {
    Write-Host "Reinstalling AppTimeTracker Windows Service..." -ForegroundColor Green

    Uninstall-Service
    Start-Sleep -Seconds 1
    Install-Service
}

# Execute requested action
switch ($Action) {
    "Install" { Install-Service }
    "Uninstall" { Uninstall-Service }
    "Reinstall" { Reinstall-Service }
    default { Write-Host "Unknown action: $Action" -ForegroundColor Red; exit 1 }
}

exit 0
