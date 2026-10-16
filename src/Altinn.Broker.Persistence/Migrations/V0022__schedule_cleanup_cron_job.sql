DO $do$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_cron') THEN
        
        PERFORM cron.unschedule('broker_cleanup_job')
        WHERE EXISTS (
            SELECT 1 FROM cron.job WHERE jobname = 'broker_cleanup_job'
        );
        
        PERFORM cron.schedule(
            'broker_cleanup_job',
            '0 2 * * *',
            'CALL broker.cleanup_old_data();'
        );
        
        RAISE NOTICE 'Cleanup job scheduled successfully: broker_cleanup_job (runs daily at 02:00)';
    ELSE
        RAISE NOTICE 'pg_cron extension not available. Cleanup job not scheduled.';
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        RAISE NOTICE 'Could not schedule cleanup job: %', SQLERRM;
END
$do$;
