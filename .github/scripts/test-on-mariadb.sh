#!/usr/bin/env bash
# Usage: test-on-mariadb.sh <port>. Gives the MariaDB server on 127.0.0.1:<port> fresh test schemas,
# then runs the built test suite against it.
set -euo pipefail
port="$1"
db() { mariadb --protocol=tcp --host=127.0.0.1 --port="$port" --user=root --skip-ssl "$@"; }
# The server keeps its data in memory, so every run starts and ends without test schemas.
drop_schemas() {
  for schema in $(db -N -e "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('mysql', 'information_schema', 'performance_schema', 'sys')"); do
    db -e "DROP DATABASE \`$schema\`"
  done
}
drop_schemas
trap drop_schemas EXIT
db -e "SET GLOBAL max_connections = 1000"
server="Server=127.0.0.1;Port=$port;User ID=root;Pooling=false;Allow User Variables=true"
imports=()
# Tests that name a disposable schema get one; the other tests create their own schemas on the server.
for variable in $(grep -rhoE '"[A-Z][A-Z0-9_]+(_DATABASE|_CONNECTION_STRING|_TEST_DB)"' Plus.Tests | tr -d '"' | sort -u); do
  case "$variable" in
    PLUS_ACL_TEST_CONNECTION_STRING) schema=task_acl_tests_ci ;;
    PLUS_CLUB_TEST_CONNECTION_STRING) schema=task_acl_tests_club ;;
    PLUS_CAMERA_TEST_CONNECTION_STRING) schema=task_camera_tests_ci ;;
    PLUS_HABBICONS_TEST_CONNECTION_STRING) schema=task_habicons_tests_ci ;;
    PLUS_HOUSEKEEPING_TEST_CONNECTION_STRING) schema=task_housekeeping_tests_ci ;;
    PLUS_REFACTOR_TEST_CONNECTION_STRING) schema=task_refactor_tests_ci ;;
    PLUS_MARKETPLACE_PROBE_CONNECTION_STRING) schema=task_refactor_tests_marketplace_ci ;;
    PLUS_RCON_GRANT_TEST_CONNECTION_STRING) schema=task_rcon_grants_tests_ci ;;
    PLUS_WIRED_RESET_TEST_DB) schema=task_wired_reset_ci ;;
    PLUS_WIRED_CATALOG_TEST_CONNECTION_STRING) schema=task_wired_catalog_tests_ci ;;
    *) schema=information_schema ;;
  esac
  if [ "$schema" != information_schema ]; then
    db -e "CREATE DATABASE $schema CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci"
    # The Habbicon and Wired catalog tests import their own schema.
    case "$variable" in
      PLUS_HABBICONS_TEST_CONNECTION_STRING | PLUS_WIRED_CATALOG_TEST_CONNECTION_STRING) ;;
      *) db "$schema" < Database/FreshInstall.sql & imports+=($!) ;;
    esac
  fi
  options=""
  # The Wired room settings factory test opens the database with the emulator's own options.
  [ "$variable" = PLUS_HOUSEKEEPING_TEST_CONNECTION_STRING ] && options=";Allow Zero DateTime=true;Convert Zero DateTime=true"
  export "$variable=$server;Database=$schema$options"
done
for import in "${imports[@]}"; do wait "$import"; done
dotnet test Plus.Tests --configuration Release --no-build
