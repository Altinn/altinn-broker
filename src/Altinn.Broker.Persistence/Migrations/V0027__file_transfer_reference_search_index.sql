-- Trigram-indeks for serverside-søk på avsenders referanse i BrokerBox.
--
-- Søket er delstrengsøk (ILIKE '%term%'), som en vanlig btree-indeks ikke kan brukes til.
-- Uten denne blir hvert søk en full gjennomgang av broker.file_transfer.
--
-- Her er opprettingen bevisst ikke defensiv, i motsetning til pgaudit i V0025: en stille
-- fallback til seq scan på en tabell med millioner av rader er verre enn en deploy som
-- stopper og ber noen tillate pg_trgm i azure.extensions.
--
-- CONCURRENTLY fordi tabellen er stor i produksjon. Merk at en avbrutt CONCURRENTLY
-- etterlater en ugyldig indeks som må slettes manuelt før migrasjonen kan kjøres på nytt.

-- flyway:executeInTransaction=false

CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_file_transfer_reference_trgm
ON broker.file_transfer
USING gin (external_file_transfer_reference gin_trgm_ops);
