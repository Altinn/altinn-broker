CREATE TABLE broker.party (
    organization_number character varying(80) PRIMARY KEY,
    party_id character varying(80) NOT NULL,
    created timestamp without time zone NOT NULL
);