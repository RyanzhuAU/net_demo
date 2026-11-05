# Deployment

## What gets provisioned

`infrastructure/main.bicep` (resource-group scope) creates:

| Resource | Module | Notes |
|---|---|---|
| Log Analytics workspace | `monitoring.bicep` | 30-day retention |
| Application Insights | `monitoring.bicep` | Workspace-based |
| Service Bus namespace | `service-bus.bicep` | Standard SKU, TLS 1.2 minimum |
| `task-processing` queue | `service-bus.bicep` | `maxDeliveryCount` 5, dead-lettering, 10-minute duplicate detection |
| Azure SQL server | `sql.bicep` | TLS 1.2, optional Entra admin, `AllowAllWindowsAzureIps` |
| Azure SQL database | `sql.bicep` | `S0` by default |
| App Service plan (Windows) | `app-service.bicep` | `S1` by default; Windows is required for `run.cmd` |
| App Service | `app-service.bicep` | System-assigned identity, `healthCheckPath: /health`, HTTPS only |
| Role assignments | `service-bus-roles.bicep` | *Azure Service Bus Data Sender* + *Data Receiver*, scoped to the namespace |

### Parameters

| Parameter | Required | Default | Notes |
|---|---|---|---|
| `applicationName` | no | `demotask` | 3–12 chars, used in resource names |
| `environmentName` | no | `dev` | `dev`, `test` or `prod` |
| `location` | no | resource group location | |
| `appServicePlanSku` | no | `S1` | `B1`, `S1`, `P0v3`, `P1v3` |
| `sqlAdministratorLogin` | **yes** | — | Supply from a pipeline variable |
| `sqlAdministratorPassword` | **yes** | — | `@secure()`; supply from a secret variable or Key Vault |
| `sqlEntraAdminObjectId` | no | `''` | Set to enable an Entra SQL administrator |
| `sqlEntraAdminLogin` | no | `''` | Display name matching the object id |
| `sqlDatabaseSkuName` | no | `S0` | |
| `serviceBusQueueName` | no | `task-processing` | |
| `scheduledWebJobCron` | no | `0 */15 * * * *` | Six-field NCRONTAB |
| `tags` | no | app/env/managedBy | |

Resource names are never hard-coded: they are derived as
`<kind>-<applicationName>-<environmentName>-<uniqueString hash>`.

### Outputs

`appServiceName`, `appServicePlanName`, `appServiceDefaultHostName`, `healthCheckUrl`,
`serviceBusNamespaceName`, `serviceBusFullyQualifiedNamespace`, `serviceBusQueueName`,
`sqlServerFqdn`, `sqlDatabaseName`, `applicationInsightsName`,
`serviceBusRoleAssignmentIds`.

No output contains a secret.

## Values that must be supplied securely

| Value | Where it comes from | Never |
|---|---|---|
| `sqlAdministratorLogin` | Azure DevOps variable group | committed |
| `sqlAdministratorPassword` | Azure DevOps **secret** variable or Key Vault reference | committed, logged, echoed |
| Service Bus credentials | Not used in Azure — managed identity instead | committed |
| Subscription / tenant ids | Service connection + variable group | in YAML |

`infrastructure/main.parameters.dev.json` deliberately contains
`REPLACE_WITH_PIPELINE_VARIABLE` / `REPLACE_WITH_PIPELINE_SECRET` placeholders.

To use Key Vault instead of a secret variable:

```bash
az keyvault secret set --vault-name <vault> --name sql-admin-password --value '<password>'
```

Then link the vault in **Pipelines → Library → Variable group → Link secrets from an Azure
key vault as variables**, and expose the secret as `sqlAdministratorPassword`.

## Manual deployment

```powershell
az login
az account set --subscription <subscription-id>

./scripts/publish.ps1 -Configuration Release -OutputDirectory artifacts

$env:SQL_ADMIN_PASSWORD = '<strong-password>'
./scripts/deploy.ps1 `
    -ResourceGroupName rg-demotask-dev `
    -Location westeurope `
    -EnvironmentName dev `
    -SqlAdministratorLogin sqladmin
```

```bash
az login
az account set --subscription <subscription-id>

./scripts/publish.sh --configuration Release --output artifacts

export SQL_ADMIN_PASSWORD='<strong-password>'
./scripts/deploy.sh \
    --resource-group rg-demotask-dev \
    --location westeurope \
    --environment dev \
    --sql-admin-login sqladmin
```

Deploy only the application to an existing App Service:

```powershell
./scripts/deploy.ps1 -ResourceGroupName rg-demotask-dev -AppServiceName app-demotask-dev-abc123 -SkipInfrastructure
```

### Applying migrations manually

```powershell
$connection = "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;User ID=<login>;Password=$env:SQL_ADMIN_PASSWORD;Encrypt=True;"
./artifacts/migrations/efbundle.exe --connection $connection
```

The bundle is self-contained (`win-x64`), so the deployment agent needs no .NET SDK. The
application never migrates on start-up outside Development.

## Azure DevOps pipeline

### Import

1. **Pipelines → New pipeline → Azure Repos Git → Existing Azure Pipelines YAML file**
2. Select `/pipelines/azure-pipelines.yml`
3. Save (do not run yet — create the variable groups and environments first)

### Service connection

