#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the AppTimeTracker Windows Service.

.DESCRIPTION
    This script installs and configures the AppTimeTracker Windows Service with:
    - Automatic startup configuration
    - Recovery options (restart on failure)
    - ProgramData directory with appropriate permissions

.PARAMETER ServicePath
    Path to the AppTimeTracker.exe executable. Defaults to current directory.

.PARAMETER ServiceAccount
    The account to run the service under. Defaults to LocalSystem.
    Options: LocalSystem, LocalService, NetworkService, or a domain\user account.

.EXAMPLE
    .\Install-Service.ps1

.EXAMPLE
    .\Install-Service.ps1 -ServicePath "C:\Services\AppTimeTracker\AppTimeTracker.exe"

.NOTES
    Requires Administrator privileges to install Windows Services.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ServicePath = (Join-Path $PSScriptRoot "..\src\bin\Release\net8.0\win-x64\publish\AppTimeTracker.exe"),

    [Parameter()]
    [ValidateSet("LocalSystem", "LocalService", "NetworkService")]
    [string]$ServiceAccount = "LocalSystem"
)

# Service configuration
$ServiceName = "AppTimeTracker"
$ServiceDisplayName = "App Time Tracker"
$ServiceDescription = "Monitors application usage time by tracking foreground window focus."
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
Write-Host "  AppTimeTracker Service Installer" -ForegroundColor White
Write-Host "========================================" -ForegroundColor White
Write-Host ""

# Validate .NET 8 Runtime
Write-Step "Checking .NET 8 Runtime..."
try {
    $dotnetInfo = & dotnet --list-runtimes 2>&1
    if ($dotnetInfo -match "Microsoft\.NETCore\.App 8\.") {
        Write-Success ".NET 8 Runtime is installed."
    } else {
        Write-Warning ".NET 8 Runtime not detected. The service may not start correctly."
        Write-Host "    Download from: https://dotnet.microsoft.com/download/dotnet/8.0"
    }
} catch {
    Write-Warning "Could not verify .NET 8 Runtime. Ensure it is installed."
}

# Validate service executable exists
Write-Step "Validating service executable..."
if (-not (Test-Path $ServicePath)) {
    # Try alternative path in current directory
    $altPath = Join-Path $PSScriptRoot "AppTimeTracker.exe"
    if (Test-Path $altPath) {
        $ServicePath = $altPath
    } else {
        Write-Error "Service executable not found at: $ServicePath"
        Write-Host "    Please build the project first:"
        Write-Host "    dotnet publish -c Release -r win-x64 --self-contained false"
        exit 1
    }
}
Write-Success "Found service executable: $ServicePath"

# Check if service already exists
Write-Step "Checking for existing service..."
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Warning "Service '$ServiceName' already exists."
    $response = Read-Host "Do you want to reinstall? (Y/N)"
    if ($response -eq "Y" -or $response -eq "y") {
        Write-Step "Stopping existing service..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2

        Write-Step "Removing existing service..."
        & sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    } else {
        Write-Host "Installation cancelled."
        exit 0
    }
}

# Create ProgramData directory with proper permissions
Write-Step "Creating ProgramData directory..."
if (-not (Test-Path $ProgramDataPath)) {
    New-Item -ItemType Directory -Path $ProgramDataPath -Force | Out-Null
}

# Create subdirectories
$LogsPath = Join-Path $ProgramDataPath "Logs"
if (-not (Test-Path $LogsPath)) {
    New-Item -ItemType Directory -Path $LogsPath -Force | Out-Null
}
Write-Success "Created directory: $ProgramDataPath"

# Set directory permissions
Write-Step "Configuring directory permissions..."
try {
    $acl = Get-Acl $ProgramDataPath

    # Add SYSTEM full control
    $systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "NT AUTHORITY\SYSTEM",
        "FullControl",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.AddAccessRule($systemRule)

    # Add Administrators full control
    $adminRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "BUILTIN\Administrators",
        "FullControl",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.AddAccessRule($adminRule)

    # Add Users read access (for viewing logs)
    $usersRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "BUILTIN\Users",
        "ReadAndExecute",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.AddAccessRule($usersRule)

    Set-Acl -Path $ProgramDataPath -AclObject $acl
    Write-Success "Directory permissions configured."
} catch {
    Write-Warning "Could not set directory permissions: $_"
}

# Create the Windows Service
Write-Step "Creating Windows Service..."
$binPath = "`"$ServicePath`""

# Map service account to sc.exe format
$scAccount = switch ($ServiceAccount) {
    "LocalSystem" { "LocalSystem" }
    "LocalService" { "NT AUTHORITY\LocalService" }
    "NetworkService" { "NT AUTHORITY\NetworkService" }
    default { $ServiceAccount }
}

# Create service using sc.exe
$result = & sc.exe create $ServiceName binPath= $binPath start= auto obj= $scAccount DisplayName= $ServiceDisplayName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service: $result"
    exit 1
}
Write-Success "Service created successfully."

# Set service description
Write-Step "Setting service description..."
& sc.exe description $ServiceName $ServiceDescription | Out-Null

# Configure recovery options
Write-Step "Configuring recovery options..."
# Recovery: First failure - restart after 5 seconds
#           Second failure - restart after 10 seconds
#           Subsequent failures - restart after 60 seconds
# Reset failure count after 86400 seconds (24 hours)
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/60000 | Out-Null
Write-Success "Recovery options configured (restart on failure)."

# Register EventLog source
Write-Step "Registering EventLog source..."
try {
    if (-not [System.Diagnostics.EventLog]::SourceExists($ServiceName)) {
        [System.Diagnostics.EventLog]::CreateEventSource($ServiceName, "Application")
        Write-Success "EventLog source registered."
    } else {
        Write-Success "EventLog source already exists."
    }
} catch {
    Write-Warning "Could not register EventLog source: $_"
    Write-Host "    The service will still work, but EventLog may not show proper source names."
}

# Start the service
Write-Step "Starting service..."
try {
    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 2

    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq "Running") {
        Write-Success "Service started successfully!"
    } else {
        Write-Warning "Service is not running. Status: $($service.Status)"
        Write-Host "    Check the EventLog for errors."
    }
} catch {
    Write-Warning "Could not start service: $_"
    Write-Host "    You can start it manually: Start-Service -Name $ServiceName"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor White
Write-Host "  Installation Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor White
Write-Host ""
Write-Host "Service Name:     $ServiceName"
Write-Host "Display Name:     $ServiceDisplayName"
Write-Host "Executable:       $ServicePath"
Write-Host "Data Directory:   $ProgramDataPath"
Write-Host "Log Directory:    $LogsPath"
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Yellow
Write-Host "  Start service:   Start-Service -Name $ServiceName"
Write-Host "  Stop service:    Stop-Service -Name $ServiceName"
Write-Host "  Service status:  Get-Service -Name $ServiceName"
Write-Host "  View logs:       Get-EventLog -LogName Application -Source $ServiceName -Newest 20"
Write-Host ""
