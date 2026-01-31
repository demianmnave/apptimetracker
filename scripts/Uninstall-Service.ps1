#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls the AppTimeTracker Windows Service.

.DESCRIPTION
    This script stops and removes the AppTimeTracker Windows Service.
    Optionally removes the ProgramData directory containing logs and database.

.PARAMETER RemoveData
    If specified, removes the ProgramData\AppTimeTracker directory
    containing the database and log files.

.PARAMETER Force
    If specified, skips confirmation prompts.

.EXAMPLE
    .\Uninstall-Service.ps1

.EXAMPLE
    .\Uninstall-Service.ps1 -RemoveData

.EXAMPLE
    .\Uninstall-Service.ps1 -RemoveData -Force

.NOTES
    Requires Administrator privileges to uninstall Windows Services.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$RemoveData,

    [Parameter()]
    [switch]$Force
)

# Service configuration
$ServiceName = "AppTimeTracker"
$ProgramDataPath = Join-Path $env:ProgramData "AppTimeTracker"

function Write-Step {
    param([string]$Message)
    Write-Host "[*] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[+] $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "[-] $Message" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[!] $Message" -ForegroundColor Yellow
}

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator."
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'."
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor White
Write-Host "  AppTimeTracker Service Uninstaller" -ForegroundColor White
Write-Host "========================================" -ForegroundColor White
Write-Host ""

# Check if service exists
Write-Step "Checking for service..."
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    Write-Warning "Service '$ServiceName' is not installed."

    if ($RemoveData -and (Test-Path $ProgramDataPath)) {
        Write-Step "Removing data directory..."
        if (-not $Force) {
            $response = Read-Host "Remove data directory at $ProgramDataPath? (Y/N)"
            if ($response -ne "Y" -and $response -ne "y") {
                Write-Host "Data directory preserved."
                exit 0
            }
        }
        Remove-Item -Path $ProgramDataPath -Recurse -Force
        Write-Success "Data directory removed."
    }

    exit 0
}

# Confirm uninstallation
if (-not $Force) {
    Write-Host "This will uninstall the AppTimeTracker service." -ForegroundColor Yellow
    if ($RemoveData) {
        Write-Host "The data directory will also be removed: $ProgramDataPath" -ForegroundColor Yellow
    }
    Write-Host ""
    $response = Read-Host "Continue? (Y/N)"
    if ($response -ne "Y" -and $response -ne "y") {
        Write-Host "Uninstallation cancelled."
        exit 0
    }
}

# Stop the service
Write-Step "Stopping service..."
if ($existingService.Status -eq "Running") {
    try {
        Stop-Service -Name $ServiceName -Force -ErrorAction Stop
        Start-Sleep -Seconds 3
        Write-Success "Service stopped."
    } catch {
        Write-Warning "Could not stop service gracefully: $_"
        Write-Step "Attempting to kill service process..."

        # Try to find and kill the process
        $process = Get-Process -Name "AppTimeTracker" -ErrorAction SilentlyContinue
        if ($process) {
            Stop-Process -Id $process.Id -Force
            Start-Sleep -Seconds 2
        }
    }
} else {
    Write-Success "Service is not running."
}

# Delete the service
Write-Step "Removing service registration..."
$result = & sc.exe delete $ServiceName 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Success "Service removed successfully."
} else {
    # Service might be marked for deletion, wait and retry
    Write-Warning "Service marked for deletion. Waiting..."
    Start-Sleep -Seconds 5

    # Verify removal
    $checkService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $checkService) {
        Write-Success "Service removed successfully."
    } else {
        Write-Error "Failed to remove service. A system restart may be required."
    }
}

# Remove EventLog source
Write-Step "Removing EventLog source..."
try {
    if ([System.Diagnostics.EventLog]::SourceExists($ServiceName)) {
        [System.Diagnostics.EventLog]::DeleteEventSource($ServiceName)
        Write-Success "EventLog source removed."
    } else {
        Write-Success "EventLog source not found (already removed)."
    }
} catch {
    Write-Warning "Could not remove EventLog source: $_"
}

# Remove data directory if requested
if ($RemoveData) {
    Write-Step "Removing data directory..."
    if (Test-Path $ProgramDataPath) {
        try {
            Remove-Item -Path $ProgramDataPath -Recurse -Force
            Write-Success "Data directory removed: $ProgramDataPath"
        } catch {
            Write-Error "Could not remove data directory: $_"
            Write-Host "    You may need to remove it manually."
        }
    } else {
        Write-Success "Data directory not found (already removed)."
    }
} else {
    if (Test-Path $ProgramDataPath) {
        Write-Host ""
        Write-Host "Data directory preserved: $ProgramDataPath" -ForegroundColor Yellow
        Write-Host "To remove it, run: Remove-Item -Path '$ProgramDataPath' -Recurse -Force"
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor White
Write-Host "  Uninstallation Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor White
Write-Host ""
