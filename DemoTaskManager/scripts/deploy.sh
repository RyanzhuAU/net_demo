#!/usr/bin/env bash
# Deploys the Bicep infrastructure and/or the application package to Azure App Service.
# Requires an authenticated Azure CLI session (az login).
# Secrets are read from options or environment variables and are never echoed.
set -Eeuo pipefail

RESOURCE_GROUP_NAME=""
LOCATION="westeurope"
ENVIRONMENT_NAME="dev"
APPLICATION_NAME="demotask"
SUBSCRIPTION_ID=""
SQL_ADMIN_LOGIN="${SQL_ADMIN_LOGIN:-}"
PACKAGE_PATH="artifacts/DemoTaskManager.zip"
APP_SERVICE_NAME=""
SKIP_INFRASTRUCTURE=0
SKIP_APPLICATION=0

usage() {
    cat <<'EOF'
Usage: deploy.sh --resource-group NAME [options]

Options:
  --resource-group NAME       Target resource group (required)
  --location REGION           Azure region (default: westeurope)
  --environment dev|test|prod Environment discriminator (default: dev)
  --application-name NAME     Short application name (default: demotask)
  --subscription-id ID        Subscription to select before deploying
  --sql-admin-login NAME      SQL administrator login (or SQL_ADMIN_LOGIN env var)
  --package PATH              Deployment ZIP (default: artifacts/DemoTaskManager.zip)
  --app-service-name NAME     Required when --skip-infrastructure is used
  --skip-infrastructure       Only deploy the application package
  --skip-application          Only deploy infrastructure

The SQL administrator password must be supplied through the SQL_ADMIN_PASSWORD
environment variable so it never appears in shell history.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --resource-group) RESOURCE_GROUP_NAME="$2"; shift 2 ;;
        --location) LOCATION="$2"; shift 2 ;;
        --environment) ENVIRONMENT_NAME="$2"; shift 2 ;;
        --application-name) APPLICATION_NAME="$2"; shift 2 ;;
        --subscription-id) SUBSCRIPTION_ID="$2"; shift 2 ;;
        --sql-admin-login) SQL_ADMIN_LOGIN="$2"; shift 2 ;;
        --package) PACKAGE_PATH="$2"; shift 2 ;;
        --app-service-name) APP_SERVICE_NAME="$2"; shift 2 ;;
        --skip-infrastructure) SKIP_INFRASTRUCTURE=1; shift ;;
        --skip-application) SKIP_APPLICATION=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage; exit 1 ;;
    esac
done

[[ -n "${RESOURCE_GROUP_NAME}" ]] || { echo "--resource-group is required" >&2; exit 1; }
command -v az >/dev/null 2>&1 || { echo "The Azure CLI (az) is required." >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
TEMPLATE_FILE="${REPO_ROOT}/infrastructure/main.bicep"
[[ "${PACKAGE_PATH}" = /* ]] || PACKAGE_PATH="${REPO_ROOT}/${PACKAGE_PATH}"

step() { printf '==> %s\n' "$1"; }

if [[ -n "${SUBSCRIPTION_ID}" ]]; then
    step "Selecting subscription"
    az account set --subscription "${SUBSCRIPTION_ID}"
fi

if [[ "${SKIP_INFRASTRUCTURE}" -eq 0 ]]; then
    [[ -f "${TEMPLATE_FILE}" ]] || { echo "Bicep template not found at ${TEMPLATE_FILE}" >&2; exit 1; }
    [[ -n "${SQL_ADMIN_LOGIN}" ]] || { echo "Provide --sql-admin-login or SQL_ADMIN_LOGIN." >&2; exit 1; }
    [[ -n "${SQL_ADMIN_PASSWORD:-}" ]] || { echo "Set the SQL_ADMIN_PASSWORD environment variable." >&2; exit 1; }

    step "Ensuring resource group '${RESOURCE_GROUP_NAME}' exists"
    az group create --name "${RESOURCE_GROUP_NAME}" --location "${LOCATION}" --output none

    DEPLOYMENT_NAME="demotaskmanager-${ENVIRONMENT_NAME}-$(date -u +%Y%m%d%H%M%S)"

    step "Validating the Bicep deployment"
    az deployment group validate \
        --resource-group "${RESOURCE_GROUP_NAME}" \
        --template-file "${TEMPLATE_FILE}" \
        --parameters \
            "applicationName=${APPLICATION_NAME}" \
            "environmentName=${ENVIRONMENT_NAME}" \
            "location=${LOCATION}" \
            "sqlAdministratorLogin=${SQL_ADMIN_LOGIN}" \
            "sqlAdministratorPassword=${SQL_ADMIN_PASSWORD}" \
        --output none

    step "Deploying infrastructure"
    az deployment group create \
        --resource-group "${RESOURCE_GROUP_NAME}" \
        --name "${DEPLOYMENT_NAME}" \
        --template-file "${TEMPLATE_FILE}" \
        --parameters \
            "applicationName=${APPLICATION_NAME}" \
            "environmentName=${ENVIRONMENT_NAME}" \
            "location=${LOCATION}" \
            "sqlAdministratorLogin=${SQL_ADMIN_LOGIN}" \
            "sqlAdministratorPassword=${SQL_ADMIN_PASSWORD}" \
        --output none

    APP_SERVICE_NAME="$(az deployment group show \
        --resource-group "${RESOURCE_GROUP_NAME}" \
        --name "${DEPLOYMENT_NAME}" \
        --query properties.outputs.appServiceName.value -o tsv)"

    echo "    App Service: ${APP_SERVICE_NAME}"
fi

if [[ "${SKIP_APPLICATION}" -eq 0 ]]; then
    [[ -n "${APP_SERVICE_NAME}" ]] || { echo "Provide --app-service-name when skipping infrastructure." >&2; exit 1; }
    [[ -f "${PACKAGE_PATH}" ]] || { echo "Deployment package not found at ${PACKAGE_PATH}. Run scripts/publish.sh first." >&2; exit 1; }

    step "Deploying $(basename "${PACKAGE_PATH}") to App Service '${APP_SERVICE_NAME}'"
    az webapp deploy \
        --resource-group "${RESOURCE_GROUP_NAME}" \
        --name "${APP_SERVICE_NAME}" \
        --src-path "${PACKAGE_PATH}" \
        --type zip \
        --output none

    HEALTH_URL="https://${APP_SERVICE_NAME}.azurewebsites.net/health"
    step "Smoke testing ${HEALTH_URL}"
    for attempt in 1 2 3 4 5 6; do
        sleep 10
        if curl --fail --silent --show-error --max-time 30 "${HEALTH_URL}" >/dev/null; then
            echo "Health check passed."
            break
        fi
        echo "    attempt ${attempt} did not succeed yet"
        [[ "${attempt}" -lt 6 ]] || { echo "Health check did not succeed." >&2; exit 1; }
    done
fi

echo "Deployment completed successfully."
