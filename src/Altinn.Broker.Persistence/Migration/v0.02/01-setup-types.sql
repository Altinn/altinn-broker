DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'emailnotificationresulttype') THEN
        CREATE TYPE emailnotificationresulttype AS ENUM ('New', 'Sending', 'Succeeded', 'Failed_RecipientNotIdentified');
    END IF;
END$$;