**Project settings → Service connections → New service connection → Azure Resource Manager**.
Workload identity federation is recommended. Grant it **Contributor** plus
**User Access Administrator** on the target resource group — the second role is required
because the template creates Service Bus role assignments.

### Variable groups

Create `demo-task-manager-dev`, `demo-task-manager-test`, `demo-task-manager-prod`:

| Variable | Secret | Purpose |
|---|---|---|
| `azureServiceConnection` | no | Service connection name |
| `subscriptionId` | no | Target subscription |
| `resourceGroupName` | no | Target resource group |
| `environmentName` | no | `dev` / `test` / `prod` |
| `location` | no | Azure region |
| `appServiceName` | no | Filled from the Bicep output at run time |
| `sqlAdministratorLogin` | no | SQL admin login |
| `sqlAdministratorPassword` | **yes** | SQL admin password |
| `sqlConnectionString` | **yes** | Optional pre-built override |
| `serviceBusNamespace` | no | Namespace name |
| `serviceBusQueueName` | no | `task-processing` |

### Environments and approvals

**Pipelines → Environments → New environment** for `demo-task-manager-dev`,
`demo-task-manager-test` and `demo-task-manager-prod`. On the production environment add
**Approvals and checks → Approvals** and nominate the approvers. Every job in the `Prod`
stage then waits for sign-off.

### Stage flow

```
Build ──► Dev ──► Test ──► Prod (manual approval)
```

`Build` runs on every branch in the trigger and on pull requests; the deployment stages are
skipped for PR builds. `Prod` additionally requires `refs/heads/main`.

Build stage steps: `UseDotNet@2` (10.0.x) → `NodeTool@0` (22.22.3) → `npm ci` →
`npm run lint` → `npm run test:ci` → `npm run build` → `dotnet restore` → `dotnet build` →
`dotnet test` (TRX + coverage) → `scripts/publish.ps1` → packaging verification →
`az bicep build` → publish `app-package`, `migrations` and `infrastructure` artifacts.

Deployment stage steps: validate Bicep → deploy Bicep (outputs exported as job variables) →
open a temporary SQL firewall rule for the agent → run `efbundle.exe` → remove the firewall
rule (`condition: always()`) → `AzureWebApp@1` zip deploy → poll `/health` up to 8 times.

## Package layout verification

The build stage fails unless all of these exist:

```
artifacts/app/wwwroot/index.html
artifacts/app/App_Data/jobs/triggered/reminder-sweep/run.cmd
artifacts/app/App_Data/jobs/triggered/reminder-sweep/settings.job
artifacts/app/App_Data/jobs/continuous/task-processor/run.cmd
artifacts/app/App_Data/jobs/continuous/task-processor/settings.job
artifacts/DemoTaskManager.zip
artifacts/migrations/efbundle.exe
```

## Post-deployment checks

```bash
curl -f https://<app-service-name>.azurewebsites.net/health
curl -f https://<app-service-name>.azurewebsites.net/config.json
curl -f "https://<app-service-name>.azurewebsites.net/api/tasks?pageSize=5"
```

In the portal:

- **App Service → WebJobs** should list `reminder-sweep` (Triggered, scheduled) and
  `task-processor` (Continuous, Running).
- **Service Bus → Queues → task-processing** should show messages after using
  **Send to Service Bus** in the UI.
- **Application Insights → Transaction search** should correlate browser, API and WebJob
  traces through `X-Correlation-Id`.

## WebJob schedule

`settings.job` for the triggered job uses six-field NCRONTAB:

```
{second} {minute} {hour} {day} {month} {day-of-week}
   0        */15     *     *      *          *          → every 15 minutes
   0         0       *     *      *          *          → hourly
   0         0       2     *      *          *          → daily at 02:00 UTC
   0        */5      *     *      *          *          → every 5 minutes
```

This is **not** the five-field Unix cron format. Change the schedule at deploy time with the
`scheduledWebJobCron` parameter; to change it on a running site, edit
`App_Data/jobs/triggered/reminder-sweep/settings.job` through the Kudu console and restart
the app.

Continuous WebJobs require **Always On**, which the template enables for every SKU except
`B1`.

## Troubleshooting deployments

| Symptom | Cause / fix |
|---|---|
| `AuthorizationFailed` creating role assignments | The service connection needs *User Access Administrator* on the resource group |
| `InvalidTemplateDeployment` on SQL | Password must satisfy Azure SQL complexity rules (12+ chars, 3 of 4 character classes) |
| Migration step: `Login failed for user` | Wrong `sqlAdministratorLogin`/`sqlAdministratorPassword` in the variable group |
| Migration step: cannot open server | The temporary firewall rule failed; check that the agent IP was added |
| Continuous WebJob shows *Pending Restart* | Enable Always On, or the process exited — check **WebJobs → Logs** |
| Service Bus `Unauthorized` | Role assignment propagation can take several minutes; verify the identity has both data roles |
| SPA loads but API calls 404 | Confirm `Spa__ApiBaseUrl` is `/api` and that the ZIP contains `wwwroot/index.html` |
| Health check red | `/health` includes the SQL check — verify the connection string app setting and firewall |

## Cleanup

```bash
az group delete --name rg-demotask-dev --yes --no-wait
```

Also remove the Azure DevOps variable groups, environments and the service connection if the
demo is finished.
