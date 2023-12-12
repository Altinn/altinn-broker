

INSERT INTO broker.file (file_id_pk, service_owner_id_fk, filename, checksum, sender_actor_id_fk, external_file_reference, created, storage_provider_id_fk, file_location)
VALUES ('ed76ce89-3768-481a-bca1-4e4262d9098b', '0192:991825827', 'filename.txt', NULL, 1, 'external_reference', NOW(), 1, 'https://blob-storage-example')