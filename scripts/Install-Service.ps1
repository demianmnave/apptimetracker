#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Installs the AppTimeTracker Windows Service with elevated privileges validation
.DESCRIPTION
    Installs AppTimeTracker as a Windows Service with proper dependency checks,
    service registration, and startup validation.
.PARAMETER ServicePath
    Path to the AppTimeTracker.exe executable (defaults to script directory parent\src\worker\bin\Release\net8.0-windows\AppTimeTracker.exe)
.PARAMETER ServiceName
    Name to register the service as (default: AppTimeTracker)
.PARAMETER StartupType
    Service startup type: Automatic, Manual, or Disabled (default: Automatic)
.EXAMPLE
    .\Install-Service.ps1
    .\Install-Service.ps1 -ServicePath "C:\Program Files\AppTimeTracker\AppTimeTracker.exe"
    .\Install-Service.ps1 -StartupType Manual
#>

param(
    [string]$ServicePath = (Join-Path (Split-Path $PSScriptRoot -Parent) "src\worker\bin\Release\net8.0-windows\AppTimeTracker.exe"),
    [string]$ServiceName = "AppTimeTracker",
    [ValidateSet("Automatic", "Manual", "Disabled")]
    [string]$StartupType = "Automatic"
)

# Configuration
$ServiceDisplayName = "AppTimeTracker Session Monitor"
$ServiceDescription = "Monitors application usage and session states for AppTimeTracker"

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-Status {
    param([string]$Message, [string]$Type = "Info")
    
    $colors = @{
        Info    = "Cyan"
        Success = "Green"
        Warning = "Yellow"
        Error   = "Red"
    }
    
    $symbol = @{
        Info    = "ℹ️ "
        Success = "✅"
        Warning = "⚠️ "
        Error   = "❌"
    }
    
    Write-Host "$($symbol[$Type]) $Message" -ForegroundColor $colors[$Type]
}

function Test-Dependencies {
    Write-Host "`n--- Checking Dependencies ---`n"
    
    # Check if .NET 8 is installed
    $dotnetVersion = dotnet --version 2>$null
    if (-not $dotnetVersion) {
        Write-Status ".NET SDK not found. Please install .NET 8 or later." "Error"
        return $false
    }
    
    Write-Status ".NET version: $dotnetVersion" "Success"
    
    # Check if service executable exists
    if (-not (Test-Path $ServicePath)) {
        Write-Status "Service executable not found at: $ServicePath" "Error"
        Write-Status "Please build the project first: cd src/worker && dotnet publish -c Release" "Info"
        return $false
    }
    
    Write-Status "Service executable found: $ServicePath" "Success"
    return $true
}

