#!/bin/bash
# Runs once on first Postgres init: add the Keycloak database next to `schuly`.
set -e
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname schuly <<-SQL
  SELECT 'CREATE DATABASE keycloak' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak')\gexec
SQL
