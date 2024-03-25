CREATE TABLE broker.altinn_resource (
    resource_id_pk character varying(80) PRIMARY KEY,
    created timestamp without time zone NOT NULL,
    max_file_transfer_size bigint,
    organization_number character varying(14) NOT NULL,
    service_owner_id_fk character varying(14) NOT NULL,
    FOREIGN KEY (service_owner_id_fk) REFERENCES broker.service_owner (service_owner_id_pk)
);