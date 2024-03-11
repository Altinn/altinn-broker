CREATE TABLE broker.altinn_resource (
    resource_id_pk character varying(80) PRIMARY KEY,
    created timestamp without time zone NOT NULL,
    max_file_transfer_size bigint NOT NULL,
    organization_number character varying(14) NOT NULL,
    service_owner_id character varying(14) NOT NULL
);