#!/usr/bin/env bash

if [[ -n "${RIDECLUB_SHARED_FUNCTIONS_LOADED:-}" ]]; then
  return 0 2>/dev/null || exit 0
fi

RIDECLUB_SHARED_FUNCTIONS_LOADED=1
RIDECLUB_SCRIPT_DIRECTORY="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
RIDECLUB_DEFAULT_ASCII_ART_FILE="${RIDECLUB_DEFAULT_ASCII_ART_FILE:-$RIDECLUB_SCRIPT_DIRECTORY/ascii-header.txt}"

if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  readonly COLOR_RESET=$'\033[0m'
  readonly COLOR_BLUE=$'\033[1;34m'
  readonly COLOR_GREEN=$'\033[1;32m'
  readonly COLOR_YELLOW=$'\033[1;33m'
  readonly COLOR_RED=$'\033[1;31m'
  readonly COLOR_WHITE=$'\033[1;37m'
else
  readonly COLOR_RESET=''
  readonly COLOR_BLUE=''
  readonly COLOR_GREEN=''
  readonly COLOR_YELLOW=''
  readonly COLOR_RED=''
  readonly COLOR_WHITE=''
fi

print_highlight() {
  local color="$1"
  local label="$2"
  shift 2

  printf '%s%s%s %s\n' "$color" "$label" "$COLOR_RESET" "$*"
}

print_info() {
  print_highlight "$COLOR_BLUE" "INFO" "$@"
}

print_success() {
  print_highlight "$COLOR_GREEN" "OK" "$@"
}

print_warning() {
  print_highlight "$COLOR_YELLOW" "WARN" "$@"
}

print_error() {
  print_highlight "$COLOR_RED" "ERROR" "$@" >&2
}

print_header() {
  printf '\n%s%s%s\n' "$COLOR_WHITE" "$*" "$COLOR_RESET"
}

print_ascii_header() {
  local art_file="${1:-$RIDECLUB_DEFAULT_ASCII_ART_FILE}"
  local title="${2:-}"

  if [[ ! -f "$art_file" && "$art_file" != "$RIDECLUB_DEFAULT_ASCII_ART_FILE" ]]; then
    print_warning "ASCII art file not found: $art_file; using default"
    art_file="$RIDECLUB_DEFAULT_ASCII_ART_FILE"
  fi

  require_file "$art_file"

  printf '\n'
  if [[ -n "$title" ]]; then
    printf '%s%s%s\n' "$COLOR_WHITE" "$title" "$COLOR_RESET"
  fi

  printf '%s' "$COLOR_GREEN"
  while IFS= read -r line || [[ -n "$line" ]]; do
    printf '%s\n' "$line"
  done < "$art_file"
  printf '%s' "$COLOR_RESET"
}

die() {
  print_error "$@"
  exit 1
}

is_dry_run() {
  [[ "${RIDECLUB_DRY_RUN:-}" == "1" || "${RIDECLUB_DRY_RUN:-}" == "true" ]]
}

require_command() {
  local command_name="$1"

  if is_dry_run; then
    return 0
  fi

  command -v "$command_name" >/dev/null 2>&1 || die "Required command not found: $command_name"
}

require_file() {
  local path="$1"

  [[ -f "$path" ]] || die "Required file not found: $path"
}

shell_join() {
  local arg

  for arg in "$@"; do
    printf '%s ' "$arg"
  done
  printf '\n'
}

run_command() {
  print_info "$(shell_join "$@")"

  if is_dry_run; then
    return 0
  fi

  "$@"
}

remove_path() {
  local path="$1"

  if [[ ! -e "$path" ]]; then
    print_info "Nothing to remove at $path"
    return 0
  fi

  run_command rm -rf "$path"
}

script_dir() {
  local source_path="${BASH_SOURCE[1]}"

  cd "$(dirname "$source_path")" >/dev/null 2>&1 && pwd
}
