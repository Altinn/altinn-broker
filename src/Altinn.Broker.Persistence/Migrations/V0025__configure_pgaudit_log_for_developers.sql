DO $do$
BEGIN
    -- First, try to create the pgaudit extension.
    BEGIN
        CREATE EXTENSION IF NOT EXISTS pgaudit;
        RAISE NOTICE 'PGAUDIT extension created successfully';
    EXCEPTION
        WHEN OTHERS THEN
            RAISE NOTICE 'PGAUDIT extension could not be created: %', SQLERRM;
    END;

    -- Then, try to set pgaudit.log = 'all' for Altinn-30-Broker-Test-Developers (test environments).
    BEGIN
        ALTER ROLE "Altinn-30-Broker-Test-Developers" SET pgaudit.log = 'all';
        RAISE NOTICE 'Set pgaudit.log for Altinn-30-Broker-Test-Developers';
    EXCEPTION WHEN undefined_object THEN
        RAISE NOTICE 'Role Altinn-30-Broker-Test-Developers does not exist, skipping';
    END;

    -- Try to set pgaudit.log = 'all' for Altinn-30-Broker-Prod-Developers (production environments).
    BEGIN
        ALTER ROLE "Altinn-30-Broker-Prod-Developers" SET pgaudit.log = 'all';
        RAISE NOTICE 'Set pgaudit.log for Altinn-30-Broker-Prod-Developers';
    EXCEPTION WHEN undefined_object THEN
        RAISE NOTICE 'Role Altinn-30-Broker-Prod-Developers does not exist, skipping';
    END;
EXCEPTION
    WHEN OTHERS THEN
        -- Log the error but do not fail the migration.
        RAISE NOTICE 'Could not configure pgaudit for developer roles: %', SQLERRM;
END
$do$;
