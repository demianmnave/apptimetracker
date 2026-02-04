#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Uninstalls the AppTimeTracker Windows Service
.DESCRIPTION
    Safely uninstalls the AppTimeTracker Windows Service, stops it if running,
    and cleans up service registration.
.PARAMETER ServiceName
    Name of the service to uninstall (default: AppTimeTracker)
.PARAMETER Force
    Force uninstall without confirmation (default: $false)
.EXAMPLE
    .\Uninstall-Service.ps1
    .\Uninstall-Service.ps1 -Force
    .\Uninstall-Service.ps1 -ServiceName "AppTimeTracker" -Force
#>

param(
    [string]$ServiceName = "AppTimeTracker",
    [switch]$Force
)

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

function Confirm-Uninstall {
    Write-Host "`n⚠️  WARNING: You are about to uninstall the $ServiceName service.`n"
    
    $response = Read-Host "Are you sure you want to continue? (yes/no)"
    
    return ($response -eq "yes")
}

function Stop-ServiceIfRunning {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    
    if (-not $service) {
        Write-Status "Service not found" "Warning"
        return $true
    }
    
    if ($service.Status -eq "Running") {
        Write-Status "Service is running. Stopping service..." "Info"
        
        try {
            Stop-Service -Name $ServiceName -Force -ErrorAction Stop
            
            # Wait for service to stop
            $timeout = 0
            $maxWait = 30
            
            while ($timeout -lt $maxWait) {
                $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
                if ($svc.Status -eq "Stopped") {
                    Write-Status "Service stopped successfully" "Success"
                    return $true
                }
                Start-Sleep -Seconds 1
                $timeout++
            }
            
            Write-Status "Service did not stop within timeout. Continuing uninstall..." "Warning"
            return $true
        }
        catch {
            Write-Status "Error stopping service: $_" "Warning"
            return $true
        }
    }
    
    Write-Status "Service is already stopped" "Info"
    return $true
}

function Remove-WindowsService {
    Write-Host "`n--- Removing Windows Service ---`n"
    
    try {
        Write-Status "Removing service registration..." "Info"
        
        $output = sc.exe delete $ServiceName 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Status "Service removed successfully" "Success"
            return $true
        }
        else {
            # Check if service doesn't exist (which is fine)
            if ($output -match "does not exist" -or $output -match "not found") {
                Write-Status "Service does not exist in registry" "Info"
                return $true
            }
            else {
                Write-Status "Failed to remove service. Exit code: $LASTEXITCODE" "Error"
                Write-Status "Output: $output" "Error"
                return $false
            }
        }
    }
    catch {
        Write-Status "Error removing service: $_" "Error"
        return $false
    }
}

function Verify-ServiceRemoval {
    Write-Host "`n--- Verifying Service Removal ---`n"
    
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    
    if ($service) {
        Write-Status "Service still exists in registry" "Warning"
        return $false
    }
    
    Write-Status "Service successfully removed from registry" "Success"
    return $true
}

function Show-Summary {
    param([bool]$Success)
    
    Write-Host "`n" + ("=" * 60)
    
    if ($Success) {
        Write-Status "Service uninstalled successfully!" "Success"
        Write-Host "`nℹ️  The $ServiceName service has been removed."
        Write-Host "`nℹ️  To reinstall the service, run:"
        Write-Host "  .\Install-Service.ps1"
    }
    else {
        Write-Status "Service uninstall encountered issues." "Error"
        Write-Host "`nℹ️  Troubleshooting:"
        Write-Host "  • Ensure you have Administrator privileges"
        Write-Host "  • The service may still be running or locked"
        Write-Host "  • Try running: Stop-Service -Name $ServiceName -Force"
        Write-Host "  • Then manually delete using: sc.exe delete $ServiceName"
    }
    
    Write-Host ("=" * 60) "`n"
}

# Main execution
Write-Host "`n╔═══════════════════════════════════════════════════════════╗"
Write-Host "║   AppTimeTracker Windows Service Uninstaller               ║"
Write-Host "╚═══════════════════════════════════════════════════════════╝`n"

# Verify admin privileges
if (-not (Test-Administrator)) {
    Write-Status "This script requires Administrator privileges!" "Error"
    Write-Status "Please run PowerShell as Administrator and try again." "Info"
    exit 1
}

Write-Status "Running with Administrator privileges" "Success"

# Confirm uninstall
if (-not $Force) {
    if (-not (Confirm-Uninstall)) {
        Write-Status "Uninstall cancelled" "Info"
        exit 0
    }
}

# Stop service if running
if (-not (Stop-ServiceIfRunning)) {
    Show-Summary $false
    exit 1
}

# Remove service
if (-not (Remove-WindowsService)) {
    Show-Summary $false
    exit 1
}

# Verify removal
if (-not (Verify-ServiceRemoval)) {
    Show-Summary $false
    exit 1
}

Show-Summary $true
exit 0
