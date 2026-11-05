#!/usr/bin/env bash
# Publishes the API and both WebJobs, copies the Angular bundle into wwwroot and produces
# the App Service deployment ZIP.
#
# ZIP layout:
#   /                                            ASP.NET Core API
#   /wwwroot                                     Angular browser bundle
#   /App_Data/jobs/triggered/<triggered-job>
#   /App_Data/jobs/continuous/<continuous-job>
set -Eeuo pipefail

CONFIGURATION="Release"
OUTPUT_DIRECTORY="artifacts"
TRIGGERED_JOB_NAME="reminder-sweep"
CONTINUOUS_JOB_NAME="task-processor"
SKIP_ANGULAR_BUILD=0
SKIP_MIGRATION_BUNDLE=0

usage() {
    cat <<'EOF'
Usage: publish.sh [--configuration Debug|Release] [--output DIR]
                  [--triggered-job NAME] [--continuous-job NAME]
                  [--skip-angular-build] [--skip-migration-bundle]
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration) CONFIGURATION="$2"; shift 2 ;;
        --output) OUTPUT_DIRECTORY="$2"; shift 2 ;;
        --triggered-job) TRIGGERED_JOB_NAME="$2"; shift 2 ;;
        --continuous-job) CONTINUOUS_JOB_NAME="$2"; shift 2 ;;
        --skip-angular-build) SKIP_ANGULAR_BUILD=1; shift ;;
        --skip-migration-bundle) SKIP_MIGRATION_BUNDLE=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage; exit 1 ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WEB_DIR="${REPO_ROOT}/src/DemoTaskManager.Web"
API_PROJECT="${REPO_ROOT}/src/DemoTaskManager.Api/DemoTaskManager.Api.csproj"
SCHEDULED_PROJECT="${REPO_ROOT}/src/DemoTaskManager.ScheduledWebJob/DemoTaskManager.ScheduledWebJob.csproj"
SERVICEBUS_PROJECT="${REPO_ROOT}/src/DemoTaskManager.ServiceBusWebJob/DemoTaskManager.ServiceBusWebJob.csproj"
INFRA_PROJECT="${REPO_ROOT}/src/DemoTaskManager.Infrastructure/DemoTaskManager.Infrastructure.csproj"

[[ "${OUTPUT_DIRECTORY}" = /* ]] || OUTPUT_DIRECTORY="${REPO_ROOT}/${OUTPUT_DIRECTORY}"
APP_DIR="${OUTPUT_DIRECTORY}/app"
ZIP_PATH="${OUTPUT_DIRECTORY}/DemoTaskManager.zip"
MIGRATIONS_DIR="${OUTPUT_DIRECTORY}/migrations"

step() { printf '==> %s\n' "$1"; }

assert_path() {
    [[ -e "$1" ]] || { echo "Expected $2 at '$1' but it was not found." >&2; exit 1; }
}

resolve_angular_output() {
    # Read angular.json rather than assuming dist/<project>/browser.
    local angular_json="${WEB_DIR}/angular.json"
    assert_path "${angular_json}" "angular.json"

    node -e '
const fs = require("fs");
const path = require("path");
const webDir = process.argv[1];
const config = JSON.parse(fs.readFileSync(path.join(webDir, "angular.json"), "utf8"));
const projectName = Object.keys(config.projects)[0];
const options = config.projects[projectName].architect.build.options ?? {};
let base = `dist/${projectName}`;
let browser = "browser";
if (typeof options.outputPath === "string") {
  base = options.outputPath;
} else if (options.outputPath && typeof options.outputPath === "object") {
  base = options.outputPath.base ?? base;
  browser = options.outputPath.browser ?? browser;
}
const resolved = path.join(webDir, base, browser);
if (!fs.existsSync(resolved) && fs.existsSync(path.join(webDir, base, "index.html"))) {
  process.stdout.write(path.join(webDir, base));
} else {
  process.stdout.write(resolved);
}
' "${WEB_DIR}"
}

step "Preparing output directory '${OUTPUT_DIRECTORY}'"
rm -rf "${OUTPUT_DIRECTORY}"
mkdir -p "${OUTPUT_DIRECTORY}"

if [[ "${SKIP_ANGULAR_BUILD}" -eq 0 ]]; then
    step "Building the Angular production bundle"
    [[ -d "${WEB_DIR}/node_modules" ]] || (cd "${WEB_DIR}" && npm ci)
    (cd "${WEB_DIR}" && npm run build)
fi

ANGULAR_OUTPUT="$(resolve_angular_output)"
assert_path "${ANGULAR_OUTPUT}" "Angular browser output"
assert_path "${ANGULAR_OUTPUT}/index.html" "Angular index.html"
echo "    Angular output resolved to ${ANGULAR_OUTPUT}"

step "Publishing the ASP.NET Core API"
dotnet publish "${API_PROJECT}" --configuration "${CONFIGURATION}" --output "${APP_DIR}"

step "Copying the Angular bundle into wwwroot"
rm -rf "${APP_DIR}/wwwroot"
mkdir -p "${APP_DIR}/wwwroot"
cp -R "${ANGULAR_OUTPUT}/." "${APP_DIR}/wwwroot/"
assert_path "${APP_DIR}/wwwroot/index.html" "index.html in wwwroot"

TRIGGERED_DIR="${APP_DIR}/App_Data/jobs/triggered/${TRIGGERED_JOB_NAME}"
step "Publishing the triggered WebJob to App_Data/jobs/triggered/${TRIGGERED_JOB_NAME}"
dotnet publish "${SCHEDULED_PROJECT}" --configuration "${CONFIGURATION}" --output "${TRIGGERED_DIR}"
assert_path "${TRIGGERED_DIR}/run.cmd" "run.cmd for the triggered WebJob"
assert_path "${TRIGGERED_DIR}/settings.job" "settings.job for the triggered WebJob"

CONTINUOUS_DIR="${APP_DIR}/App_Data/jobs/continuous/${CONTINUOUS_JOB_NAME}"
step "Publishing the continuous WebJob to App_Data/jobs/continuous/${CONTINUOUS_JOB_NAME}"
dotnet publish "${SERVICEBUS_PROJECT}" --configuration "${CONFIGURATION}" --output "${CONTINUOUS_DIR}"
assert_path "${CONTINUOUS_DIR}/run.cmd" "run.cmd for the continuous WebJob"
assert_path "${CONTINUOUS_DIR}/settings.job" "settings.job for the continuous WebJob"

if [[ "${SKIP_MIGRATION_BUNDLE}" -eq 0 ]]; then
    step "Creating the EF Core migration bundle"
    mkdir -p "${MIGRATIONS_DIR}"
    (cd "${REPO_ROOT}" && dotnet tool restore)
    (cd "${REPO_ROOT}" && dotnet tool run dotnet-ef migrations bundle \
        --project "${INFRA_PROJECT}" \
        --startup-project "${API_PROJECT}" \
        --configuration "${CONFIGURATION}" \
        --self-contained \
        --target-runtime win-x64 \
        --output "${MIGRATIONS_DIR}/efbundle.exe" \
        --force)
    assert_path "${MIGRATIONS_DIR}/efbundle.exe" "EF Core migration bundle"
fi

step "Creating the deployment package '${ZIP_PATH}'"
rm -f "${ZIP_PATH}"
(cd "${APP_DIR}" && zip -q -r "${ZIP_PATH}" .)
assert_path "${ZIP_PATH}" "deployment ZIP"

echo "Package created: ${ZIP_PATH}"
