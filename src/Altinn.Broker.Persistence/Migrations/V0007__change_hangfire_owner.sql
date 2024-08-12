ALTER SCHEMA hangfire OWNER TO azure_pg_admin;

CREATE OR REPLACE FUNCTION set_hangfire_table_owner()
RETURNS event_trigger
LANGUAGE plpgsql
AS $$
DECLARE
    obj record;
    target_role name := 'azure_pg_admin';
    target_schema name := 'hangfire';
BEGIN
    FOR obj IN 
        SELECT * FROM pg_event_trigger_ddl_commands()
        WHERE command_tag = 'CREATE TABLE'
          AND object_schema = target_schema
    LOOP
        EXECUTE format(
            'ALTER TABLE %I.%I OWNER TO %I',
            obj.object_schema,
            obj.object_name,
            target_role
        );
        
        RAISE NOTICE 'Changed owner of %.% to %', 
            obj.object_schema, obj.object_name, target_role;
    END LOOP;
END;
$$;

-- Create the event trigger
CREATE EVENT TRIGGER set_hangfire_table_owner_trigger
ON ddl_command_end
WHEN TAG IN ('CREATE TABLE')
EXECUTE FUNCTION set_hangfire_table_owner();