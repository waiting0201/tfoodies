#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# EF Core Database-First scaffold against the FROZEN `tfoodies` schema.
#
# Re-running this against the live/snapshot DB and diffing the output is our
# regression guard that the schema has not drifted (plan §2). The generated
# entities keep the ugly DB names verbatim (Refound, quantity_left, …); clean
# domain names are introduced only in the Mappers layer — never edit the output
# by hand.
#
# Usage:
#   TFOODIES_CONNSTRING='Server=...;Database=tfoodies;User Id=...;Password=...;TrustServerCertificate=True' \
#     ./scripts/scaffold-db.sh
#
# Prereqs: `dotnet tool install --global dotnet-ef` (or it is restored locally).
# -----------------------------------------------------------------------------
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INFRA="$ROOT/src/TFoodies.Infrastructure"
OUT_DIR="Persistence/Scaffolded/Entities"
CTX_DIR="Persistence/Scaffolded"

if [[ -z "${TFOODIES_CONNSTRING:-}" ]]; then
  echo "ERROR: set TFOODIES_CONNSTRING to a (read-only / snapshot) connection string." >&2
  echo "       Do NOT commit the connection string; it comes from Key Vault / env." >&2
  exit 1
fi

if ! dotnet ef --version >/dev/null 2>&1; then
  echo "Installing dotnet-ef..." >&2
  dotnet tool install --global dotnet-ef >/dev/null
fi

echo "Scaffolding TfoodiesContext from the frozen schema..."
dotnet ef dbcontext scaffold "$TFOODIES_CONNSTRING" Microsoft.EntityFrameworkCore.SqlServer \
  --project   "$INFRA" \
  --context   TfoodiesContext \
  --context-dir "$CTX_DIR" \
  --output-dir  "$OUT_DIR" \
  --namespace   TFoodies.Infrastructure.Persistence.Scaffolded \
  --context-namespace TFoodies.Infrastructure.Persistence \
  --data-annotations \
  --no-onconfiguring \
  --use-database-names \
  --force

echo "Done. Review the diff under $INFRA/$CTX_DIR — an empty diff means no schema drift."
