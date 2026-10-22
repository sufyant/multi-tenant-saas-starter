#!/usr/bin/env bash
# Runs the API image `api` against PostgreSQL in Docker: bootstrap.sql as a provider role (twice,
# then once more with a changed role, which must fail), the migration with the owner role only, and
# the web and the worker with every required setting. Both must answer the health path on their
# management port, as .railway/api.json names them. The web's public port, 8080, must answer 404
# for it.
# No process may try to load the Kerberos library (libgssapi), which the image does not have.
# The passwords are made for this run and printed nowhere. They protect nothing outside it.
#
# Inputs: RUNNER_TEMP (a directory for temporary files). The PostgreSQL image is the one of
# apps/api/db/postgres-image, which the test fixtures run too.
# Exit codes: 0 when both processes are ready, the public port hides readiness and no output names
# libgssapi; non-zero at the first failed command.
# Leaves the containers api-db, api-web, api-worker and the network api-image; the workflow prints
# their logs on failure and removes them.
#
# Run it locally from any folder, with Docker:
#   docker build -t api apps/api
#   RUNNER_TEMP="$(mktemp -d)" scripts/ci/run-api-image.sh
#   docker rm -f api-db api-web api-worker; docker network rm api-image
set -euo pipefail

: "${RUNNER_TEMP:?}"
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
postgres_image=$(cat apps/api/db/postgres-image)
management_port=$(jq -r '.managementPort' .railway/api.json)
health_path=$(jq -r '.healthPath' .railway/api.json)

superuser_password=$(openssl rand -hex 24)
provider_password=$(openssl rand -hex 24)
owner_password=$(openssl rand -hex 24)
application_password=$(openssl rand -hex 24)

docker network create api-image
docker run -d --name api-db --network api-image \
  -e POSTGRES_PASSWORD="$superuser_password" -e POSTGRES_DB=app "$postgres_image"

# Over TCP: while the image sets up the database, the server listens only on its socket.
for _ in $(seq 30); do
  docker exec api-db pg_isready -h 127.0.0.1 -U postgres -d app && break
  sleep 1
done
docker exec api-db pg_isready -h 127.0.0.1 -U postgres -d app

# A managed PostgreSQL gives no superuser. Its most privileged role has CREATEROLE, CREATEDB and
# BYPASSRLS and owns the database (apps/api/docs/setup.md).
docker exec api-db psql -U postgres -d app -v ON_ERROR_STOP=1 \
  -c "CREATE ROLE provider_admin LOGIN CREATEROLE CREATEDB BYPASSRLS PASSWORD '$provider_password'" \
  -c "ALTER DATABASE app OWNER TO provider_admin"

# The two roles, as in apps/api/docs/local-run.md section 2, as the provider's role over the network.
bootstrap() {
  docker run --rm -i --network api-image -e PGPASSWORD="$provider_password" "$postgres_image" \
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

# Only the owner role's connection (R3). The output is kept for the last check.
docker run --rm --network api-image \
  -e ConnectionStrings__Migrations="Host=api-db;Port=5432;Database=app;Username=api_owner;Password=$owner_password" \
  api migrate 2>&1 | tee "$RUNNER_TEMP/migrate.log"

# Every required setting (apps/api/docs/configuration.md). The fake values reach no real service.
{
  echo "ASPNETCORE_ENVIRONMENT=Production"
  echo "ConnectionStrings__Database=Host=api-db;Port=5432;Database=app;Username=api_application;Password=$application_password"
  echo "Authentication__Clerk__Issuer=https://clerk.example.invalid"
  echo "Authentication__Clerk__AuthorizedParties__0=https://app.example.invalid"
  echo "ControlPlane__SystemAdmins__RootEmails__0=admin@example.invalid"
  echo "ControlPlane__Clerk__SecretKey=sk_test_fake_not_a_secret"
  echo "ControlPlane__Clerk__BackendApiUrl=https://clerk.example.invalid/v1/"
  echo "ControlPlane__Invitations__AcceptUrl=https://app.example.invalid/invitations/accept"
  echo "Notifications__Resend__ApiKey=re_fake_not_a_secret"
  echo "Notifications__Resend__From=App <no-reply@example.invalid>"
  echo "Notifications__Resend__ApiUrl=https://resend.example.invalid/"
} > "$RUNNER_TEMP/api.env"

# On this machine: the web's public port on 8080 and its management port on 8081; the worker opens
# only its management port, here on 8082.
docker run -d --name api-web --network api-image -p 8080:8080 -p "8081:$management_port" \
  --env-file "$RUNNER_TEMP/api.env" -e Host__Role=web api
docker run -d --name api-worker --network api-image -p "8082:$management_port" \
  --env-file "$RUNNER_TEMP/api.env" -e Host__Role=worker api

# The health endpoints answer on the management port of each process (apps/api/docs/ARCHITECTURE.md
# section 8).
for port in 8081 8082; do
  curl --silent --show-error --fail --max-time 5 \
    --retry 60 --retry-delay 1 --retry-all-errors --retry-max-time 60 \
    "http://localhost:$port$health_path"
  echo " on management port $port"
done

# The public port does not answer them.
status=$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' --max-time 5 \
  "http://localhost:8080$health_path")
echo "GET $health_path on public port 8080: $status"
if [[ "$status" != "404" ]]; then
  echo "The public port answered $status for $health_path, not 404." >&2
  exit 1
fi

# A false error trains people to ignore errors. When Npgsql tries GSSAPI encryption, the runtime
# prints "Cannot load library libgssapi_krb5.so.2" to stderr, and the platform logs it as an error.
docker logs api-web > "$RUNNER_TEMP/api-web.log" 2>&1
docker logs api-worker > "$RUNNER_TEMP/api-worker.log" 2>&1
failed=0
for process in migrate api-web api-worker; do
  if grep libgssapi "$RUNNER_TEMP/$process.log"; then
    echo "The $process output names libgssapi: a connection tried GSSAPI encryption." >&2
    failed=1
  fi
done
if (( failed )); then
  exit 1
fi
echo "No output names libgssapi."
