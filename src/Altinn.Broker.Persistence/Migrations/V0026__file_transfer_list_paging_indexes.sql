-- Indekser for cursor-paginering av formidlingslistene i BrokerBox.
--
-- Listene sorterer på latest_file_status_date, og fram til nå fantes det ingen indeks på den
-- kolonnen: spørringen sorterte alle matchende rader før LIMIT. For en part med hundretusener
-- av formidlinger er det en sortering per sidevisning.
--
-- Keyset-paginering trenger dessuten en deterministisk rekkefølge, derfor file_transfer_id_pk
-- som sekundærnøkkel i samme retning som sorteringen.

-- Dekker avsender-grenen av spørringen: likhet på aktør og status, deretter sortert.
CREATE INDEX IF NOT EXISTS idx_file_transfer_sender_status_date
ON broker.file_transfer (
    sender_actor_id_fk,
    latest_file_status_id,
    latest_file_status_date DESC,
    file_transfer_id_pk DESC
);

-- Dekker den ordnede gjennomgangen når mottaker-grenen slår til, så planleggeren kan lese i
-- sorteringsrekkefølge og stoppe etter LIMIT rader i stedet for å sortere hele treffmengden.
CREATE INDEX IF NOT EXISTS idx_file_transfer_status_date_id
ON broker.file_transfer (
    latest_file_status_id,
    latest_file_status_date DESC,
    file_transfer_id_pk DESC
);
