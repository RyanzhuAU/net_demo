# Local development

## Toolchain

| Tool | Required version | Check |
|---|---|---|
| .NET SDK | 10.0.400+ | `dotnet --version` |
| Node.js | `^22.22.3 \|\| ^24.15.0 \|\| >=26.0.0` | `node --version` |
| npm | 10+ | `npm --version` |
| PowerShell | 7+ (for `.ps1` scripts) | `pwsh --version` |
| SQL Server | LocalDB / Developer / Docker | `sqlcmd -?` |

Angular CLI is **not** required globally — it is installed as a dev dependency, so
`npx ng ...` or the npm scripts always use the pinned 22.1.5.

## First run

```bash
dotnet restore DemoTaskManager.sln
dotnet tool restore
cd src/DemoTaskManager.Web && npm ci && cd ../..
```

Start the API (applies migrations and seeds four demo tasks in Development):

```bash
dotnet run --project src/DemoTaskManager.Api
```

Start the SPA in a second terminal:

```bash
cd src/DemoTaskManager.Web
npm start
```

Open <http://localhost:4200>.

## Ports and proxying

| Process | URL |
|---|---|
| API (HTTP) | <http://localhost:5080> |
| API (HTTPS) | <https://localhost:7080> |
| Angular dev server | <http://localhost:4200> |

`proxy.conf.json` forwards `/api`, `/health` and `/config.json` to `http://localhost:5080`,
so the SPA and API share an origin during development and CORS never comes into play. The
API still registers a development-only CORS policy for `localhost:4200` in case you prefer
to bypass the proxy.

## Configuration precedence

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (Development only)
4. Environment variables (`Section__Key`)
5. Command line

Store anything sensitive in user secrets:

```bash
cd src/DemoTaskManager.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>"
dotnet user-secrets set "ServiceBus:ConnectionString" "<connection-string>"
dotnet user-secrets set "ServiceBus:Enabled" "true"
```

## Database

Development defaults to LocalDB with `Database:AutoMigrate = true`. Reset it at any time:

```bash
dotnet tool run dotnet-ef database drop \
  --project src/DemoTaskManager.Infrastructure \
  --startup-project src/DemoTaskManager.Api --force
```

The next API start recreates and reseeds it. Seed data lives in
`DbInitializer.CreateSeedData` and uses fixed GUIDs so it is stable across resets.

## Frontend workflow

```bash
cd src/DemoTaskManager.Web
npm start          # dev server
npm run lint       # ESLint (TypeScript + templates)
npm test           # vitest watch mode
npm run test:ci    # single run, used by CI
npm run build      # production bundle
npm run format     # Prettier
```

Generate new code with the CLI; schematics default to standalone components with
`OnPush` change detection and SCSS:

```bash
npx ng generate component features/tasks/components/task-summary
npx ng generate service core/services/telemetry
```

### Frontend structure

```
src/app/
  core/
    api/api-client.ts                     typed HttpClient wrapper (api:// prefix)
    config/app-config.service.ts          runtime config signal
    config/runtime-config.initializer.ts  provideAppInitializer target
    guards/                               functional route guards
    interceptors/                         base URL, correlation id, loading, error
    models/                               TypeScript mirrors of the .NET DTOs
    services/                             loading + notification signals
  shared/components/                      spinner, empty state, pagination, dialog, badge, field error, toasts
  features/
    tasks/
      components/                         task-filter, task-table
      pages/                              task-list-page, task-detail-page, task-form-page
      services/task-api.service.ts
      task.routes.ts                      lazy feature routes
    not-found/not-found-page.ts
  app.ts / app.html / app.scss            application shell
  app.config.ts                           bootstrapApplication providers
  app.routes.ts                           root routes
```

## Running the WebJobs locally

### Reminder sweep

```bash
export ConnectionStrings__DefaultConnection="<connection-string>"
export Reminders__BatchSize=50
dotnet run --project src/DemoTaskManager.ScheduledWebJob
echo "exit code: $?"     # 0 on success, 1 on failure
```

Create an overdue task in the UI (a due date in the past, status not Completed) to see the
sweep pick it up. Run it twice: the second run reports zero candidates, which demonstrates
the claim-then-act idempotency.

### Service Bus consumer

```bash
export ConnectionStrings__DefaultConnection="<connection-string>"
export ServiceBusConnection="<service-bus-connection-string>"
export ServiceBus__QueueName="task-processing"
dotnet run --project src/DemoTaskManager.ServiceBusWebJob
```

Then press **Send to Service Bus** on a task detail page (with `ServiceBus:Enabled=true` in
the API) and watch the consumer log the correlation id and outcome.

## Tests

```bash
dotnet test DemoTaskManager.sln
cd src/DemoTaskManager.Web && npm run test:ci
```

Run a single .NET test project or filter:

```bash
dotnet test tests/DemoTaskManager.Application.Tests
dotnet test DemoTaskManager.sln --filter "FullyQualifiedName~Reminder"
```

Run a single Angular spec:

```bash
cd src/DemoTaskManager.Web
npx ng test --no-watch --include src/app/features/tasks/services/task-api.service.spec.ts
```

## Full local build

```powershell
./scripts/build.ps1                       # lint + test + build everything
./scripts/publish.ps1                     # produce artifacts/DemoTaskManager.zip
```

```bash
./scripts/build.sh
./scripts/publish.sh
```

To preview exactly what App Service will run:

```bash
cd artifacts/app
ConnectionStrings__DefaultConnection="<connection-string>" dotnet DemoTaskManager.Api.dll
```

Open <http://localhost:5000> — the API now serves the compiled Angular bundle from
`wwwroot`, with `/api` and `/health` excluded from the SPA fallback.
