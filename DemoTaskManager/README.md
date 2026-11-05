# Demo Task Manager

A complete, runnable demo of a modern Azure full-stack application:

- **Angular 22** single-page application (standalone components, signals, reactive forms)
- **ASP.NET Core 10** Web API with **Entity Framework Core 10** and Azure SQL
- **Two Azure App Service WebJobs**: a scheduled reminder sweep and a continuous Service Bus consumer
- **Bicep** infrastructure as code and an importable **Azure DevOps** YAML pipeline

Everything is packaged into a single App Service ZIP: the API serves the compiled Angular
bundle, and both WebJobs ship inside `App_Data/jobs`.

---

## Table of contents

- [Architecture overview](#architecture-overview)
- [Repository layout](#repository-layout)
- [Prerequisites](#prerequisites)
- [Version decisions](#version-decisions)
- [Local setup](#local-setup)
- [Running frontend and backend together](#running-frontend-and-backend-together)
- [EF Core migrations](#ef-core-migrations)
- [Running the WebJobs locally](#running-the-webjobs-locally)
- [Environment variables](#environment-variables)
- [Azure deployment](#azure-deployment)
- [Azure DevOps pipeline](#azure-devops-pipeline)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)
- [Cleanup](#cleanup)

---

## Architecture overview

```
Browser ──► App Service (Windows)
              ├── ASP.NET Core API  (/api, /health, /config.json)
              ├── wwwroot           (Angular browser bundle, SPA fallback)
              └── App_Data/jobs
                    ├── triggered/reminder-sweep   (CRON, one batch then exit)
                    └── continuous/task-processor  (Service Bus queue trigger)
                          │
            ┌─────────────┼──────────────┐
            ▼             ▼              ▼
       Azure SQL   Service Bus     Application
       Database    task-processing  Insights
```

Layering:

| Project | Responsibility |
|---|---|
| `DemoTaskManager.Domain` | `TaskItem` entity, `TaskStatus` enum, domain invariants |
| `DemoTaskManager.Contracts` | Request/response DTOs shared by API, WebJobs and pipeline |
| `DemoTaskManager.Application` | Use cases, abstractions, FluentValidation validators, explicit mapping |
| `DemoTaskManager.Infrastructure` | EF Core `AppDbContext`, repositories, Service Bus publisher |
| `DemoTaskManager.Api` | Controllers, middleware, health checks, SPA hosting |
| `DemoTaskManager.Web` | Angular 22 SPA |
| `DemoTaskManager.ScheduledWebJob` | Triggered WebJob: overdue reminder sweep |
| `DemoTaskManager.ServiceBusWebJob` | Continuous WebJob: `task-processing` queue consumer |

Full detail, including Mermaid diagrams, is in [docs/architecture.md](docs/architecture.md).

## Repository layout

```
DemoTaskManager/
  DemoTaskManager.sln
  Directory.Build.props            Shared MSBuild settings (net10.0, nullable, warnings-as-errors)
  Directory.Packages.props         Central NuGet package versions
  global.json                      Pins the .NET SDK band
  dotnet-tools.json                Local dotnet-ef tool manifest
  src/
    DemoTaskManager.Api/
    DemoTaskManager.Application/
    DemoTaskManager.Contracts/
    DemoTaskManager.Domain/
    DemoTaskManager.Infrastructure/
    DemoTaskManager.ScheduledWebJob/
    DemoTaskManager.ServiceBusWebJob/
    DemoTaskManager.Web/
  tests/
    DemoTaskManager.Api.Tests/
    DemoTaskManager.Application.Tests/
    DemoTaskManager.WebJobs.Tests/
  infrastructure/
    main.bicep
    main.parameters.dev.json
    modules/
      app-service.bicep
      monitoring.bicep
      service-bus.bicep
      service-bus-roles.bicep
      sql.bicep
  pipelines/
    azure-pipelines.yml
    templates/{build,deploy-infrastructure,deploy-application}.yml
  scripts/
    build.ps1 / build.sh
    publish.ps1 / publish.sh
    deploy.ps1 / deploy.sh
  docs/
    architecture.md
    local-development.md
    deployment.md
```

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | **10.0.400** or later 10.x | `global.json` uses `rollForward: latestFeature` |
| Node.js | **22.22.3+**, 24.15+, or 26+ | Required by Angular CLI 22 |
| npm | 10.x or later | Bundled with Node.js |
| Angular CLI | **22.1.5** | Installed locally via `npm ci`; no global install needed |
| SQL Server | LocalDB, Developer Edition or Docker | Any SQL Server 2019+ |
| Azure CLI | 2.60+ | Only needed for deployment |
| PowerShell | 7.0+ | For the `.ps1` scripts |

Verify:

```bash
dotnet --version   # 10.0.400
node --version     # v22.23.0
npm --version      # 10.9.8
```

## Version decisions

- **Angular 22.1.5** is the latest stable release and is compatible with the installed
  Node.js 22.23.0 (`engines.node` requires `^22.22.3 || ^24.15.0 || >=26.0.0`).
- **Vitest + jsdom** is the unit test runner. It is what Angular CLI 22 scaffolds by
  default (`@angular/build:unit-test`), replacing Karma. **npm** is the package manager.
- **Azure WebJobs SDK v3** (`Microsoft.Azure.WebJobs` 3.0.47,
  `Microsoft.Azure.WebJobs.Extensions.ServiceBus` 5.17.0) is the newest stable line. It
  targets `netstandard2.0`/`net6.0` but runs correctly on `net10.0`; the
  `DemoTaskManager.ServiceBusWebJob` project suppresses the resulting transitive
  compatibility warnings rather than downgrading the target framework.
- **FluentAssertions 7.2.0** is used instead of 8.x. Version 8 moved to a paid commercial
  licence, which would make this demo awkward to redistribute; 7.x is the last MIT release.
- **Application Insights** is registered only when a connection string is configured,
  because the SDK throws at host start-up when it is missing.
- **Windows App Service plan** is required: both WebJobs are launched by `run.cmd`.

## Local setup

### 1. Clone and restore

```bash
git clone <your-fork-url>
cd DemoTaskManager

dotnet restore DemoTaskManager.sln
dotnet tool restore

cd src/DemoTaskManager.Web
npm ci
cd ../..
```

### 2. SQL Server

The development configuration targets LocalDB:

```
Server=(localdb)\mssqllocaldb;Database=DemoTaskManager;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

Prefer Docker? Start SQL Server and override the connection string:

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<your-strong-password>" \
  -p 1433:1433 --name demo-sql -d mcr.microsoft.com/mssql/server:2022-latest

# bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=DemoTaskManager;User Id=sa;Password=<your-strong-password>;TrustServerCertificate=True"
```

```powershell
# PowerShell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost,1433;Database=DemoTaskManager;User Id=sa;Password=<your-strong-password>;TrustServerCertificate=True'
```

> Never commit a real connection string. `appsettings.Development.json` intentionally
> contains only a LocalDB trusted-connection value, and `.gitignore` blocks
> `appsettings.*.local.json`, `.env` and `secrets.json`.

In Development, `Database:AutoMigrate` is `true`, so the API applies migrations and inserts
four demo tasks on first start. Production startup never migrates automatically.

### 3. Service Bus (optional locally)

The API runs with `ServiceBus:Enabled = false` in Development and logs a warning instead of
publishing. To exercise the real queue locally:

```bash
az servicebus namespace create --resource-group <rg> --name <namespace> --location westeurope --sku Standard
az servicebus queue create --resource-group <rg> --namespace-name <namespace> --name task-processing \
  --enable-duplicate-detection true

# Use user secrets rather than files under source control
cd src/DemoTaskManager.Api
dotnet user-secrets set "ServiceBus:Enabled" "true"
dotnet user-secrets set "ServiceBus:ConnectionString" "<connection-string>"
```

## Running frontend and backend together

Two terminals:

```bash
# Terminal 1 - API on http://localhost:5080
dotnet run --project src/DemoTaskManager.Api

# Terminal 2 - Angular dev server on http://localhost:4200
cd src/DemoTaskManager.Web
npm start
```

`proxy.conf.json` forwards `/api`, `/health` and `/config.json` from the dev server to
`http://localhost:5080`, so no CORS configuration is needed during development.

Useful URLs:

| URL | Purpose |
|---|---|
| <http://localhost:4200> | Angular application |
| <http://localhost:5080/swagger> | OpenAPI explorer |
| <http://localhost:5080/health> | Full health report |
| <http://localhost:5080/health/live> | Liveness probe |
| <http://localhost:5080/config.json> | SPA runtime configuration |

### Angular commands

```bash
cd src/DemoTaskManager.Web
npm start          # dev server with proxy
npm run build      # production build -> dist/demo-task-manager-web/browser
npm run build:dev  # development build
npm test           # vitest in watch mode
npm run test:ci    # single vitest run
npm run lint       # ESLint over TypeScript and templates
npm run format     # Prettier
```

### .NET commands

```bash
dotnet build DemoTaskManager.sln --configuration Release
dotnet test  DemoTaskManager.sln --configuration Release
dotnet run   --project src/DemoTaskManager.Api
dotnet run   --project src/DemoTaskManager.ScheduledWebJob
dotnet run   --project src/DemoTaskManager.ServiceBusWebJob
```

### One-shot build of everything

```powershell
./scripts/build.ps1 -Configuration Release
```

```bash
./scripts/build.sh --configuration Release
```

## EF Core migrations

The `dotnet-ef` tool is pinned in `dotnet-tools.json`.

```bash
dotnet tool restore

# Add a migration
dotnet tool run dotnet-ef migrations add <Name> \
  --project src/DemoTaskManager.Infrastructure \
  --startup-project src/DemoTaskManager.Api \
  --output-dir Persistence/Migrations

# Apply migrations to the local database
dotnet tool run dotnet-ef database update \
  --project src/DemoTaskManager.Infrastructure \
  --startup-project src/DemoTaskManager.Api

# Remove the last (unapplied) migration
dotnet tool run dotnet-ef migrations remove \
  --project src/DemoTaskManager.Infrastructure \
  --startup-project src/DemoTaskManager.Api

# Produce the deployment-safe migration bundle used by the pipeline
dotnet tool run dotnet-ef migrations bundle \
  --project src/DemoTaskManager.Infrastructure \
  --startup-project src/DemoTaskManager.Api \
  --self-contained --target-runtime win-x64 \
  --output artifacts/migrations/efbundle.exe --force

# Generate an idempotent SQL script for DBA review
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/DemoTaskManager.Infrastructure \
  --startup-project src/DemoTaskManager.Api \
  --output artifacts/migrations/migrate.sql
```

The pipeline runs `efbundle.exe`, never `Database.Migrate()` at application start-up.

## Running the WebJobs locally

### Scheduled reminder sweep (triggered)

```bash
dotnet run --project src/DemoTaskManager.ScheduledWebJob
```

It executes one batch and exits with `0` on success or `1` on failure. There is no
in-process timer; the schedule lives in `settings.job`:

```json
{ "schedule": "0 */15 * * * *" }
```

**App Service WebJobs use six-field NCRONTAB** — `{second} {minute} {hour} {day} {month} {day-of-week}`.
`0 */15 * * * *` means "at second 0 of every 15th minute". This is *not* the five-field
Unix cron format. The value is configurable through the `scheduledWebJobCron` Bicep
parameter (surfaced as the `WebJobs__ScheduledCron` app setting).

**Concurrency and idempotency.** Overlapping runs are safe. Each candidate is claimed with
a single conditional statement:

```sql
UPDATE Tasks SET IsReminderSent = 1, UpdatedAtUtc = @now
WHERE Id = @id AND IsReminderSent = 0
```

Implemented with `ExecuteUpdateAsync`, this returns `1` for exactly one caller. Only the
winner dispatches the reminder, so a task can never be reminded twice.

### Service Bus consumer (continuous)

```bash
cd src/DemoTaskManager.ServiceBusWebJob
# Local development uses a connection string...
export ServiceBusConnection="<connection-string>"
# ...deployed environments use managed identity instead:
# export ServiceBusConnection__fullyQualifiedNamespace="<namespace>.servicebus.windows.net"
export ConnectionStrings__DefaultConnection="<sql-connection-string>"
dotnet run
```

Processing is idempotent: replaying a message whose task already has the requested status
is a no-op. Invalid payloads throw, so Service Bus retries and eventually dead-letters them
after `maxDeliveryCount` (5). Only identifiers are logged — never message bodies.

## Environment variables

Double underscores map to configuration sections (`Section__Key`).

| Variable | Used by | Description |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | API, both WebJobs | Azure SQL connection string. **Secret.** |
| `ASPNETCORE_ENVIRONMENT` | API | `Development`, `Staging` or `Production` |
| `Database__AutoMigrate` | API | Development-only automatic migration. `false` in Azure |
| `Spa__ApiBaseUrl` | API | Value returned by `/config.json`; defaults to `/api` |
| `Spa__ApplicationInsightsConnectionString` | API | Optional browser telemetry connection string |
| `ServiceBus__Enabled` | API | `false` disables publishing (offline demo) |
| `ServiceBus__QueueName` | API, SB WebJob | Defaults to `task-processing` |
| `ServiceBus__FullyQualifiedNamespace` | API | `<namespace>.servicebus.windows.net` — managed identity |
| `ServiceBus__ConnectionString` | API | Local development fallback. **Secret.** |
| `ServiceBusConnection__fullyQualifiedNamespace` | SB WebJob | Managed identity binding for the WebJobs SDK |
| `ServiceBusConnection` | SB WebJob | Local connection string fallback. **Secret.** |
| `Reminders__BatchSize` | Scheduled WebJob | Max tasks handled per run (default 100) |
| `Reminders__GraceMinutes` | Scheduled WebJob | Delay after the due date before reminding |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | All | Enables telemetry when present |

**Never commit** passwords, access keys, tokens or real connection strings. In Azure they
are injected as App Service application settings by Bicep; in Azure DevOps they come from
secret variables in a variable group; locally use `dotnet user-secrets` or environment
variables.

## Azure deployment

Detailed walkthrough: [docs/deployment.md](docs/deployment.md).

### 1. Build the deployment package

```powershell
./scripts/publish.ps1 -Configuration Release -OutputDirectory artifacts
```

```bash
./scripts/publish.sh --configuration Release --output artifacts
```

This produces:

```
artifacts/
  app/
    DemoTaskManager.Api.dll ...
    wwwroot/index.html ...
    App_Data/jobs/triggered/reminder-sweep/{run.cmd,settings.job,...}
    App_Data/jobs/continuous/task-processor/{run.cmd,settings.job,...}
  DemoTaskManager.zip
  migrations/efbundle.exe
```

### 2. Provision and deploy

```powershell
az login
$env:SQL_ADMIN_PASSWORD = '<strong-password>'   # never hard-coded, never logged
./scripts/deploy.ps1 -ResourceGroupName rg-demotask-dev -Location westeurope `
    -EnvironmentName dev -SqlAdministratorLogin sqladmin
```

```bash
az login
export SQL_ADMIN_PASSWORD='<strong-password>'
./scripts/deploy.sh --resource-group rg-demotask-dev --location westeurope \
    --environment dev --sql-admin-login sqladmin
```

The script creates the resource group, validates and deploys `infrastructure/main.bicep`,
zip-deploys the package, then polls `/health` until it returns `200`.

Provisioned resources: Windows App Service plan, App Service (system-assigned identity,
health check path `/health`), Azure SQL server + database, Service Bus namespace + the
`task-processing` queue, Log Analytics workspace and Application Insights, plus
`Azure Service Bus Data Sender`/`Data Receiver` role assignments scoped to the namespace.

### Identity decisions

- **Service Bus uses managed identity.** The App Service identity receives only the data
  sender and data receiver roles on the namespace — no management permissions.
- **SQL uses a securely supplied connection string.** Managed-identity authentication to
  Azure SQL requires an in-database `CREATE USER ... FROM EXTERNAL PROVIDER` step that
  cannot be expressed in Bicep and needs an Entra-authenticated SQL session. That extra
  moving part makes an unattended demo fragile, so SQL authentication is used and the
  credentials are injected as secure deployment parameters. `docs/deployment.md`
  documents how to switch to managed identity for SQL when you are ready.

## Azure DevOps pipeline

Import `pipelines/azure-pipelines.yml`:

1. **Pipelines → New pipeline → Azure Repos Git → Existing Azure Pipelines YAML file**,
   then select `/pipelines/azure-pipelines.yml`.
2. **Project settings → Service connections → New → Azure Resource Manager** (workload
   identity federation is recommended). Note the name.
3. **Pipelines → Library → Variable groups**, create `demo-task-manager-dev`,
   `demo-task-manager-test` and `demo-task-manager-prod`, each containing:

   | Variable | Secret | Example |
   |---|---|---|
   | `azureServiceConnection` | no | `sc-demo-task-manager` |
   | `subscriptionId` | no | GUID |
   | `resourceGroupName` | no | `rg-demotask-dev` |
   | `environmentName` | no | `dev` |
   | `location` | no | `westeurope` |
   | `appServiceName` | no | set automatically from Bicep output |
   | `sqlAdministratorLogin` | no | `sqladmin` |
   | `sqlAdministratorPassword` | **yes** | strong password |
   | `sqlConnectionString` | **yes** | optional pre-built override |
   | `serviceBusNamespace` | no | `sb-demotask-dev-abc123` |
   | `serviceBusQueueName` | no | `task-processing` |

4. **Pipelines → Environments**, create `demo-task-manager-dev`, `-test` and `-prod`. Add an
   **Approvals** check to `demo-task-manager-prod` to gate production.

Stages: `Build` → `Dev` → `Test` → `Prod`. The build stage runs `npm ci`, Angular lint,
Angular unit tests, the Angular production build, `dotnet restore/build/test`, produces the
App Service ZIP, the EF Core migration bundle and validates Bicep, then publishes the
`app-package`, `migrations` and `infrastructure` artifacts. Each deployment stage validates
and deploys Bicep, applies migrations with `efbundle.exe`, zip-deploys the package and
smoke tests `/health`.

No subscription id, tenant id, password, key or token appears in the YAML.

## Testing

```bash
# .NET - 57 tests
dotnet test DemoTaskManager.sln --configuration Release

# Angular - 24 tests
cd src/DemoTaskManager.Web && npm run test:ci
```

| Suite | Coverage |
|---|---|
| `DemoTaskManager.Application.Tests` | Entity/DTO mapping, create & update validation, status filtering, pagination, search, Service Bus publishing, reminder claim, idempotent processing |
| `DemoTaskManager.Api.Tests` | Full CRUD over HTTP with `WebApplicationFactory`, ProblemDetails, paging, `/process`, SPA fallback exclusions, security headers |
| `DemoTaskManager.WebJobs.Tests` | Service Bus message (de)serialisation, poison-message handling, transient failure propagation, overdue selection, reminder state update |
| Angular specs | Task list rendering, service API calls, status filtering, pagination interaction, form validation, error interceptor, loading and empty states |

Relational behaviour is tested against **SQLite in-memory**, not the EF Core InMemory
provider, so query translation and paging are genuinely exercised.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `A connection string was not found` at start-up | Set `ConnectionStrings__DefaultConnection`, or `APPLICATIONINSIGHTS_CONNECTION_STRING` if the message mentions telemetry |
| `Angular build output was not found` on `/` | Run `npm run build` in `src/DemoTaskManager.Web`, or use `scripts/publish.ps1` which copies it into `wwwroot` |
| Angular CLI refuses to run | Node.js must satisfy `^22.22.3 \|\| ^24.15.0 \|\| >=26.0.0` |
| `dotnet ef` cannot find the design package | Always pass `--startup-project src/DemoTaskManager.Api` |
| 404 HTML returned for a bad `/api` path | Expected: `/api`, `/health` and `/swagger` are excluded from the SPA fallback and return JSON 404 |
| Continuous WebJob keeps restarting | Enable **Always On** (not available on the `B1` free-tier-like SKU used for quick tests) |
| Service Bus `Unauthorized` in Azure | Role assignments can take a few minutes; confirm the App Service identity holds *Azure Service Bus Data Sender/Receiver* |
| Pipeline migration step times out | The temporary SQL firewall rule may have been removed early; re-run the stage |
| `Login failed for user` during migrations | Check `sqlAdministratorLogin`/`sqlAdministratorPassword` in the variable group |

Logs: **App Service → Log stream** for the API, and **App Service → WebJobs → Logs** for
both jobs. Application Insights receives traces from all three processes and correlates
them through the `X-Correlation-Id` header propagated by the Angular interceptor.

## Cleanup

```bash
az group delete --name rg-demotask-dev --yes --no-wait
```

Locally:

```bash
dotnet tool run dotnet-ef database drop \
  --project src/DemoTaskManager.Infrastructure \
  --startup-project src/DemoTaskManager.Api --force

rm -rf artifacts src/DemoTaskManager.Web/node_modules src/DemoTaskManager.Web/dist
```