function Install-WindowsService {
    Write-Host "`n--- Installing Windows Service ---`n"
    
    # Check if service already exists
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    
    if ($existingService) {
        Write-Status "Service '$ServiceName' already exists. Removing existing service..." "Warning"
        
        # Stop the service if running
        if ($existingService.Status -eq "Running") {
            Write-Status "Stopping service..." "Info"
            Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
        }
        
        # Remove the service
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Milliseconds 1000
        Write-Status "Existing service removed" "Success"
    }
    
    # Create the service
    try {
        Write-Status "Creating service: $ServiceName" "Info"
        
        # Resolve full path
        $fullPath = Resolve-Path $ServicePath
        
        # Create service using sc.exe
        $output = sc.exe create $ServiceName `
            binPath= $fullPath `
            DisplayName= $ServiceDisplayName `
            start= $StartupType `
            type= own `
            error= ignore
        
        if ($LASTEXITCODE -ne 0) {
            Write-Status "Failed to create service. Exit code: $LASTEXITCODE" "Error"
            Write-Status "Output: $output" "Error"
            return $false
        }
        
        Write-Status "Service created successfully" "Success"
        
        # Set service description
        try {
            sc.exe description $ServiceName $ServiceDescription | Out-Null
            Write-Status "Service description set" "Success"
        }
        catch {
            Write-Status "Warning: Could not set service description: $_" "Warning"
        }
        
        # Set failure actions (restart on failure)
        try {
            sc.exe failure $ServiceName reset= 60 actions= restart/5000 | Out-Null
            Write-Status "Failure recovery configured (restart on failure)" "Success"
        }
        catch {
            Write-Status "Warning: Could not configure failure actions: $_" "Warning"
        }
        
        return $true
    }
    catch {
        Write-Status "Error creating service: $_" "Error"
        return $false
    }
}

function Verify-ServiceInstallation {
    Write-Host "`n--- Verifying Service Installation ---`n"
    
    # Check if service was created
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    
    if (-not $service) {
        Write-Status "Service verification failed: Service not found in registry" "Error"
        return $false
    }
    
    Write-Status "Service registered: $($service.Name)" "Success"
    Write-Status "Display Name: $($service.DisplayName)" "Info"
    Write-Status "Status: $($service.Status)" "Info"
    Write-Status "Start Type: $($service.StartType)" "Info"
    
    return $true
}

function Start-ServiceValidation {
    Write-Host "`n--- Starting Service Validation ---`n"
    
    try {
        Write-Status "Starting service..." "Info"
        Start-Service -Name $ServiceName -ErrorAction Stop
        
        # Wait for service to start
        $timeout = 0
        $maxWait = 30
        
        while ($timeout -lt $maxWait) {
            $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if ($svc -and $svc.Status -eq "Running") {
                Write-Status "Service started successfully" "Success"
                return $true
            }
            Start-Sleep -Seconds 1
            $timeout++
        }
        
        Write-Status "Service failed to start within timeout period" "Warning"
        
        # Try to get more details
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        Write-Status "Current service status: $($svc.Status)" "Info"
        
        return $false
    }
    catch {
        Write-Status "Error starting service: $_" "Error"
        return $false
    }
}

function Show-Summary {
    param([bool]$Success)
    
    Write-Host "`n" + ("=" * 60)
    
    if ($Success) {
        Write-Status "Service installed successfully!" "Success"
        Write-Host "`nℹ️  Service Information:"
        Write-Host "  • Service Name: $ServiceName"
        Write-Host "  • Display Name: $ServiceDisplayName"
        Write-Host "  • Executable: $ServicePath"
        Write-Host "  • Start Type: $StartupType"
        Write-Host "`nℹ️  Next Steps:"
        Write-Host "  • View service status: Get-Service -Name $ServiceName"
        Write-Host "  • View service logs: Get-EventLog -LogName Application -Source AppTimeTracker -Newest 10"
        Write-Host "  • Manually start: Start-Service -Name $ServiceName"
        Write-Host "  • Manually stop: Stop-Service -Name $ServiceName"
        Write-Host "  • Uninstall: .\Uninstall-Service.ps1"
    }
    else {
        Write-Status "Service installation encountered issues. Please review the errors above." "Error"
        Write-Host "`nℹ️  Troubleshooting:"
        Write-Host "  • Ensure you have Administrator privileges"
        Write-Host "  • Check that the executable path is correct"
        Write-Host "  • Review Windows Event Log for service startup errors"
        Write-Host "  • Run: dotnet build -c Release in src/worker directory"
    }
    
    Write-Host ("=" * 60) "`n"
}

# Main execution
Write-Host "`n╔═══════════════════════════════════════════════════════════╗"
Write-Host "║     AppTimeTracker Windows Service Installer               ║"
Write-Host "╚═══════════════════════════════════════════════════════════╝`n"

# Verify admin privileges
if (-not (Test-Administrator)) {
    Write-Status "This script requires Administrator privileges!" "Error"
    Write-Status "Please run PowerShell as Administrator and try again." "Info"
    exit 1
}

Write-Status "Running with Administrator privileges" "Success"

# Test dependencies
if (-not (Test-Dependencies)) {
    Show-Summary $false
    exit 1
}

# Install service
if (-not (Install-WindowsService)) {
    Show-Summary $false
    exit 1
}

# Verify installation
if (-not (Verify-ServiceInstallation)) {
    Show-Summary $false
    exit 1
}

# Validate startup
Start-ServiceValidation | Out-Null

# Show summary
Show-Summary $true
exit 0
