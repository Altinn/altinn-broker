\set ON_ERROR_STOP on

\echo '=== Cleanup Script with Procedures (each batch is its own transaction) ==='
\echo ''

CREATE OR REPLACE PROCEDURE broker.cleanup_purged_batch(
    INOUT batch_size INTEGER,
    OUT deleted_count INTEGER,
    OUT batch_duration INTERVAL
)
LANGUAGE plpgsql
AS $$
DECLARE
    batch_start_time TIMESTAMP;
BEGIN
    batch_start_time := clock_timestamp();
    
    DELETE FROM broker.file_transfer
    WHERE file_transfer_id_pk IN (
        SELECT f.file_transfer_id_pk
        FROM broker.file_transfer f
        INNER JOIN broker.altinn_resource r ON r.resource_id_pk = f.resource_id
        WHERE f.latest_file_status_id = 6  -- Purged
          AND f.created + COALESCE(r.file_transfer_time_to_live, INTERVAL '30 days') + INTERVAL '7 days' < NOW()
        LIMIT batch_size
    );
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    batch_duration := clock_timestamp() - batch_start_time;
    
    COMMIT;
END;
$$;

CREATE OR REPLACE PROCEDURE broker.cleanup_all_confirmed_batch(
    INOUT batch_size INTEGER,
    OUT deleted_count INTEGER,
    OUT batch_duration INTERVAL
)
LANGUAGE plpgsql
AS $$
DECLARE
    batch_start_time TIMESTAMP;
BEGIN
    batch_start_time := clock_timestamp();
    
    DELETE FROM broker.file_transfer
    WHERE file_transfer_id_pk IN (
        SELECT f.file_transfer_id_pk
        FROM broker.file_transfer f
        INNER JOIN broker.altinn_resource r ON r.resource_id_pk = f.resource_id
        WHERE f.latest_file_status_id = 5  -- AllConfirmedDownloaded
          AND r.purge_file_transfer_after_all_recipients_confirmed = true
          AND f.latest_file_status_date + COALESCE(r.purge_file_transfer_grace_period, INTERVAL '2 hours') < NOW()
        LIMIT batch_size
    );
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    batch_duration := clock_timestamp() - batch_start_time;
    
    COMMIT;
END;
$$;

CREATE OR REPLACE PROCEDURE broker.cleanup_expired_batch(
    INOUT batch_size INTEGER,
    OUT deleted_count INTEGER,
    OUT batch_duration INTERVAL
)
LANGUAGE plpgsql
AS $$
DECLARE
    batch_start_time TIMESTAMP;
BEGIN
    batch_start_time := clock_timestamp();
    
    DELETE FROM broker.file_transfer
    WHERE file_transfer_id_pk IN (
        SELECT f.file_transfer_id_pk
        FROM broker.file_transfer f
        LEFT JOIN broker.altinn_resource r ON r.resource_id_pk = f.resource_id
        WHERE f.expiration_time < NOW()
          AND (
              f.latest_file_status_id = 3  -- Published
              OR (
                  f.latest_file_status_id = 5  -- AllConfirmedDownloaded
                  AND COALESCE(r.purge_file_transfer_after_all_recipients_confirmed, false) = false
              )
          )
        LIMIT batch_size
    );
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    batch_duration := clock_timestamp() - batch_start_time;
    
    COMMIT;
END;
$$;

CREATE OR REPLACE PROCEDURE broker.cleanup_idempotency_batch(
    retention_days INTEGER,
    INOUT batch_size INTEGER,
    OUT deleted_count INTEGER,
    OUT batch_duration INTERVAL
)
LANGUAGE plpgsql
AS $$
DECLARE
    batch_start_time TIMESTAMP;
BEGIN
    batch_start_time := clock_timestamp();
    
    DELETE FROM broker.idempotency_event
    WHERE idempotency_event_id_pk IN (
        SELECT idempotency_event_id_pk
        FROM broker.idempotency_event
        WHERE created < NOW() - (retention_days || ' days')::INTERVAL
        LIMIT batch_size
    );
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    batch_duration := clock_timestamp() - batch_start_time;
    
    COMMIT;
END;
$$;

\echo '--- Category 1: Purged File Transfers ---'
\echo ''

DO $$
DECLARE
    batch_size INTEGER := 500;
    deleted_count INTEGER;
    total_deleted INTEGER := 0;
    batch_number INTEGER := 0;
    batch_duration INTERVAL;
    rows_per_second NUMERIC;
BEGIN
    LOOP
        CALL broker.cleanup_purged_batch(batch_size, deleted_count, batch_duration);
        
        batch_number := batch_number + 1;
        total_deleted := total_deleted + deleted_count;
        
        IF batch_duration > INTERVAL '0 seconds' THEN
            rows_per_second := deleted_count / EXTRACT(EPOCH FROM batch_duration);
            
            IF rows_per_second > 1000 AND batch_size < 2000 THEN
                batch_size := batch_size + 100;
            ELSIF rows_per_second < 100 AND batch_size > 100 THEN
                batch_size := batch_size - 50;
            END IF;
        END IF;
        
        RAISE NOTICE 'Batch %: Deleted % file transfers in %. Total deleted: %. Batch size: %', 
            batch_number, deleted_count, batch_duration, total_deleted, batch_size;
        
        EXIT WHEN deleted_count = 0;
        
        PERFORM pg_sleep(0.1);
    END LOOP;
    
    RAISE NOTICE 'Done. Total deleted: % purged file transfers in % batches', total_deleted, batch_number;
