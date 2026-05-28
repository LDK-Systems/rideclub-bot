#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

fail() {
  printf 'FAIL: %s\n' "$1" >&2
  exit 1
}

assert_contains() {
  local haystack="$1"
  local needle="$2"

  [[ "$haystack" == *"$needle"* ]] || fail "Expected output to contain: $needle"
}

assert_not_contains() {
  local haystack="$1"
  local needle="$2"

  [[ "$haystack" != *"$needle"* ]] || fail "Expected output not to contain: $needle"
}

start_output="$(RIDECLUB_DRY_RUN=1 "$ROOT_DIR/start.sh" --telemetry --build)"
assert_contains "$start_output" "docker compose"
assert_contains "$start_output" "-f $ROOT_DIR/docker-compose.yml"
assert_contains "$start_output" "-f $ROOT_DIR/docker-compose.telemetry.yml"
assert_contains "$start_output" "up --build"

base_start_output="$(RIDECLUB_DRY_RUN=1 "$ROOT_DIR/start.sh")"
assert_contains "$base_start_output" "-f $ROOT_DIR/docker-compose.yml"
assert_not_contains "$base_start_output" "docker-compose.telemetry.yml"
assert_not_contains "$base_start_output" "--build"

build_output="$(RIDECLUB_DRY_RUN=1 "$ROOT_DIR/build_docker.sh")"
assert_contains "$build_output" "docker buildx build"
assert_contains "$build_output" "--cache-from type=local,src=$ROOT_DIR/.docker-cache/buildx"
assert_contains "$build_output" "--cache-to type=local,dest=$ROOT_DIR/.docker-cache/buildx-next,mode=max"
assert_contains "$build_output" "--load"

build_arg_output="$(RIDECLUB_DRY_RUN=1 "$ROOT_DIR/build_docker.sh" --build-arg BUILD_CONFIGURATION=Debug --build-arg VERSION=local)"
assert_contains "$build_arg_output" "--build-arg BUILD_CONFIGURATION=Debug"
assert_contains "$build_arg_output" "--build-arg VERSION=local"

clean_builder_output="$(RIDECLUB_DRY_RUN=1 "$ROOT_DIR/build_docker.sh" --clean-builder)"
assert_contains "$clean_builder_output" "docker buildx rm"
assert_not_contains "$clean_builder_output" "docker buildx build"

dockerfile_contents="$(<"$ROOT_DIR/Dockerfile")"
assert_contains "$dockerfile_contents" "RUN dotnet restore src/LDK.RideClub.Bot/RideClub.Bot.csproj"
assert_not_contains "$dockerfile_contents" "RUN dotnet restore"$'\n'

ascii_art_file="$(mktemp)"
trap 'rm -f "$ascii_art_file"' EXIT
printf 'RIDE\nCLUB\n' > "$ascii_art_file"

ascii_art_output="$(NO_COLOR=1 bash -c "source '$ROOT_DIR/shared_functions.sh'; print_ascii_header '$ascii_art_file'")"
assert_contains "$ascii_art_output" "RIDE"
assert_contains "$ascii_art_output" "CLUB"

default_ascii_art_output="$(NO_COLOR=1 bash -c "source '$ROOT_DIR/shared_functions.sh'; print_ascii_header")"
assert_contains "$default_ascii_art_output" "Ride Club"

printf 'Shell script tests passed.\n'
