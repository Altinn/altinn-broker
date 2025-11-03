DO $do$
BEGIN
    -- Try to create the extension
    CREATE EXTENSION IF NOT EXISTS pg_cron;

    -- If successful, schedule the weekly ANALYZE job
    PERFORM cron.schedule(
        'weekly_analyze',
        '0 4 * * 0',
        $$ ANALYZE; $$
    );
EXCEPTION
    WHEN OTHERS THEN
        -- Log the error but don't fail the migration
        RAISE NOTICE 'pg_cron could not be activated: %', SQLERRM;
END
$do$;