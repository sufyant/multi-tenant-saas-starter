-- Creates the database roles. Runs before the migrations, as a role with CREATEROLE that owns the database: on a hosted
-- database, the owner role the provider created (see docs/setup.md). A superuser works too. Safe to run again; a rerun
-- sets the passwords given and changes nothing else.
--
--   psql "$DATABASE_URL" -v owner_password=... -v application_password=... -f bootstrap.sql

\set ON_ERROR_STOP on

-- No role bypasses row level security. CREATE ROLE may name every attribute with a value the running role may give.
-- ALTER ROLE may not: since PostgreSQL 16, naming SUPERUSER or BYPASSRLS needs that attribute, whatever the value.
SELECT 'CREATE ROLE api_owner LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'api_owner') \gexec
SELECT 'CREATE ROLE api_application LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'api_application') \gexec

SELECT format('ALTER ROLE api_owner LOGIN PASSWORD %L', :'owner_password') \gexec
SELECT format('ALTER ROLE api_application LOGIN PASSWORD %L', :'application_password') \gexec

-- A rerun does not reset the attributes, so a role that someone changed by hand stops the script here.
DO $$
DECLARE
    found text;
BEGIN
    SELECT string_agg(format('%s has %s', rolname, attribute), ', ' ORDER BY rolname, attribute)
    INTO found
    FROM pg_roles
    CROSS JOIN LATERAL (VALUES
        (rolsuper, 'SUPERUSER'), (rolbypassrls, 'BYPASSRLS'), (rolcreatedb, 'CREATEDB'), (rolcreaterole, 'CREATEROLE')
    ) AS attributes (granted, attribute)
    WHERE rolname IN ('api_owner', 'api_application') AND granted;

    IF found IS NOT NULL THEN
        RAISE EXCEPTION 'A role of the API has an attribute it must not have: %. Remove these attributes, then run this script again.', found;
    END IF;
END
$$;

-- The owner creates each module's schema in its migrations; the application role receives privileges from those migrations.
SELECT format('GRANT CREATE ON DATABASE %I TO api_owner', current_database()) \gexec
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
