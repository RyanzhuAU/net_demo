#Requires -Version 7.0
<#
.SYNOPSIS
    Restores dependencies, then builds, lints and tests the Angular frontend and the .NET solution.
.EXAMPLE
    ./scripts/build.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$SkipTests,

    [switch]$SkipFrontend
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$webDir = Join-Path $repoRoot 'src/DemoTaskManager.Web'
$solution = Join-Path $repoRoot 'DemoTaskManager.sln'

function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments, [string]$WorkingDirectory = $repoRoot)

    Push-Location $WorkingDirectory
    try {
        & $Executable @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Executable $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path $solution)) { throw "Solution not found at $solution" }

if (-not $SkipFrontend) {
    if (-not (Test-Path (Join-Path $webDir 'package.json'))) { throw "package.json not found in $webDir" }

    Write-Step 'Restoring frontend dependencies (npm ci)'
    Invoke-Checked -Executable 'npm' -Arguments @('ci') -WorkingDirectory $webDir

    Write-Step 'Linting the Angular application'
    Invoke-Checked -Executable 'npm' -Arguments @('run', 'lint') -WorkingDirectory $webDir

    if (-not $SkipTests) {
        Write-Step 'Running Angular unit tests'
        Invoke-Checked -Executable 'npm' -Arguments @('run', 'test:ci') -WorkingDirectory $webDir
    }

    Write-Step 'Building the Angular production bundle'
    Invoke-Checked -Executable 'npm' -Arguments @('run', 'build') -WorkingDirectory $webDir
}

Write-Step 'Restoring .NET dependencies'
Invoke-Checked -Executable 'dotnet' -Arguments @('restore', $solution)

Write-Step "Building the .NET solution ($Configuration)"
Invoke-Checked -Executable 'dotnet' -Arguments @('build', $solution, '--configuration', $Configuration, '--no-restore')

if (-not $SkipTests) {
    Write-Step 'Running .NET tests'
    Invoke-Checked -Executable 'dotnet' -Arguments @('test', $solution, '--configuration', $Configuration, '--no-build', '--verbosity', 'minimal')
}

Write-Host 'Build completed successfully.' -ForegroundColor Green
