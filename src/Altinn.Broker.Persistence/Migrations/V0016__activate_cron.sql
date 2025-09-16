DO $do$
BEGIN
    -- Create extension only if it's available on the server
    IF EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'pg_cron') THEN
        CREATE EXTENSION IF NOT EXISTS pg_cron;
    END IF;

    -- Only touch cron schema if extension is actually installed
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_cron') THEN
        -- Idempotency: remove any existing job with the same name
        DELETE FROM cron.job WHERE jobname = 'weekly_analyze';

        PERFORM cron.schedule(
            'weekly_analyze',
            '0 4 * * 0',
            $$ANALYZE;$$
        );
    END IF;
END
$do$;