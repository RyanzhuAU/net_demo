#!/usr/bin/env bash
# Restores dependencies, then builds, lints and tests the Angular frontend and the .NET solution.
set -Eeuo pipefail

CONFIGURATION="Release"
SKIP_TESTS=0
SKIP_FRONTEND=0

usage() {
    cat <<'EOF'
Usage: build.sh [--configuration Debug|Release] [--skip-tests] [--skip-frontend]
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --configuration) CONFIGURATION="$2"; shift 2 ;;
        --skip-tests) SKIP_TESTS=1; shift ;;
        --skip-frontend) SKIP_FRONTEND=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage; exit 1 ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WEB_DIR="${REPO_ROOT}/src/DemoTaskManager.Web"
SOLUTION="${REPO_ROOT}/DemoTaskManager.sln"

step() { printf '==> %s\n' "$1"; }

[[ -f "${SOLUTION}" ]] || { echo "Solution not found at ${SOLUTION}" >&2; exit 1; }

if [[ "${SKIP_FRONTEND}" -eq 0 ]]; then
    [[ -f "${WEB_DIR}/package.json" ]] || { echo "package.json not found in ${WEB_DIR}" >&2; exit 1; }

    step "Restoring frontend dependencies (npm ci)"
    (cd "${WEB_DIR}" && npm ci)

    step "Linting the Angular application"
    (cd "${WEB_DIR}" && npm run lint)

    if [[ "${SKIP_TESTS}" -eq 0 ]]; then
        step "Running Angular unit tests"
        (cd "${WEB_DIR}" && npm run test:ci)
    fi

    step "Building the Angular production bundle"
    (cd "${WEB_DIR}" && npm run build)
fi

step "Restoring .NET dependencies"
dotnet restore "${SOLUTION}"

step "Building the .NET solution (${CONFIGURATION})"
dotnet build "${SOLUTION}" --configuration "${CONFIGURATION}" --no-restore

if [[ "${SKIP_TESTS}" -eq 0 ]]; then
    step "Running .NET tests"
    dotnet test "${SOLUTION}" --configuration "${CONFIGURATION}" --no-build --verbosity minimal
fi

echo "Build completed successfully."
