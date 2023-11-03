INSERT INTO broker.file_status_description (file_status_description_id_pk, file_status_description)
VALUES 
(0, 'Initialized'),						
(1, 'AwaitingUpload'),
(2, 'UploadInProgress'),
(3, 'AwaitingUploadProcessing'),
(4, 'UploadedAndProcessed'),
(5, 'Published'),
(6, 'Cancelled'),
(7, 'Downloaded'),
(8, 'AllConfirmedDownloaded'),
(9, 'Deleted'),
(10, 'Failed');

INSERT INTO broker.actor_file_status_description (actor_file_status_id_pk, actor_file_status_description)
VALUES
(0, 'None'),
(1, 'Initialized'),
(2, 'Uploaded'),
(3, 'Downloaded');
