-- This script backfills:
-- 1. latest_file_status_id and latest_file_status_date on file_transfer table
-- 2. actor_file_transfer_latest_status lookup table

DO $$
DECLARE
  batch_size INT := 5000;
  rows_updated INT;
  total_updated INT := 0;
  start_time TIMESTAMP;
  elapsed INTERVAL;
BEGIN
  
  -- PART 1: Backfill file_transfer.latest_file_status_id
  RAISE NOTICE '========================================';
  RAISE NOTICE 'Starting file_transfer status backfill';
  RAISE NOTICE '========================================';
  
  start_time := clock_timestamp();
  total_updated := 0;
  
  LOOP
    WITH batch AS (
      SELECT file_transfer_id_pk
      FROM broker.file_transfer
      WHERE latest_file_status_id IS NULL
      ORDER BY file_transfer_id_pk
      LIMIT batch_size
    ),
    latest_statuses AS (
      SELECT DISTINCT ON (fs.file_transfer_id_fk)
        fs.file_transfer_id_fk,
        fs.file_transfer_status_description_id_fk,
        fs.file_transfer_status_date
      FROM broker.file_transfer_status fs
      INNER JOIN batch b ON b.file_transfer_id_pk = fs.file_transfer_id_fk
      ORDER BY fs.file_transfer_id_fk, fs.file_transfer_status_id_pk DESC
    )
    UPDATE broker.file_transfer f
    SET 
      latest_file_status_id = ls.file_transfer_status_description_id_fk,
      latest_file_status_date = ls.file_transfer_status_date
    FROM latest_statuses ls
    WHERE f.file_transfer_id_pk = ls.file_transfer_id_fk;
    
    GET DIAGNOSTICS rows_updated = ROW_COUNT;
    
    EXIT WHEN rows_updated = 0;
    
    total_updated := total_updated + rows_updated;
    elapsed := clock_timestamp() - start_time;
    
    RAISE NOTICE 'File status: Updated % rows (Total: %, Elapsed: %)', 
      rows_updated, total_updated, elapsed;
    
    -- Small delay between batches to reduce load
    PERFORM pg_sleep(0.1);
    
    -- Commit every batch
    COMMIT;
  END LOOP;
  
  elapsed := clock_timestamp() - start_time;
  RAISE NOTICE '========================================';
  RAISE NOTICE 'File status backfill COMPLETE';
  RAISE NOTICE 'Total rows updated: %', total_updated;
  RAISE NOTICE 'Total time: %', elapsed;
  RAISE NOTICE '========================================';
  RAISE NOTICE '';
  
  -- PART 2: Backfill actor_file_transfer_latest_status table
  RAISE NOTICE '========================================';
  RAISE NOTICE 'Starting actor status backfill';
  RAISE NOTICE '========================================';
  
  start_time := clock_timestamp();
  total_updated := 0;
  
  LOOP
    WITH batch AS (
      -- Find distinct (file, actor) combinations that haven't been backfilled yet
      SELECT DISTINCT file_transfer_id_fk, actor_id_fk
      FROM broker.actor_file_transfer_status afts
      WHERE NOT EXISTS (
        SELECT 1 
        FROM broker.actor_file_transfer_latest_status afls
        WHERE afls.file_transfer_id_fk = afts.file_transfer_id_fk
          AND afls.actor_id_fk = afts.actor_id_fk
      )
      ORDER BY file_transfer_id_fk, actor_id_fk
      LIMIT batch_size
    ),
    latest_statuses AS (
      -- Get the latest status for each (file, actor) pair in this batch
      SELECT DISTINCT ON (afts.file_transfer_id_fk, afts.actor_id_fk)
        afts.file_transfer_id_fk,
        afts.actor_id_fk,
        afts.actor_file_transfer_status_description_id_fk,
        afts.actor_file_transfer_status_date
      FROM broker.actor_file_transfer_status afts
      INNER JOIN batch b 
        ON b.file_transfer_id_fk = afts.file_transfer_id_fk
        AND b.actor_id_fk = afts.actor_id_fk
      ORDER BY 
        afts.file_transfer_id_fk, 
        afts.actor_id_fk, 
        afts.actor_file_transfer_status_id_pk DESC
    )
    INSERT INTO broker.actor_file_transfer_latest_status (
      file_transfer_id_fk,
      actor_id_fk,
      latest_actor_status_id,
      latest_actor_status_date
    )
    SELECT 
      file_transfer_id_fk,
      actor_id_fk,
      actor_file_transfer_status_description_id_fk,
      actor_file_transfer_status_date
    FROM latest_statuses
    ON CONFLICT (file_transfer_id_fk, actor_id_fk) DO NOTHING;
    
    GET DIAGNOSTICS rows_updated = ROW_COUNT;
    
    EXIT WHEN rows_updated = 0;
    
    total_updated := total_updated + rows_updated;
    elapsed := clock_timestamp() - start_time;
    
    RAISE NOTICE 'Actor status: Inserted % rows (Total: %, Elapsed: %)', 
      rows_updated, total_updated, elapsed;
    
    -- Small delay between batches
    PERFORM pg_sleep(0.1);
    
    -- Commit every batch
    COMMIT;
  END LOOP;
  
  elapsed := clock_timestamp() - start_time;
  RAISE NOTICE '========================================';
  RAISE NOTICE 'Actor status backfill COMPLETE';
  RAISE NOTICE 'Total rows inserted: %', total_updated;
  RAISE NOTICE 'Total time: %', elapsed;
  RAISE NOTICE '========================================';
  RAISE NOTICE '';
  
  -- PART 3: Summary and Validation
  RAISE NOTICE '========================================';
  RAISE NOTICE 'BACKFILL SUMMARY';
  RAISE NOTICE '========================================';
  
  -- File transfer status summary
  DECLARE
    total_files BIGINT;
    backfilled_files BIGINT;
    remaining_files BIGINT;
  BEGIN
    SELECT 
      COUNT(*),
      COUNT(latest_file_status_id),
      COUNT(*) - COUNT(latest_file_status_id)
    INTO total_files, backfilled_files, remaining_files
    FROM broker.file_transfer;
    
    RAISE NOTICE 'File Transfer Status:';
    RAISE NOTICE '  Total files: %', total_files;
    RAISE NOTICE '  Backfilled: %', backfilled_files;
    RAISE NOTICE '  Remaining: %', remaining_files;
    RAISE NOTICE '  Progress: %%%', ROUND(100.0 * backfilled_files / NULLIF(total_files, 0), 2);
  END;
  
  -- Actor status summary
  DECLARE
    total_actor_file_combinations BIGINT;
    backfilled_combinations BIGINT;
  BEGIN
    SELECT COUNT(DISTINCT (file_transfer_id_fk, actor_id_fk))
    INTO total_actor_file_combinations
    FROM broker.actor_file_transfer_status;
    
    SELECT COUNT(*)
    INTO backfilled_combinations
    FROM broker.actor_file_transfer_latest_status;
    
    RAISE NOTICE '';
    RAISE NOTICE 'Actor File Transfer Status:';
    RAISE NOTICE '  Total (file, actor) combinations: %', total_actor_file_combinations;
    RAISE NOTICE '  Backfilled: %', backfilled_combinations;
    RAISE NOTICE '  Remaining: %', total_actor_file_combinations - backfilled_combinations;
    RAISE NOTICE '  Progress: %%%', ROUND(100.0 * backfilled_combinations / NULLIF(total_actor_file_combinations, 0), 2);
  END;
  
  RAISE NOTICE '========================================';
  
END $$;

