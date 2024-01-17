CREATE TABLE broker.user (
    client_id_pk character varying(36) NOT NULL PRIMARY KEY,
    organization_number character varying(14) NOT NULL,
    CONSTRAINT organization_number_format CHECK (organization_number ~ '^\d{4}:\d{9}$')
);

CREATE TABLE broker.user_right_description (
	user_right_description_id_pk integer PRIMARY KEY,
	user_right_description character varying(30) NOT NULL
);

CREATE TABLE broker.user_right (
    resource_id_fk bigint,
    user_id_fk character varying(36) NOT NULL,
    user_right_description_id_fk int,
    PRIMARY KEY (resource_id_fk, user_id_fk, user_right_description_id_fk),
    FOREIGN KEY (resource_id_fk) REFERENCES broker.resource (resource_id_pk),
    FOREIGN KEY (user_id_fk) REFERENCES broker.user (client_id_pk)
);

CREATE INDEX ix_user_client_id ON broker.user (client_id_pk);