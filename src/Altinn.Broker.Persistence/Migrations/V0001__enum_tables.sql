INSERT INTO broker.file_status_description (file_status_description_id_pk, file_status_description)
VALUES 
(0, 'Initialized'),				
(1, 'UploadStarted'),
(2, 'UploadProcessing'),
(3, 'Published'),
(4, 'Cancelled'),
(5, 'AllConfirmedDownloaded'),
(6, 'Deleted'),
(7, 'Failed');

INSERT INTO broker.actor_file_status_description (actor_file_status_id_pk, actor_file_status_description)
VALUES
(0 'Initialized'),
(1, 'DownloadStarted'),
(2, 'DownloadConfirmed');
