#!/usr/bin/env bash
# Regenera el DbContext y las entidades desde la base (Database First).
# Solo tablas de negocio: Identity y OpenIddict usan sus propias entidades.
# Uso: ConnectionStrings__Migrations="Host=...;..." database/scaffold.sh
set -euo pipefail
cd "$(dirname "$0")/.."

: "${ConnectionStrings__Migrations:?Falta la variable ConnectionStrings__Migrations}"

tables=(
  suppliers products requisitions requisition_lines approvals
  purchase_orders purchase_order_lines supplier_notifications
  outbox_messages audit_events
)

args=()
for t in "${tables[@]}"; do args+=(--table "$t"); done

dotnet tool restore >/dev/null
# dotnet ef compila sin restaurar paquetes: en una máquina limpia hay que hacerlo antes.
dotnet restore src/Requi.Infrastructure >/dev/null
dotnet ef dbcontext scaffold "$ConnectionStrings__Migrations" Npgsql.EntityFrameworkCore.PostgreSQL \
  --project src/Requi.Infrastructure \
  --context RequiDbContext \
  --context-dir Persistence \
  --context-namespace Requi.Infrastructure.Persistence \
  --output-dir ../Requi.Domain/Entities \
  --namespace Requi.Domain.Entities \
  --no-onconfiguring \
  --force \
  "${args[@]}"
