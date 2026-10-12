#!/usr/bin/env bash
# Runs the API image `api` against PostgreSQL in Docker: bootstrap.sql as a provider role (twice,
# then once more with a changed role, which must fail), the migration with the owner role only, and
# the web and the worker with every required setting. Both must answer /health/ready on their
# management port, 8081 in the container. The web's public port, 8080, must answer 404 for it.
# The passwords are made for this run and printed nowhere. They protect nothing outside it.
#
# Inputs: POSTGRES_IMAGE (the PostgreSQL image), RUNNER_TEMP (a directory for temporary files).
# Exit codes: 0 when both processes are ready and the public port hides readiness; non-zero at the
# first failed command.
# Leaves the containers api-db, api-web, api-worker and the network api-image; the workflow prints
# their logs on failure and removes them.
#
# Run it locally from any folder, with Docker:
#   docker build -t api apps/api
#   POSTGRES_IMAGE=postgres:18 RUNNER_TEMP="$(mktemp -d)" scripts/ci/run-api-image.sh
#   docker rm -f api-db api-web api-worker; docker network rm api-image
set -euo pipefail

: "${POSTGRES_IMAGE:?}" "${RUNNER_TEMP:?}"
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

superuser_password=$(openssl rand -hex 24)
provider_password=$(openssl rand -hex 24)
owner_password=$(openssl rand -hex 24)
application_password=$(openssl rand -hex 24)

docker network create api-image
docker run -d --name api-db --network api-image \
  -e POSTGRES_PASSWORD="$superuser_password" -e POSTGRES_DB=app "$POSTGRES_IMAGE"

# Over TCP: while the image sets up the database, the server listens only on its socket.
for _ in $(seq 30); do
  docker exec api-db pg_isready -h 127.0.0.1 -U postgres -d app && break
  sleep 1
done
docker exec api-db pg_isready -h 127.0.0.1 -U postgres -d app

# A managed provider gives no superuser. Its most privileged role has CREATEROLE, CREATEDB and
# BYPASSRLS and owns the database, like neondb_owner on Neon (apps/api/docs/setup.md).
docker exec api-db psql -U postgres -d app -v ON_ERROR_STOP=1 \
  -c "CREATE ROLE provider_admin LOGIN CREATEROLE CREATEDB BYPASSRLS PASSWORD '$provider_password'" \
  -c "ALTER DATABASE app OWNER TO provider_admin"

# The two roles, as in apps/api/docs/local-run.md section 2, as the provider's role over the network.
bootstrap() {
  docker run --rm -i --network api-image -e PGPASSWORD="$provider_password" "$POSTGRES_IMAGE" \
    psql -h api-db -U provider_admin -d app \
    -v owner_password="$1" -v application_password="$2" -f - < apps/api/db/bootstrap.sql
}
roles() {
  docker exec api-db psql -U postgres -d app -At \
    -c "SELECT * FROM pg_roles WHERE rolname IN ('api_owner', 'api_application') ORDER BY rolname"
}

# A rerun changes only the passwords: the steps below log in with the second run's passwords.
bootstrap "$(openssl rand -hex 24)" "$(openssl rand -hex 24)"
roles > "$RUNNER_TEMP/roles-first.txt"
bootstrap "$owner_password" "$application_password"
roles > "$RUNNER_TEMP/roles-second.txt"
cat "$RUNNER_TEMP/roles-second.txt"
diff "$RUNNER_TEMP/roles-first.txt" "$RUNNER_TEMP/roles-second.txt"

# A role that someone changed by hand stops a rerun, which names the role and the attribute.
docker exec api-db psql -U postgres -d app -c "ALTER ROLE api_application BYPASSRLS"
if bootstrap "$owner_password" "$application_password" 2> "$RUNNER_TEMP/changed-role.txt"; then
  echo "The script ran on although api_application can bypass RLS." >&2
  exit 1
fi
cat "$RUNNER_TEMP/changed-role.txt"
grep -q "api_application has BYPASSRLS" "$RUNNER_TEMP/changed-role.txt"
docker exec api-db psql -U postgres -d app -c "ALTER ROLE api_application NOBYPASSRLS"

# Only the owner role's connection (R3).
docker run --rm --network api-image \
  -e ConnectionStrings__Migrations="Host=api-db;Port=5432;Database=app;Username=api_owner;Password=$owner_password" \
  api migrate

# Every required setting (apps/api/docs/configuration.md). The fake values reach no real service.
{
  echo "ASPNETCORE_ENVIRONMENT=Production"
  echo "ConnectionStrings__Database=Host=api-db;Port=5432;Database=app;Username=api_application;Password=$application_password"
  echo "Authentication__Clerk__Issuer=https://clerk.example.invalid"
  echo "Authentication__Clerk__AuthorizedParties__0=https://app.example.invalid"
  echo "ControlPlane__FirstSystemAdminEmail=admin@example.invalid"
  echo "ControlPlane__Clerk__SecretKey=sk_test_fake_not_a_secret"
  echo "ControlPlane__Clerk__BackendApiUrl=https://clerk.example.invalid/v1/"
  echo "ControlPlane__Invitations__AcceptUrl=https://app.example.invalid/invitations/accept"
  echo "Notifications__Resend__ApiKey=re_fake_not_a_secret"
  echo "Notifications__Resend__From=App <no-reply@example.invalid>"
  echo "Notifications__Resend__ApiUrl=https://resend.example.invalid/"
} > "$RUNNER_TEMP/api.env"

# On this machine: the web's public port on 8080 and its management port on 8081; the worker opens
# only its management port, here on 8082.
docker run -d --name api-web --network api-image -p 8080:8080 -p 8081:8081 \
  --env-file "$RUNNER_TEMP/api.env" -e Host__Role=web api
docker run -d --name api-worker --network api-image -p 8082:8081 \
  --env-file "$RUNNER_TEMP/api.env" -e Host__Role=worker api

# The health endpoints answer on the management port of each process (apps/api/docs/ARCHITECTURE.md
# section 8).
for port in 8081 8082; do
  curl --silent --show-error --fail --max-time 5 \
    --retry 60 --retry-delay 1 --retry-all-errors --retry-max-time 60 \
    "http://localhost:$port/health/ready"
  echo " on management port $port"
done

# The public port does not answer them.
status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' --max-time 5 \
  "http://localhost:8080/health/ready")
echo "GET /health/ready on public port 8080: $status"
if [[ "$status" != "404" ]]; then
  echo "The public port answered $status for /health/ready, not 404." >&2
  exit 1
fi
