INSERT INTO broker.resource_owner (resource_owner_id_pk, resource_owner_name, file_time_to_live)
VALUES ('0192:991825827', 'Digitaliseringsdirektoratet Avd Oslo', '1 Days');

INSERT INTO broker.storage_provider (storage_provider_id_pk, resource_owner_id_fk, created, storage_provider_type, resource_name)
VALUES (DEFAULT, '0192:991825827', NOW(), 'Altinn3Azure', 'dummy-value');

INSERT INTO broker.resource (resource_id_pk, created, organization_number, resource_owner_id_fk)
VALUES ('resource-1', NOW(), '0192:991825827', '0192:991825827');

INSERT INTO broker.resource (resource_id_pk, created, organization_number, resource_owner_id_fk)
VALUES ('resource-2', NOW(), '0192:991825827', '0192:991825827');

INSERT INTO broker.user (client_id_pk, organization_number)
VALUES ('00000000-0000-0000-0000-000000000000', '0192:991825827');				 

INSERT INTO broker.user_right (resource_id_fk, user_id_fk, user_right_description_id_fk)
VALUES ('resource-1', '00000000-0000-0000-0000-000000000000', 1);

INSERT INTO broker.user_right (resource_id_fk, user_id_fk, user_right_description_id_fk)
VALUES ('resource-2', '00000000-0000-0000-0000-000000000000', 1);

INSERT INTO broker.user (client_id_pk, organization_number)
VALUES ('11111111-1111-1111-1111-111111111111', '0192:991825832');				 

INSERT INTO broker.user_right (resource_id_fk, user_id_fk, user_right_description_id_fk)
VALUES ('resource-1', '11111111-1111-1111-1111-111111111111', 0);

INSERT INTO broker.user (client_id_pk, organization_number)
VALUES ('21111111-2111-2111-2111-211111111111', '0192:910351192');	

INSERT INTO broker.user_right (resource_id_fk, user_id_fk, user_right_description_id_fk)
VALUES ('resource-2', '11111111-1111-1111-1111-111111111111', 0);

INSERT INTO broker.user_right (resource_id_fk, user_id_fk, user_right_description_id_fk)
VALUES ('resource-2', '21111111-2111-2111-2111-211111111111', 0);