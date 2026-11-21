#!/usr/bin/env bash
# Levanta todo para el e2e y se queda en primer plano (Playwright lo usa como webServer).
# Requiere PostgreSQL en localhost:5432 (docker compose up -d db) y E2E_CLAVE.
set -euo pipefail
cd "$(dirname "$0")/../.."
export PATH="$HOME/.dotnet:$PATH"

: "${E2E_CLAVE:?Falta E2E_CLAVE (contraseña de los usuarios de demo)}"
PGPASS="${REQUI_DB_PASSWORD:-requi_local}"
# Base propia (requi_e2e): así no choca con los usuarios de demo que ya haya en la base local.
DB=requi_e2e
CONN="Host=localhost;Port=5432;Database=${DB};Username=requi;Password=${PGPASS}"

sql() { # sql <base> [args de psql...]; el SQL entra por stdin
  local base="$1"; shift
  if command -v psql >/dev/null; then
    PGPASSWORD="$PGPASS" psql -h localhost -U requi -d "$base" -v ON_ERROR_STOP=1 -q "$@"
  else
    docker compose exec -T db psql -U requi -d "$base" -v ON_ERROR_STOP=1 -q "$@"
  fi
}

echo "== Base $DB"
# Se recrea en cada corrida: la clave de demo cambia y los usuarios ya creados conservarían la anterior.
sql requi -c "DROP DATABASE IF EXISTS ${DB} WITH (FORCE)" </dev/null
sql requi -c "CREATE DATABASE ${DB}" </dev/null

echo "== Migrador y usuarios de demo"
ConnectionStrings__Migrations="$CONN" Demo__Clave="$E2E_CLAVE" \
  Oidc__Redirecciones="http://localhost:4200/callback" \
  Oidc__RedireccionesSalida="http://localhost:4200/" \
  dotnet run --project src/Requi.DbMigrator

echo "== Catálogo de demo"
sql "$DB" < tests/e2e/semilla.sql

pids=()
limpiar() { for p in "${pids[@]:-}"; do [ -n "$p" ] && kill "$p" 2>/dev/null || true; done; }
trap limpiar EXIT INT TERM

echo "== API en :5103 (Outbox activo)"
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5103 \
  ConnectionStrings__Database="$CONN" Limites__LoginPorMinuto=1000 Limites__TokensPorMinuto=1000 \
  dotnet run --project src/Requi.Api --no-launch-profile &
pids+=($!)
for _ in $(seq 1 90); do
  curl -fs http://localhost:5103/health/ready >/dev/null && break
  sleep 2
done
curl -fs http://localhost:5103/health/ready >/dev/null || { echo "La API no arrancó"; exit 1; }

echo "== Angular en :4200"
cd web
[ -d node_modules ] || npm ci
npx ng serve --port 4200 &
pids+=($!)
wait -n
