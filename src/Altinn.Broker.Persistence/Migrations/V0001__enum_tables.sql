INSERT INTO broker.actor_shipment_status_description (actor_shipment_status_id_pk, actor_shipment_status_description)
VALUES 
(0, 'None'),
(1, 'Initialized'),
(2, 'Finalized'),
(3, 'Downloaded'),
(4, 'Failed'),
(5, 'Deleted');

INSERT INTO broker.shipment_status (shipment_status_id_pk, shipment_status)
VALUES 
(0, 'None'),						
(1, 'Initialized'),
(2, 'Processing'),
(3, 'Ready'),
(4, 'Failed'),
(5, 'Deleted');

INSERT INTO broker.file_status (file_status_id_pk, file_status)
VALUES 
(0, 'None'),						
(1, 'Initialized'),
(2, 'Processing'),
(3, 'Ready'),
(4, 'Failed'),
(5, 'Deleted');

INSERT INTO broker.actor_file_status_description (actor_file_status_id_pk, actor_file_status_description)
VALUES
(0, 'None'),
(1, 'Initialized'),
(2, 'Uploaded'),
(3, 'Downloaded');
