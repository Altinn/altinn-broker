CREATE TABLE broker.service_owner (
    service_owner_sub_pk character varying(500) NOT NULL PRIMARY KEY,
    service_owner_name character varying(500) NOT NULL,
    azure_storage_account_connection_string character varying(500) NULL
);
