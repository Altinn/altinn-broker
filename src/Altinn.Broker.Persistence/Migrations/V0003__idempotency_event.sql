CREATE TABLE broker.idempotency_event (
    idempotency_event_id_pk character varying(80) PRIMARY KEY,
    created timestamp without time zone NOT NULL
);