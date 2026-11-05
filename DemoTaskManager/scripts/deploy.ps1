#Requires -Version 7.0
<#
.SYNOPSIS
    Deploys the Bicep infrastructure and/or the application package to Azure App Service.
.DESCRIPTION
    Requires an authenticated Azure CLI session (az login). Secrets are read from
    parameters or environment variables and are never written to the console.
.EXAMPLE
    ./scripts/deploy.ps1 -ResourceGroupName rg-demotask-dev -Location westeurope `
        -SqlAdministratorLogin sqladmin -EnvironmentName dev
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ResourceGroupName,

    [string]$Location = 'westeurope',

    [ValidateSet('dev', 'test', 'prod')]
    [string]$EnvironmentName = 'dev',

    [string]$ApplicationName = 'demotask',

    [string]$SubscriptionId,

    [string]$SqlAdministratorLogin,

    [securestring]$SqlAdministratorPassword,

    [string]$PackagePath = 'artifacts/DemoTaskManager.zip',

    [string]$AppServiceName,

    [switch]$SkipInfrastructure,

    [switch]$SkipApplication
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$templateFile = Join-Path $repoRoot 'infrastructure/main.bicep'

if (-not [System.IO.Path]::IsPathRooted($PackagePath)) {
    $PackagePath = Join-Path $repoRoot $PackagePath
}

function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments)

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable failed with exit code $LASTEXITCODE"
    }
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'The Azure CLI (az) is required. See https://learn.microsoft.com/cli/azure/install-azure-cli'
}

if ($SubscriptionId) {
    Write-Step 'Selecting subscription'
    Invoke-Checked -Executable 'az' -Arguments @('account', 'set', '--subscription', $SubscriptionId)
}

if (-not $SkipInfrastructure) {
    if (-not $SqlAdministratorLogin) {
        $SqlAdministratorLogin = $env:SQL_ADMIN_LOGIN
    }
    if (-not $SqlAdministratorPassword) {
        if (-not $env:SQL_ADMIN_PASSWORD) {
            throw 'Provide -SqlAdministratorPassword or set the SQL_ADMIN_PASSWORD environment variable.'
        }
        $SqlAdministratorPassword = ConvertTo-SecureString $env:SQL_ADMIN_PASSWORD -AsPlainText -Force
    }
    if (-not $SqlAdministratorLogin) {
        throw 'Provide -SqlAdministratorLogin or set the SQL_ADMIN_LOGIN environment variable.'
    }

    if (-not (Test-Path $templateFile)) { throw "Bicep template not found at $templateFile" }

    Write-Step "Ensuring resource group '$ResourceGroupName' exists"
    Invoke-Checked -Executable 'az' -Arguments @(
        'group', 'create', '--name', $ResourceGroupName, '--location', $Location, '--output', 'none'
    )

    Write-Step 'Validating the Bicep deployment'
    $plainPassword = [System.Net.NetworkCredential]::new('', $SqlAdministratorPassword).Password
    $deploymentParameters = @(
        "applicationName=$ApplicationName"
        "environmentName=$EnvironmentName"
        "location=$Location"
        "sqlAdministratorLogin=$SqlAdministratorLogin"
        "sqlAdministratorPassword=$plainPassword"
    )

    $validateArgs = @(
        'deployment', 'group', 'validate',
        '--resource-group', $ResourceGroupName,
        '--template-file', $templateFile,
        '--parameters'
    ) + $deploymentParameters + @('--output', 'none')
    Invoke-Checked -Executable 'az' -Arguments $validateArgs

    Write-Step 'Deploying infrastructure'
    $deploymentName = "demotaskmanager-$EnvironmentName-$(Get-Date -Format 'yyyyMMddHHmmss')"
    $createArgs = @(
        'deployment', 'group', 'create',
        '--resource-group', $ResourceGroupName,
        '--name', $deploymentName,
        '--template-file', $templateFile,
        '--parameters'
    ) + $deploymentParameters + @('--output', 'none')
    Invoke-Checked -Executable 'az' -Arguments $createArgs

    $outputsJson = az deployment group show --resource-group $ResourceGroupName --name $deploymentName --query properties.outputs -o json
    $outputs = $outputsJson | ConvertFrom-Json
    $AppServiceName = $outputs.appServiceName.value

    Write-Host "    App Service: $AppServiceName"
    Write-Host "    Health URL : $($outputs.healthCheckUrl.value)"
}

if (-not $SkipApplication) {
    if (-not $AppServiceName) {
        throw 'Provide -AppServiceName when infrastructure deployment is skipped.'
    }
    if (-not (Test-Path $PackagePath)) {
        throw "Deployment package not found at '$PackagePath'. Run scripts/publish.ps1 first."
    }

    Write-Step "Deploying '$([System.IO.Path]::GetFileName($PackagePath))' to App Service '$AppServiceName'"
    Invoke-Checked -Executable 'az' -Arguments @(
        'webapp', 'deploy',
        '--resource-group', $ResourceGroupName,
        '--name', $AppServiceName,
        '--src-path', $PackagePath,
        '--type', 'zip',
        '--output', 'none'
    )

    $healthUrl = "https://$AppServiceName.azurewebsites.net/health"
    Write-Step "Smoke testing $healthUrl"
    $attempt = 0
    do {
        $attempt++
        Start-Sleep -Seconds 10
        try {
            $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 30
            if ($response.StatusCode -eq 200) {
                Write-Host 'Health check passed.' -ForegroundColor Green
                break
            }
        }
        catch {
            Write-Host "    attempt $attempt did not succeed yet"
        }

        if ($attempt -ge 6) { throw "Health check did not succeed after $attempt attempts." }
    } while ($true)
}

Write-Host 'Deployment completed successfully.' -ForegroundColor Green
