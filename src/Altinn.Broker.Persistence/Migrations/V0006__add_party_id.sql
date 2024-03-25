CREATE TABLE broker.party (
    organization_number_pk character varying(14) PRIMARY KEY,
    party_id character varying(20) NOT NULL,
    created timestamp without time zone NOT NULL
);