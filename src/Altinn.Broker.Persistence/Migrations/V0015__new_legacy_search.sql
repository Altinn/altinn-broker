-- For actor file transfer status CTE
CREATE index CONCURRENTLY idx_actor_file_transfer_status_window 
ON broker.actor_file_transfer_status (file_transfer_id_fk, actor_id_fk, actor_file_transfer_status_id_pk DESC);

-- For file transfer status CTE  
CREATE INDEX concurrently idx_file_transfer_status_window
ON broker.file_transfer_status (file_transfer_id_fk, file_transfer_status_id_pk DESC);

-- For main query filtering
CREATE INDEX concurrently idx_file_transfer_search
ON broker.file_transfer (created, resource_id) 
INCLUDE (file_transfer_id_pk);
