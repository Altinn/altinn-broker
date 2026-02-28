CREATE OR REPLACE PROCEDURE broker.cleanup_purged_file_transfers(
    retention_days INTEGER DEFAULT 7,
    batch_size INTEGER DEFAULT 1000,
    dry_run BOOLEAN DEFAULT false,
    OUT deleted_count BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    purged_count BIGINT;
    deleted_file_transfers INTEGER;
    batch_count INTEGER;
    remaining_count BIGINT;
BEGIN
    IF dry_run THEN
        RAISE NOTICE '--- KATEGORI 1: Purged File Transfers (DRY RUN) ---';
    ELSE
        RAISE NOTICE '--- KATEGORI 1: Purged File Transfers ---';
    END IF;
    RAISE NOTICE 'Retention: % days', retention_days;
    
    SELECT COUNT(*) INTO purged_count
    FROM broker.file_transfer f
    WHERE f.latest_file_status_id = 6
      AND f.latest_file_status_date < NOW() - (retention_days || ' days')::INTERVAL;
    
    RAISE NOTICE 'Found % file transfers with Purged status older than % days', 
        purged_count, retention_days;
    
    deleted_count := 0;
    
    IF purged_count = 0 THEN
        RAISE NOTICE 'No purged file transfers to delete';
    ELSE
        IF dry_run THEN
            deleted_count := purged_count;
            batch_count := CEIL(purged_count::NUMERIC / batch_size);
            RAISE NOTICE 'Would delete % purged file transfers in % batches (DRY RUN)', 
                deleted_count, batch_count;
        ELSE
            batch_count := 0;
            remaining_count := purged_count;
            
            WHILE remaining_count > 0 LOOP
                WITH batch_to_delete AS (
                    SELECT file_transfer_id_pk
                    FROM broker.file_transfer
                    WHERE latest_file_status_id = 6
                      AND latest_file_status_date < NOW() - (retention_days || ' days')::INTERVAL
                    LIMIT batch_size
                )
                DELETE FROM broker.file_transfer
                WHERE file_transfer_id_pk IN (SELECT file_transfer_id_pk FROM batch_to_delete);
                
                GET DIAGNOSTICS deleted_file_transfers = ROW_COUNT;
                batch_count := batch_count + 1;
                
                IF deleted_file_transfers = 0 THEN
                    RAISE NOTICE 'No rows deleted in batch %. Exiting loop.', batch_count;
                    EXIT;
                END IF;
                
                remaining_count := remaining_count - deleted_file_transfers;
                deleted_count := deleted_count + deleted_file_transfers;
                
                RAISE NOTICE 'Batch %: Deleted % file transfers. Remaining: %', 
                    batch_count, deleted_file_transfers, remaining_count;
                
                PERFORM pg_sleep(0.1);
            END LOOP;
        END IF;
        
        IF NOT dry_run THEN
            RAISE NOTICE 'Successfully deleted % purged file transfers in % batches', 
                deleted_count, batch_count;
        END IF;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE broker.cleanup_all_confirmed_downloaded(
    batch_size INTEGER DEFAULT 1000,
    dry_run BOOLEAN DEFAULT false,
    OUT deleted_count BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    all_confirmed_count BIGINT;
    deleted_file_transfers INTEGER;
    batch_count INTEGER;
    remaining_count BIGINT;
BEGIN
    IF dry_run THEN
        RAISE NOTICE '--- KATEGORI 2: AllConfirmedDownloaded File Transfers (DRY RUN) ---';
    ELSE
        RAISE NOTICE '--- KATEGORI 2: AllConfirmedDownloaded File Transfers ---';
    END IF;
    
    SELECT COUNT(*) INTO all_confirmed_count
    FROM broker.file_transfer f
    INNER JOIN broker.altinn_resource r ON r.resource_id_pk = f.resource_id
    WHERE f.latest_file_status_id = 5
      AND r.purge_file_transfer_after_all_recipients_confirmed = true
      AND (
          (r.purge_file_transfer_grace_period IS NOT NULL 
           AND f.latest_file_status_date + r.purge_file_transfer_grace_period < NOW())
          OR
          (r.purge_file_transfer_grace_period IS NULL 
           AND f.latest_file_status_date < NOW() - INTERVAL '2 hours')
      );
    
    RAISE NOTICE 'Found % file transfers with AllConfirmedDownloaded status where grace period has passed', 
        all_confirmed_count;
    
    deleted_count := 0;
    
    IF all_confirmed_count = 0 THEN
        RAISE NOTICE 'No AllConfirmedDownloaded file transfers to delete';
    ELSE
        IF dry_run THEN
            deleted_count := all_confirmed_count;
            batch_count := CEIL(all_confirmed_count::NUMERIC / batch_size);
            RAISE NOTICE 'Would delete % AllConfirmedDownloaded file transfers in % batches (DRY RUN)', 
                deleted_count, batch_count;
        ELSE
            batch_count := 0;
            remaining_count := all_confirmed_count;
            
            WHILE remaining_count > 0 LOOP
                WITH batch_to_delete AS (
                    SELECT f.file_transfer_id_pk
                    FROM broker.file_transfer f
                    INNER JOIN broker.altinn_resource r ON r.resource_id_pk = f.resource_id
                    WHERE f.latest_file_status_id = 5
                      AND r.purge_file_transfer_after_all_recipients_confirmed = true
                      AND (
                          (r.purge_file_transfer_grace_period IS NOT NULL 
                           AND f.latest_file_status_date + r.purge_file_transfer_grace_period < NOW())
                          OR
                          (r.purge_file_transfer_grace_period IS NULL 
                           AND f.latest_file_status_date < NOW() - INTERVAL '2 hours')
                      )
                    LIMIT batch_size
                )
                DELETE FROM broker.file_transfer
                WHERE file_transfer_id_pk IN (SELECT file_transfer_id_pk FROM batch_to_delete);
                
                GET DIAGNOSTICS deleted_file_transfers = ROW_COUNT;
                batch_count := batch_count + 1;
                
                IF deleted_file_transfers = 0 THEN
                    RAISE NOTICE 'No rows deleted in batch %. Exiting loop.', batch_count;
                    EXIT;
                END IF;
                
                remaining_count := remaining_count - deleted_file_transfers;
                deleted_count := deleted_count + deleted_file_transfers;
                
                RAISE NOTICE 'Batch %: Deleted % file transfers. Remaining: %', 
                    batch_count, deleted_file_transfers, remaining_count;
                
                PERFORM pg_sleep(0.1);
            END LOOP;
        END IF;
        
        IF NOT dry_run THEN
            RAISE NOTICE 'Successfully deleted % AllConfirmedDownloaded file transfers in % batches', 
                deleted_count, batch_count;
        END IF;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE broker.cleanup_expired_file_transfers(
    batch_size INTEGER DEFAULT 1000,
    dry_run BOOLEAN DEFAULT false,
    OUT deleted_count BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    expired_count BIGINT;
    deleted_file_transfers INTEGER;
    batch_count INTEGER;
    remaining_count BIGINT;
BEGIN
    IF dry_run THEN
        RAISE NOTICE '--- KATEGORI 3: Expired File Transfers (DRY RUN) ---';
    ELSE
        RAISE NOTICE '--- KATEGORI 3: Expired File Transfers ---';
    END IF;
    
    SELECT COUNT(*) INTO expired_count
    FROM broker.file_transfer f
    WHERE f.expiration_time < NOW()
      AND f.latest_file_status_id IN (3, 5)
      AND NOT (
          f.latest_file_status_id = 5 
          AND EXISTS (
              SELECT 1 FROM broker.altinn_resource r 
              WHERE r.resource_id_pk = f.resource_id 
              AND r.purge_file_transfer_after_all_recipients_confirmed = true
          )
      );
    
    RAISE NOTICE 'Found % expired file transfers (expiration_time passed, backup cleanup)', 
        expired_count;
    
    deleted_count := 0;
    
    IF expired_count = 0 THEN
        RAISE NOTICE 'No expired file transfers to delete';
    ELSE
        IF dry_run THEN
            deleted_count := expired_count;
            batch_count := CEIL(expired_count::NUMERIC / batch_size);
            RAISE NOTICE 'Would delete % expired file transfers in % batches (DRY RUN)', 
                deleted_count, batch_count;
        ELSE
            batch_count := 0;
            remaining_count := expired_count;
            
            WHILE remaining_count > 0 LOOP
                WITH batch_to_delete AS (
                    SELECT f.file_transfer_id_pk
                    FROM broker.file_transfer f
                    WHERE f.expiration_time < NOW()
                      AND f.latest_file_status_id IN (3, 5)
                      AND NOT (
                          f.latest_file_status_id = 5 
                          AND EXISTS (
                              SELECT 1 FROM broker.altinn_resource r 
                              WHERE r.resource_id_pk = f.resource_id 
                              AND r.purge_file_transfer_after_all_recipients_confirmed = true
                          )
                      )
                    LIMIT batch_size
                )
                DELETE FROM broker.file_transfer
                WHERE file_transfer_id_pk IN (SELECT file_transfer_id_pk FROM batch_to_delete);
                
                GET DIAGNOSTICS deleted_file_transfers = ROW_COUNT;
                batch_count := batch_count + 1;
                
                IF deleted_file_transfers = 0 THEN
                    RAISE NOTICE 'No rows deleted in batch %. Exiting loop.', batch_count;
                    EXIT;
                END IF;
                
                remaining_count := remaining_count - deleted_file_transfers;
                deleted_count := deleted_count + deleted_file_transfers;
                
                RAISE NOTICE 'Batch %: Deleted % file transfers. Remaining: %', 
                    batch_count, deleted_file_transfers, remaining_count;
                
                PERFORM pg_sleep(0.1);
            END LOOP;
        END IF;
        
        IF NOT dry_run THEN
            RAISE NOTICE 'Successfully deleted % expired file transfers in % batches', 
                deleted_count, batch_count;
        END IF;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE broker.cleanup_idempotency_events(
    retention_days INTEGER DEFAULT 30,
    dry_run BOOLEAN DEFAULT false,
    OUT deleted_count BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF dry_run THEN
        RAISE NOTICE '--- Idempotency Events (DRY RUN) ---';
    ELSE
        RAISE NOTICE '--- Idempotency Events ---';
    END IF;
    RAISE NOTICE 'Retention: % days', retention_days;
    
    IF dry_run THEN
        SELECT COUNT(*) INTO deleted_count
        FROM broker.idempotency_event
        WHERE created < NOW() - (retention_days || ' days')::INTERVAL;
        
        RAISE NOTICE 'Would delete % old idempotency events (older than % days) (DRY RUN)', 
            deleted_count, retention_days;
    ELSE
        DELETE FROM broker.idempotency_event
        WHERE created < NOW() - (retention_days || ' days')::INTERVAL;
        
        GET DIAGNOSTICS deleted_count = ROW_COUNT;
        
        RAISE NOTICE 'Deleted % old idempotency events (older than % days)', 
            deleted_count, retention_days;
    END IF;
END;
$$;
