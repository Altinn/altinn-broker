CREATE OR REPLACE PROCEDURE broker.cleanup_old_data(
    purged_retention_days INTEGER DEFAULT 7,
    idempotency_retention_days INTEGER DEFAULT 30,
    batch_size INTEGER DEFAULT 1000,
    dry_run BOOLEAN DEFAULT false
)
LANGUAGE plpgsql
AS $$
DECLARE
    purged_deleted BIGINT;
    all_confirmed_deleted BIGINT;
    expired_deleted BIGINT;
    idempotency_deleted BIGINT;
    total_deleted BIGINT;
BEGIN
    IF dry_run THEN
        RAISE NOTICE '=== Starting Cleanup Job (DRY RUN) at % ===', NOW();
    ELSE
        RAISE NOTICE '=== Starting Cleanup Job at % ===', NOW();
    END IF;
    RAISE NOTICE 'Parameters: purged_retention=% days, idempotency_retention=% days, batch_size=%, dry_run=%', 
        purged_retention_days, idempotency_retention_days, batch_size, dry_run;
    RAISE NOTICE '';
    
    BEGIN
        CALL broker.cleanup_purged_file_transfers(
            retention_days := purged_retention_days,
            batch_size := batch_size,
            dry_run := dry_run,
            deleted_count := purged_deleted
        );
    EXCEPTION
        WHEN OTHERS THEN
            RAISE WARNING 'Error in cleanup_purged_file_transfers: %', SQLERRM;
            purged_deleted := 0;
    END;
    
    RAISE NOTICE '';
    
    BEGIN
        CALL broker.cleanup_all_confirmed_downloaded(
            batch_size := batch_size,
            dry_run := dry_run,
            deleted_count := all_confirmed_deleted
        );
    EXCEPTION
        WHEN OTHERS THEN
            RAISE WARNING 'Error in cleanup_all_confirmed_downloaded: %', SQLERRM;
            all_confirmed_deleted := 0;
    END;
    
    RAISE NOTICE '';
    
    BEGIN
        CALL broker.cleanup_expired_file_transfers(
            batch_size := batch_size,
            dry_run := dry_run,
            deleted_count := expired_deleted
        );
    EXCEPTION
        WHEN OTHERS THEN
            RAISE WARNING 'Error in cleanup_expired_file_transfers: %', SQLERRM;
            expired_deleted := 0;
    END;
    
    RAISE NOTICE '';
    
    BEGIN
        CALL broker.cleanup_idempotency_events(
            retention_days := idempotency_retention_days,
            dry_run := dry_run,
            deleted_count := idempotency_deleted
        );
    EXCEPTION
        WHEN OTHERS THEN
            RAISE WARNING 'Error in cleanup_idempotency_events: %', SQLERRM;
            idempotency_deleted := 0;
    END;
    
    total_deleted := purged_deleted + all_confirmed_deleted + expired_deleted;
    
    RAISE NOTICE '';
    IF dry_run THEN
        RAISE NOTICE '=== Cleanup Job Completed (DRY RUN) at % ===', NOW();
        RAISE NOTICE 'Summary (DRY RUN - no data was actually deleted):';
        RAISE NOTICE '  - Purged file transfers would be deleted: %', purged_deleted;
        RAISE NOTICE '  - AllConfirmedDownloaded file transfers would be deleted: %', all_confirmed_deleted;
        RAISE NOTICE '  - Expired file transfers would be deleted: %', expired_deleted;
        RAISE NOTICE '  - Total file transfers would be deleted: %', total_deleted;
        RAISE NOTICE '  - Idempotency events would be deleted: %', idempotency_deleted;
    ELSE
        RAISE NOTICE '=== Cleanup Job Completed at % ===', NOW();
        RAISE NOTICE 'Summary:';
        RAISE NOTICE '  - Purged file transfers deleted: %', purged_deleted;
        RAISE NOTICE '  - AllConfirmedDownloaded file transfers deleted: %', all_confirmed_deleted;
        RAISE NOTICE '  - Expired file transfers deleted: %', expired_deleted;
        RAISE NOTICE '  - Total file transfers deleted: %', total_deleted;
        RAISE NOTICE '  - Idempotency events deleted: %', idempotency_deleted;
        RAISE NOTICE '';
        RAISE NOTICE 'Note: Related rows in file_transfer_status and actor_file_transfer_status were automatically deleted via CASCADE';
    END IF;
END;
$$;
