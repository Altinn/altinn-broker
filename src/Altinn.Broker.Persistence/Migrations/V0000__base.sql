-- Create schema
CREATE SCHEMA broker;

-- Create tables
CREATE TABLE broker.actor (
    actor_id serial PRIMARY KEY,
    actor_external_id character varying(500) NOT NULL
);

CREATE TABLE broker.file_status (
    file_status_id integer PRIMARY KEY,
    file_status character varying(200) NOT NULL
);

CREATE TABLE broker.storage_reference (
    storage_reference_id bigint PRIMARY KEY,
    file_location character varying(600) NOT NULL
);

CREATE TABLE broker.shipment_status (
    shipment_status_id integer PRIMARY KEY,
    shipment_status character varying(200) NOT NULL
);

CREATE TABLE broker.shipment (
    shipment_id uuid PRIMARY KEY,
    external_shipment_reference uuid NOT NULL,
    uploader_actor_id bigint NOT NULL,
    initiated timestamp without time zone NOT NULL,
    shipment_status_id integer NOT NULL,
    FOREIGN KEY (uploader_actor_id) REFERENCES broker.actor (actor_id) ON DELETE CASCADE,
    FOREIGN KEY (shipment_status_id) REFERENCES broker.shipment_status (shipment_status_id)
);

CREATE TABLE broker.shipment_metadata (
    metadata_id bigserial PRIMARY KEY,
    shipment_id uuid NOT NULL,
    key character varying(50) NOT NULL,
    value character varying(300) NOT NULL,
    FOREIGN KEY (shipment_id) REFERENCES broker.shipment (shipment_id) ON DELETE CASCADE
);

CREATE TABLE broker.file (
    file_id uuid PRIMARY KEY,
    external_file_reference uuid NOT NULL,
    shipment_id uuid NOT NULL,
    file_status_id integer NOT NULL,
    last_status_update timestamp without time zone,
    uploaded timestamp without time zone NOT NULL,
    storage_reference_id bigint NOT NULL,
    FOREIGN KEY (file_status_id) REFERENCES broker.file_status (file_status_id),
    FOREIGN KEY (shipment_id) REFERENCES broker.shipment (shipment_id),
    FOREIGN KEY (storage_reference_id) REFERENCES broker.storage_reference (storage_reference_id)
);

CREATE TABLE broker.actor_file_status_description (
    actor_file_status_id integer PRIMARY KEY,
    actor_file_status_description character varying(200) NOT NULL
);

CREATE TABLE broker.actor_file_status (
    actor_id bigint NOT NULL,
    file_id uuid NOT NULL,
    actor_file_status_id integer NOT NULL,
    actor_file_status_date timestamp without time zone NOT NULL,
    PRIMARY KEY (actor_id, file_id),
    FOREIGN KEY (actor_id) REFERENCES broker.actor (actor_id) ON DELETE CASCADE,
    FOREIGN KEY (actor_file_status_id) REFERENCES broker.actor_file_status_description (actor_file_status_id),
    FOREIGN KEY (file_id) REFERENCES broker.file (file_id)
);

CREATE TABLE broker.actor_shipment_status_description (
    actor_shipment_status_id integer PRIMARY KEY,
    actor_shipment_status_description character varying(200) NOT NULL
);

CREATE TABLE broker.actor_shipment_status (
    actor_id bigint NOT NULL,
    shipment_id uuid NOT NULL,
    actor_shipment_status_id integer NOT NULL,
    actor_shipment_status_date timestamp without time zone NOT NULL,
    PRIMARY KEY (actor_id, shipment_id),
    FOREIGN KEY (actor_id) REFERENCES broker.actor (actor_id) ON DELETE CASCADE,
    FOREIGN KEY (actor_shipment_status_id) REFERENCES broker.actor_shipment_status_description (actor_shipment_status_id),
    FOREIGN KEY (shipment_id) REFERENCES broker.shipment (shipment_id)
);

-- Create indexes
CREATE INDEX ix_file_external_reference ON broker.file (external_file_reference);
CREATE INDEX ix_shipment_external_reference ON broker.shipment (external_shipment_reference);
