DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'brokershipmentstatus') THEN
        CREATE TYPE brokershipmentstatus AS ENUM ('Initiated', 'Processing', 'ReadyForDownload', 'Deleted');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'brokerfilestatus') THEN
        CREATE TYPE brokerfilestatus AS ENUM ('Uploaded', 'Validating', 'ReadyForDownload', 'Deleted', 'Error');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'afstatus') THEN
        CREATE TYPE afstatus AS ENUM ('Initiated', 'ReadyForDownload', 'Downloaded', 'Confirmed');
    END IF;
END$$;