END $$;

\echo ''
\echo '--- Category 2: AllConfirmedDownloaded File Transfers ---'
\echo ''

DO $$
DECLARE
    batch_size INTEGER := 500;
    deleted_count INTEGER;
    total_deleted INTEGER := 0;
    batch_number INTEGER := 0;
    batch_duration INTERVAL;
    rows_per_second NUMERIC;
BEGIN
    LOOP
        CALL broker.cleanup_all_confirmed_batch(batch_size, deleted_count, batch_duration);
        
        batch_number := batch_number + 1;
        total_deleted := total_deleted + deleted_count;
        
        IF batch_duration > INTERVAL '0 seconds' THEN
            rows_per_second := deleted_count / EXTRACT(EPOCH FROM batch_duration);
            
            IF rows_per_second > 1000 AND batch_size < 2000 THEN
                batch_size := batch_size + 100;
            ELSIF rows_per_second < 100 AND batch_size > 100 THEN
                batch_size := batch_size - 50;
            END IF;
        END IF;
        
        RAISE NOTICE 'Batch %: Deleted % file transfers in %. Total deleted: %. Batch size: %', 
            batch_number, deleted_count, batch_duration, total_deleted, batch_size;
        
        EXIT WHEN deleted_count = 0;
        
        PERFORM pg_sleep(0.1);
    END LOOP;
    
    RAISE NOTICE 'Done. Total deleted: % AllConfirmedDownloaded file transfers in % batches', total_deleted, batch_number;
END $$;

\echo ''
\echo '--- Category 3: Expired File Transfers (backup cleanup) ---'
\echo ''

DO $$
DECLARE
    batch_size INTEGER := 500;
    deleted_count INTEGER;
    total_deleted INTEGER := 0;
    batch_number INTEGER := 0;
    batch_duration INTERVAL;
    rows_per_second NUMERIC;
BEGIN
    LOOP
        CALL broker.cleanup_expired_batch(batch_size, deleted_count, batch_duration);
        
        batch_number := batch_number + 1;
        total_deleted := total_deleted + deleted_count;
        
        IF batch_duration > INTERVAL '0 seconds' THEN
            rows_per_second := deleted_count / EXTRACT(EPOCH FROM batch_duration);
            
            IF rows_per_second > 1000 AND batch_size < 2000 THEN
                batch_size := batch_size + 100;
            ELSIF rows_per_second < 100 AND batch_size > 100 THEN
                batch_size := batch_size - 50;
            END IF;
        END IF;
        
        RAISE NOTICE 'Batch %: Deleted % file transfers in %. Total deleted: %. Batch size: %', 
            batch_number, deleted_count, batch_duration, total_deleted, batch_size;
        
        EXIT WHEN deleted_count = 0;
        
        PERFORM pg_sleep(0.1);
    END LOOP;
    
    RAISE NOTICE 'Done. Total deleted: % expired file transfers in % batches', total_deleted, batch_number;
END $$;

\echo ''
\echo '--- Category 4: Idempotency Events ---'
\echo ''

DO $$
DECLARE
    retention_days INTEGER := 30;
    batch_size INTEGER := 500;
    deleted_count INTEGER;
    total_deleted INTEGER := 0;
    batch_number INTEGER := 0;
    batch_duration INTERVAL;
    rows_per_second NUMERIC;
BEGIN
    LOOP
        CALL broker.cleanup_idempotency_batch(retention_days, batch_size, deleted_count, batch_duration);
        
        batch_number := batch_number + 1;
        total_deleted := total_deleted + deleted_count;
        
        IF batch_duration > INTERVAL '0 seconds' THEN
            rows_per_second := deleted_count / EXTRACT(EPOCH FROM batch_duration);
            
            IF rows_per_second > 1000 AND batch_size < 2000 THEN
                batch_size := batch_size + 100;
            ELSIF rows_per_second < 100 AND batch_size > 100 THEN
                batch_size := batch_size - 50;
            END IF;
        END IF;
        
        RAISE NOTICE 'Batch %: Deleted % idempotency events in %. Total deleted: %. Batch size: %', 
            batch_number, deleted_count, batch_duration, total_deleted, batch_size;
        
        EXIT WHEN deleted_count = 0;
        
        PERFORM pg_sleep(0.1);
    END LOOP;
    
    RAISE NOTICE 'Done. Total deleted: % idempotency events in % batches', total_deleted, batch_number;
END $$;

\echo ''
\echo 'Cleaning up procedures...'

DROP PROCEDURE broker.cleanup_purged_batch;
DROP PROCEDURE broker.cleanup_all_confirmed_batch;
DROP PROCEDURE broker.cleanup_expired_batch;
DROP PROCEDURE broker.cleanup_idempotency_batch;

\echo 'Done.'

