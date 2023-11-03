INSERT INTO broker.file_status_description (file_status_description_id_pk, file_status_description)
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
