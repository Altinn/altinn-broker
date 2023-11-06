-- Create schema
CREATE SCHEMA broker;
CREATE EXTENSION "uuid-ossp";

-- Grant access for Azure AD users (application and developers)
DO $do$
DECLARE
    role_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO role_count FROM pg_roles WHERE rolname = 'azure_pg_admin';

    IF role_count > 0 THEN
        GRANT USAGE ON SCHEMA broker TO azure_pg_admin;
        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA broker TO azure_pg_admin;
        ALTER DEFAULT PRIVILEGES IN SCHEMA broker GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO azure_pg_admin;
    END IF;
END
$do$;

-- Create tables
CREATE TABLE broker.actor (
    actor_id_pk bigserial PRIMARY KEY,
    actor_external_id character varying(500) NOT NULL
);

CREATE TABLE broker.file_status_description (
    file_status_description_id_pk integer PRIMARY KEY,
    file_status_description character varying(200) NOT NULL
);

CREATE TABLE broker.storage_reference (
    storage_reference_id_pk bigserial PRIMARY KEY,
    file_location character varying(600) NOT NULL
);

CREATE TABLE broker.file (
    file_id_pk uuid PRIMARY KEY,
    application_id character varying(100) NOT NULL,
    filename character varying(500) NOT NULL,
    checksum character varying(500) NULL,
    sender_actor_id_fk bigserial,
    external_file_reference character varying(500) NOT NULL,
    file_status_description_id_fk integer NOT NULL,
    last_status_update timestamp without time zone,
    uploaded timestamp without time zone NOT NULL,
    storage_reference_id_fk bigint NOT NULL,
    FOREIGN KEY (file_status_description_id_fk) REFERENCES broker.file_status_description (file_status_description_id_pk),
    FOREIGN KEY (storage_reference_id_fk) REFERENCES broker.storage_reference (storage_reference_id_pk)
);

CREATE TABLE broker.file_status (
    file_status_id_pk bigserial PRIMARY KEY,
    file_id_fk uuid NOT NULL,
    file_status_description_id_fk integer NOT NULL,
    file_status_date timestamp without time zone NOT NULL,
    FOREIGN KEY (file_status_description_id_fk) REFERENCES broker.file_status_description (file_status_description_id_pk),
    FOREIGN KEY (file_id_fk) REFERENCES broker.file (file_id_pk) ON DELETE CASCADE
);

CREATE TABLE broker.file_metadata (
    metadata_id_pk bigserial PRIMARY KEY,
    file_id_fk uuid NOT NULL,
    key character varying(50) NOT NULL,
    value character varying(300) NOT NULL,
    FOREIGN KEY (file_id_fk) REFERENCES broker.file (file_id_pk) ON DELETE CASCADE
);

CREATE TABLE broker.actor_file_status_description (
    actor_file_status_id_pk integer PRIMARY KEY,
    actor_file_status_description character varying(200) NOT NULL
);

CREATE TABLE broker.actor_file_status (
    actor_file_status_id_pk bigserial PRIMARY KEY,
    actor_id_fk bigint NOT NULL,
    file_id_fk uuid NOT NULL,
    actor_file_status_id_fk integer NOT NULL,
    actor_file_status_date timestamp without time zone NOT NULL,
    FOREIGN KEY (actor_id_fk) REFERENCES broker.actor (actor_id_pk) ON DELETE CASCADE,
    FOREIGN KEY (actor_file_status_id_fk) REFERENCES broker.actor_file_status_description (actor_file_status_id_pk),
    FOREIGN KEY (file_id_fk) REFERENCES broker.file (file_id_pk)
);

-- Create indexes
CREATE INDEX ix_file_application_id ON broker.file (application_id);
CREATE INDEX ix_file_external_reference ON broker.file (external_file_reference);
CREATE INDEX ix_file_status_id ON broker.file_status (file_id_fk);
CREATE INDEX ix_actor_file_status_id ON broker.actor_file_status (file_id_fk);