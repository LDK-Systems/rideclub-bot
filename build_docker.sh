#!/usr/bin/env bash
set -euo pipefail

CURRENT_SCRIPT_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
PROJECT_ROOT="$CURRENT_SCRIPT_DIRECTORY"

# shellcheck source=shared_functions.sh
source "$PROJECT_ROOT/shared_functions.sh"

BUILDER_NAME="${RIDECLUB_BUILDX_BUILDER:-rideclub-bot-builder}"
IMAGE_NAME="${RIDECLUB_IMAGE_NAME:-rideclub-bot:local}"
CACHE_DIR="${RIDECLUB_BUILDX_CACHE_DIR:-$PROJECT_ROOT/.docker-cache/buildx}"
NEXT_CACHE_DIR="${CACHE_DIR}-next"
PLATFORM="${RIDECLUB_BUILDX_PLATFORM:-}"
clean_cache=false
clean_builder=false
docker_build_args=()
build_args=()

show_usage() {
  cat <<USAGE
Usage: $(basename "$0") [options] [--] [docker buildx build args...]

Builds the Ride Club bot container image with Docker Buildx and local cache.

Options:
  --clean-cache    Remove the on-disk build cache before building.
  --clean-builder  Remove the Buildx builder and exit.
  --builder NAME   Override the Buildx builder name. Default: $BUILDER_NAME
  --image NAME     Override the image tag. Default: $IMAGE_NAME
  --platform LIST  Build for a specific platform list.
  --build-arg ARG  Pass a build argument to Docker. Repeatable.
  -h, --help       Show this help text.

Any arguments after -- are passed to docker buildx build.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --clean-cache)
      clean_cache=true
      shift
      ;;
    --clean-builder)
      clean_builder=true
      shift
      ;;
    --builder)
      [[ $# -ge 2 ]] || die "--builder requires a value"
      BUILDER_NAME="$2"
      shift 2
      ;;
    --image)
      [[ $# -ge 2 ]] || die "--image requires a value"
      IMAGE_NAME="$2"
      shift 2
      ;;
    --platform)
      [[ $# -ge 2 ]] || die "--platform requires a value"
      PLATFORM="$2"
      shift 2
      ;;
    --build-arg)
      [[ $# -ge 2 ]] || die "--build-arg requires a value"
      docker_build_args+=("$2")
      shift 2
      ;;
    -h|--help)
      show_usage
      exit 0
      ;;
    --)
      shift
      build_args+=("$@")
      break
      ;;
    -*)
      die "Unknown option: $1"
      ;;
    *)
      build_args+=("$1")
      shift
      ;;
  esac
done

require_command docker

if [[ "$clean_cache" == "true" ]]; then
  print_header "Cleaning Buildx cache"
  remove_path "$CACHE_DIR"
  remove_path "$NEXT_CACHE_DIR"
fi

if [[ "$clean_builder" == "true" ]]; then
  print_header "Removing Buildx builder"
  if is_dry_run || docker buildx inspect "$BUILDER_NAME" >/dev/null 2>&1; then
    run_command docker buildx rm "$BUILDER_NAME"
  else
    print_info "Buildx builder does not exist: $BUILDER_NAME"
  fi
  exit 0
fi

if ! is_dry_run && ! docker buildx inspect "$BUILDER_NAME" >/dev/null 2>&1; then
  print_header "Creating Buildx builder"
  run_command docker buildx create --name "$BUILDER_NAME" --driver docker-container --use
elif ! is_dry_run; then
  run_command docker buildx use "$BUILDER_NAME"
fi

if ! is_dry_run; then
  mkdir -p "$CACHE_DIR"
  remove_path "$NEXT_CACHE_DIR"
fi

build_command=(
  docker buildx build
  --builder "$BUILDER_NAME"
  --file "$PROJECT_ROOT/Dockerfile"
  --tag "$IMAGE_NAME"
  --cache-to "type=local,dest=$NEXT_CACHE_DIR,mode=max"
  --load
)

if is_dry_run || [[ -f "$CACHE_DIR/index.json" ]]; then
  build_command+=(--cache-from "type=local,src=$CACHE_DIR")
else
  print_info "No existing Buildx cache found at $CACHE_DIR; starting with a cold cache"
fi

if [[ -n "$PLATFORM" ]]; then
  build_command+=(--platform "$PLATFORM")
fi

for docker_build_arg in "${docker_build_args[@]}"; do
  build_command+=(--build-arg "$docker_build_arg")
done

build_command+=("${build_args[@]}")
build_command+=("$PROJECT_ROOT")

print_header "Building Ride Club image"
run_command "${build_command[@]}"

if ! is_dry_run && [[ -d "$NEXT_CACHE_DIR" ]]; then
  remove_path "$CACHE_DIR"
  run_command mv "$NEXT_CACHE_DIR" "$CACHE_DIR"
fi

print_success "Built $IMAGE_NAME"
