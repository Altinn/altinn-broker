ALTER TABLE broker.altinn_resource
ALTER COLUMN required_party TYPE TEXT
USING CASE
    WHEN required_party IS TRUE THEN organization_number
    ELSE NULL
END;
