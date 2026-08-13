-- Data striping: a file transfer's content may be spread over several Azure block blobs.
--
-- Azure caps a block blob at 50 000 blocks, so a large transfer is split into stripes of
-- stripe_size_bytes each. Stripe 0 keeps the historical blob name {file_transfer_id_pk};
-- stripe k > 0 is named {file_transfer_id_pk}/stripe-{k:D4}.
--
-- NULL means the content is a single block blob named {file_transfer_id_pk}. That is how every
-- transfer created before data striping is stored, and how small transfers and every non-TUS
-- upload are still stored today.
--
-- The value is written twice: at initialize as the planned layout, derived from the resource's
-- maximum transfer size and frozen there so a later configuration change cannot alter how
-- already-written content is read; and again at upload completion with the layout actually
-- observed in storage.
--
-- Stripe count is derived as ceil(file_transfer_size / stripe_size_bytes) and deliberately not
-- stored, so it cannot drift out of sync with file_transfer_size.
ALTER TABLE broker.file_transfer
    ADD COLUMN stripe_size_bytes bigint NULL;
