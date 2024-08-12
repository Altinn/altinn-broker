ALTER SCHEMA hangfire OWNER TO azure_pg_admin;

CREATE OR REPLACE FUNCTION set_table_owner()
RETURNS event_trigger AS $$
DECLARE
  obj record;
BEGIN
  FOR obj IN SELECT * FROM pg_event_trigger_ddl_commands() 
    WHERE command_tag = 'CREATE TABLE' 
    AND object_identity ~ '^hangfire\.'
  LOOP
    EXECUTE format('ALTER TABLE %s OWNER TO azure_pg_admin', obj.object_identity);
END LOOP;
END;
$$ LANGUAGE plpgsql;

CREATE EVENT TRIGGER set_table_owner_trigger
ON ddl_command_end
WHEN TAG IN ('CREATE TABLE')
EXECUTE FUNCTION set_table_owner();
