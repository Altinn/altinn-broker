-- Create schema
CREATE SCHEMA broker;

-- Add uuid-ossp extension (only local, in environment it must be handled by IAC)
DO $do$
DECLARE
    uuid_extension_installed BOOL;
BEGIN
	SELECT EXISTS 
		(SELECT 1 FROM pg_extension
		WHERE extname = 'uuid-ossp')
	INTO uuid_extension_installed;
	
	IF uuid_extension_installed = false THEN
		CREATE EXTENSION "uuid-ossp"; 
	END IF;
END;
$do$;

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
        ALTER DEFAULT PRIVILEGES IN SCHEMA broker GRANT ALL ON SEQUENCES TO azure_pg_admin;
	END IF;
END
$do$;

-- Create tables
CREATE TABLE broker.actor (
    actor_id_pk bigserial PRIMARY KEY,
    actor_external_id character varying(500) NOT NULL
);

CREATE TABLE broker.service_owner (
    service_owner_id_pk character varying(14) NOT NULL PRIMARY KEY,
    service_owner_name character varying(500) NOT NULL,
    file_time_to_live interval NOT NULL,
    CONSTRAINT service_owner_id_pk_format CHECK (service_owner_id_pk ~ '^\d{4}:\d{9}$')
);

CREATE TABLE broker.service (
    service_id_pk bigserial PRIMARY KEY,
    created timestamp without time zone NOT NULL,
    client_id character varying(36) NOT NULL,
    organization_number character varying(14) NOT NULL,
    service_owner_id_fk character varying(14) NOT NULL,
    FOREIGN KEY (service_owner_id_fk) REFERENCES broker.service_owner (service_owner_id_pk),
    CONSTRAINT organization_number_format CHECK (organization_number ~ '^\d{4}:\d{9}$')
);

CREATE TABLE broker.storage_provider (
    storage_provider_id_pk bigserial PRIMARY KEY,    
    service_owner_id_fk character varying(14) NOT NULL,
    created timestamp without time zone NOT NULL,
    storage_provider_type character varying(50) NOT NULL CHECK (storage_provider_type = 'Altinn3Azure'),
    resource_name character varying(500) NOT NULL,
    FOREIGN KEY (service_owner_id_fk) REFERENCES broker.service_owner (service_owner_id_pk)
);

CREATE TABLE broker.file_status_description (
    file_status_description_id_pk integer PRIMARY KEY,
    file_status_description character varying(200) NOT NULL
);

CREATE TABLE broker.file (
    file_id_pk uuid PRIMARY KEY,
    service_id_fk bigint,
    created timestamp without time zone NOT NULL,
    filename character varying(500) NOT NULL,
    checksum character varying(500) NULL,
    sender_actor_id_fk bigint,
    external_file_reference character varying(500) NOT NULL,
    expiration_time timestamp without time zone NOT NULL,
    storage_provider_id_fk bigint NOT NULL,
    file_location character varying(600) NULL,
    FOREIGN KEY (service_id_fk) REFERENCES broker.service (service_id_pk),
    FOREIGN KEY (storage_provider_id_fk) REFERENCES broker.storage_provider (storage_provider_id_pk)
);

CREATE TABLE broker.file_status (
    file_status_id_pk bigserial PRIMARY KEY,
    file_id_fk uuid NOT NULL,
    file_status_description_id_fk integer NOT NULL,
    file_status_date timestamp without time zone NOT NULL,
    FOREIGN KEY (file_status_description_id_fk) REFERENCES broker.file_status_description (file_status_description_id_pk),
    FOREIGN KEY (file_id_fk) REFERENCES broker.file (file_id_pk) ON DELETE CASCADE
);

CREATE TABLE broker.file_property (
    property_id_pk bigserial PRIMARY KEY,
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
CREATE INDEX ix_file_service_id ON broker.file (service_id_fk);
CREATE INDEX ix_file_external_reference ON broker.file (external_file_reference);
CREATE INDEX ix_file_status_id ON broker.file_status (file_id_fk);
CREATE INDEX ix_actor_file_status_id ON broker.actor_file_status (file_id_fk);
CREATE INDEX ix_file_property_file_id ON broker.file_property (file_id_fk);
