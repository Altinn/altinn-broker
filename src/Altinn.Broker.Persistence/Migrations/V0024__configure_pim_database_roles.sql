DO $do$
DECLARE
    read_role_name TEXT := '${brokerDbReadAdGroupName}';
    read_role_oid TEXT := '${brokerDbReadAdGroupId}';
    write_role_name TEXT := '${brokerDbWriteAdGroupName}';
    write_role_oid TEXT := '${brokerDbWriteAdGroupId}';
BEGIN
    IF NULLIF(BTRIM(read_role_name), '') IS NULL OR NULLIF(BTRIM(read_role_oid), '') IS NULL THEN
        RAISE NOTICE 'Broker read AD group name or object id is empty, skipping read role configuration';
    ELSE
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = read_role_name) THEN
            EXECUTE FORMAT('CREATE ROLE %I LOGIN', read_role_name);
            RAISE NOTICE 'Created Broker read role %', read_role_name;
        END IF;

        EXECUTE FORMAT(
            'SECURITY LABEL FOR pgaadauth ON ROLE %I IS %L',
            read_role_name,
            'aadauth,oid=' || read_role_oid || ',type=group'
        );
        EXECUTE FORMAT('GRANT CONNECT ON DATABASE %I TO %I', CURRENT_DATABASE(), read_role_name);
        EXECUTE FORMAT('GRANT USAGE ON SCHEMA broker TO %I', read_role_name);
        EXECUTE FORMAT('GRANT SELECT ON ALL TABLES IN SCHEMA broker TO %I', read_role_name);
        EXECUTE FORMAT('ALTER DEFAULT PRIVILEGES IN SCHEMA broker GRANT SELECT ON TABLES TO %I', read_role_name);
        EXECUTE FORMAT('ALTER ROLE %I SET pgaudit.log = %L', read_role_name, 'all');
        RAISE NOTICE 'Configured Broker read role %', read_role_name;
    END IF;

    IF NULLIF(BTRIM(write_role_name), '') IS NULL OR NULLIF(BTRIM(write_role_oid), '') IS NULL THEN
        RAISE NOTICE 'Broker write AD group name or object id is empty, skipping write role configuration';
    ELSE
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = write_role_name) THEN
            EXECUTE FORMAT('CREATE ROLE %I LOGIN', write_role_name);
            RAISE NOTICE 'Created Broker write role %', write_role_name;
        END IF;

        EXECUTE FORMAT(
            'SECURITY LABEL FOR pgaadauth ON ROLE %I IS %L',
            write_role_name,
            'aadauth,oid=' || write_role_oid || ',type=group'
        );
        EXECUTE FORMAT('GRANT CONNECT ON DATABASE %I TO %I', CURRENT_DATABASE(), write_role_name);
        EXECUTE FORMAT('GRANT USAGE ON SCHEMA broker TO %I', write_role_name);
        EXECUTE FORMAT('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA broker TO %I', write_role_name);
        EXECUTE FORMAT('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA broker TO %I', write_role_name);
        EXECUTE FORMAT('ALTER DEFAULT PRIVILEGES IN SCHEMA broker GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I', write_role_name);
        EXECUTE FORMAT('ALTER DEFAULT PRIVILEGES IN SCHEMA broker GRANT USAGE, SELECT ON SEQUENCES TO %I', write_role_name);
        EXECUTE FORMAT('ALTER ROLE %I SET pgaudit.log = %L', write_role_name, 'all');
        RAISE NOTICE 'Configured Broker write role %', write_role_name;
    END IF;
END
$do$;
