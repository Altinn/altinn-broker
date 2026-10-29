-- Indexes for cursor paging of the file transfer lists in BrokerBox.
--
-- The lists sort by latest_file_status_date, and until now no index covered that column: the query
-- sorted every matching row before applying LIMIT. For a party with hundreds of thousands of file
-- transfers that is a full sort per page view.
--
-- Keyset paging also needs a deterministic order, hence file_transfer_id_pk as the tiebreaker in
-- the same direction as the sort.

-- Covers the sender branch of the query: equality on actor and status, then ordered.
CREATE INDEX IF NOT EXISTS idx_file_transfer_sender_status_date
ON broker.file_transfer (
    sender_actor_id_fk,
    latest_file_status_id,
    latest_file_status_date DESC,
    file_transfer_id_pk DESC
);

-- Covers the ordered scan when the recipient branch matches, so the planner can read in sort order
-- and stop after LIMIT rows instead of sorting the whole match set.
CREATE INDEX IF NOT EXISTS idx_file_transfer_status_date_id
ON broker.file_transfer (
    latest_file_status_id,
    latest_file_status_date DESC,
    file_transfer_id_pk DESC
);
