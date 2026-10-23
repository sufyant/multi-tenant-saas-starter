#!/usr/bin/env bash
# Writes the schema that the migration step creates on an empty database, as `pg_dump --schema-only` prints it: tables and
# their columns, constraints, indexes, row level security, policies, functions, owners and grants. A squash of the migrations
# compares the dump of the old chain with the dump of the new one: the two must be identical (step r11a). The dump holds no
# rows, so the migration history differs in no line. Needs Docker and the .NET SDK of global.json.
#
#   apps/api/scripts/dump-schema.sh <output file>
set -euo pipefail

output="${1:?Give the output file}"
api_dir="$(cd "$(dirname "$0")/.." && pwd)"
image="$(cat "$api_dir/db/postgres-image")"
password="dump-password"

container="$(docker run -d --rm -e POSTGRES_PASSWORD="$password" -e POSTGRES_DB=api -p 127.0.0.1::5432 "$image")"
trap 'docker rm -f "$container" >/dev/null' EXIT
port="$(docker port "$container" 5432/tcp | head -n 1 | cut -d: -f2)"
until docker exec "$container" pg_isready -U postgres -d api -h 127.0.0.1 >/dev/null 2>&1; do sleep 1; done

# The database is set up the way a deployment is: the bootstrap script, then the migration step as the owner role.
docker exec -i "$container" psql -q -U postgres -d api -v owner_password="$password" -v application_password="$password" \
  <"$api_dir/db/bootstrap.sql"
ConnectionStrings__Migrations="Host=127.0.0.1;Port=$port;Database=api;Username=api_owner;Password=$password" \
  dotnet run --project "$api_dir/src/Api" -- migrate

# pg_dump 18 opens and closes its script with \restrict and a key that is new on each run; the key is no part of the schema.
docker exec "$container" pg_dump -U postgres -d api --schema-only | grep -v '^\\\(un\)\?restrict ' >"$output"
