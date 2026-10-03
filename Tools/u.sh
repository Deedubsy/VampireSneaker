#!/bin/bash
# Helper: run Unity CLI command against this project. Usage: ./u.sh <command> [args...]
cd "$(dirname "$0")/.."
exec "/mnt/c/Program Files/Unity Hub/resources/cli/unity.exe" command --no-banner "$@"
