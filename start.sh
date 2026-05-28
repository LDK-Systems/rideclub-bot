#!/usr/bin/env bash
set -euo pipefail

CURRENT_SCRIPT_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
PROJECT_ROOT="$CURRENT_SCRIPT_DIRECTORY"

# shellcheck source=shared_functions.sh
source "$PROJECT_ROOT/shared_functions.sh"

show_usage() {
  cat <<USAGE
Usage: $(basename "$0") [options] [--] [docker compose up args...]

Starts the Ride Club bot containers with Docker Compose.

Options:
  --telemetry   Include docker-compose.telemetry.yml.
  --build       Build images before starting containers.
  -d, --detach  Run containers in the background.
  -h, --help    Show this help text.

Any arguments after -- are passed to docker compose up.
USAGE
}

telemetry=false
build=false
detach=false
passthrough_args=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --telemetry)
      telemetry=true
      shift
      ;;
    --build)
      build=true
      shift
      ;;
    -d|--detach)
      detach=true
      shift
      ;;
    -h|--help)
      show_usage
      exit 0
      ;;
    --)
      shift
      passthrough_args+=("$@")
      break
      ;;
    -*)
      die "Unknown option: $1"
      ;;
    *)
      passthrough_args+=("$1")
      shift
      ;;
  esac
done

compose_files=("$PROJECT_ROOT/docker-compose.yml")

if [[ "$telemetry" == "true" ]]; then
  compose_files+=("$PROJECT_ROOT/docker-compose.telemetry.yml")
fi

compose_args=()
for compose_file in "${compose_files[@]}"; do
  require_file "$compose_file"
  compose_args+=("-f" "$compose_file")
done

up_args=("up")

if [[ "$build" == "true" ]]; then
  up_args+=("--build")
fi

if [[ "$detach" == "true" ]]; then
  up_args+=("--detach")
fi

up_args+=("${passthrough_args[@]}")

require_command docker

print_header "Starting Ride Club containers"
run_command docker compose "${compose_args[@]}" "${up_args[@]}"
