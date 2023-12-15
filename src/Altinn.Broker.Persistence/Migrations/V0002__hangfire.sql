CREATE SCHEMA hangfire;

DO $do$
DECLARE
    role_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO role_count FROM pg_roles WHERE rolname = 'azure_pg_admin';

    IF role_count > 0 THEN
		GRANT ALL ON SCHEMA hangfire TO azure_pg_admin;
        ALTER DEFAULT PRIVILEGES IN SCHEMA hangfire GRANT ALL ON TABLES TO azure_pg_admin;
        ALTER DEFAULT PRIVILEGES IN SCHEMA hangfire GRANT ALL ON SEQUENCES TO azure_pg_admin;
	END IF;
END
$do$;
