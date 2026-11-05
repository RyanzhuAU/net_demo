#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes the API and both WebJobs, copies the Angular bundle into wwwroot and produces
    the App Service deployment ZIP.
.DESCRIPTION
    Output layout inside the ZIP:
        /                                       ASP.NET Core API
        /wwwroot                                Angular browser bundle
        /App_Data/jobs/triggered/<triggeredJobName>
        /App_Data/jobs/continuous/<continuousJobName>
.EXAMPLE
    ./scripts/publish.ps1 -Configuration Release -OutputDirectory ./artifacts
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory = 'artifacts',

    [string]$TriggeredJobName = 'reminder-sweep',

    [string]$ContinuousJobName = 'task-processor',

    [switch]$SkipAngularBuild,

    [switch]$SkipMigrationBundle
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$webDir = Join-Path $repoRoot 'src/DemoTaskManager.Web'
$apiProject = Join-Path $repoRoot 'src/DemoTaskManager.Api/DemoTaskManager.Api.csproj'
$scheduledProject = Join-Path $repoRoot 'src/DemoTaskManager.ScheduledWebJob/DemoTaskManager.ScheduledWebJob.csproj'
$serviceBusProject = Join-Path $repoRoot 'src/DemoTaskManager.ServiceBusWebJob/DemoTaskManager.ServiceBusWebJob.csproj'
$infraProject = Join-Path $repoRoot 'src/DemoTaskManager.Infrastructure/DemoTaskManager.Infrastructure.csproj'

if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot $OutputDirectory
}

$appDir = Join-Path $OutputDirectory 'app'
$zipPath = Join-Path $OutputDirectory 'DemoTaskManager.zip'
$migrationsDir = Join-Path $OutputDirectory 'migrations'

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

function Assert-PathExists {
    param([string]$Path, [string]$Description)
    if (-not (Test-Path $Path)) { throw "Expected $Description at '$Path' but it was not found." }
}

function Resolve-AngularBrowserOutput {
    # Read angular.json rather than assuming dist/<project>/browser.
    $angularJsonPath = Join-Path $webDir 'angular.json'
    Assert-PathExists -Path $angularJsonPath -Description 'angular.json'

    $angularJson = Get-Content $angularJsonPath -Raw | ConvertFrom-Json
    $projectName = ($angularJson.projects.PSObject.Properties | Select-Object -First 1).Name
    $buildOptions = $angularJson.projects.$projectName.architect.build.options

    $base = 'dist/{0}' -f $projectName
    $browser = 'browser'

    if ($buildOptions.PSObject.Properties.Name -contains 'outputPath') {
        $outputPath = $buildOptions.outputPath
        if ($outputPath -is [string]) {
            $base = $outputPath
        }
        else {
            if ($outputPath.PSObject.Properties.Name -contains 'base') { $base = $outputPath.base }
            if ($outputPath.PSObject.Properties.Name -contains 'browser') { $browser = $outputPath.browser }
        }
    }

    $resolved = Join-Path $webDir (Join-Path $base $browser)
    if (-not (Test-Path $resolved)) {
        $fallback = Join-Path $webDir $base
        if (Test-Path (Join-Path $fallback 'index.html')) { return $fallback }
    }

    return $resolved
}

Write-Step "Preparing output directory '$OutputDirectory'"
if (Test-Path $OutputDirectory) { Remove-Item $OutputDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

if (-not $SkipAngularBuild) {
    Write-Step 'Building the Angular production bundle'
    if (-not (Test-Path (Join-Path $webDir 'node_modules'))) {
        Invoke-Checked -Executable 'npm' -Arguments @('ci') -WorkingDirectory $webDir
    }
    Invoke-Checked -Executable 'npm' -Arguments @('run', 'build') -WorkingDirectory $webDir
}

$angularOutput = Resolve-AngularBrowserOutput
Assert-PathExists -Path $angularOutput -Description 'Angular browser output'
Assert-PathExists -Path (Join-Path $angularOutput 'index.html') -Description 'Angular index.html'
Write-Host "    Angular output resolved to $angularOutput"

Write-Step 'Publishing the ASP.NET Core API'
Invoke-Checked -Executable 'dotnet' -Arguments @(
    'publish', $apiProject,
    '--configuration', $Configuration,
    '--output', $appDir
)

Write-Step 'Copying the Angular bundle into wwwroot'
$wwwroot = Join-Path $appDir 'wwwroot'
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
New-Item -ItemType Directory -Path $wwwroot -Force | Out-Null
Copy-Item -Path (Join-Path $angularOutput '*') -Destination $wwwroot -Recurse -Force
Assert-PathExists -Path (Join-Path $wwwroot 'index.html') -Description 'index.html in wwwroot'

Write-Step "Publishing the triggered WebJob to App_Data/jobs/triggered/$TriggeredJobName"
$triggeredDir = Join-Path $appDir "App_Data/jobs/triggered/$TriggeredJobName"
Invoke-Checked -Executable 'dotnet' -Arguments @(
    'publish', $scheduledProject,
    '--configuration', $Configuration,
    '--output', $triggeredDir
)
Assert-PathExists -Path (Join-Path $triggeredDir 'run.cmd') -Description 'run.cmd for the triggered WebJob'
Assert-PathExists -Path (Join-Path $triggeredDir 'settings.job') -Description 'settings.job for the triggered WebJob'

Write-Step "Publishing the continuous WebJob to App_Data/jobs/continuous/$ContinuousJobName"
$continuousDir = Join-Path $appDir "App_Data/jobs/continuous/$ContinuousJobName"
Invoke-Checked -Executable 'dotnet' -Arguments @(
    'publish', $serviceBusProject,
    '--configuration', $Configuration,
    '--output', $continuousDir
)
Assert-PathExists -Path (Join-Path $continuousDir 'run.cmd') -Description 'run.cmd for the continuous WebJob'
Assert-PathExists -Path (Join-Path $continuousDir 'settings.job') -Description 'settings.job for the continuous WebJob'

if (-not $SkipMigrationBundle) {
    Write-Step 'Creating the EF Core migration bundle'
    New-Item -ItemType Directory -Path $migrationsDir -Force | Out-Null
    Invoke-Checked -Executable 'dotnet' -Arguments @('tool', 'restore')
    Invoke-Checked -Executable 'dotnet' -Arguments @(
        'tool', 'run', 'dotnet-ef', 'migrations', 'bundle',
        '--project', $infraProject,
        '--startup-project', $apiProject,
        '--configuration', $Configuration,
        '--self-contained',
        '--target-runtime', 'win-x64',
        '--output', (Join-Path $migrationsDir 'efbundle.exe'),
        '--force'
    )
    Assert-PathExists -Path (Join-Path $migrationsDir 'efbundle.exe') -Description 'EF Core migration bundle'
}

Write-Step "Creating the deployment package '$zipPath'"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $appDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
Assert-PathExists -Path $zipPath -Description 'deployment ZIP'

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "Package created: $zipPath ($sizeMb MB)" -ForegroundColor Green
