CREATE TABLE IF NOT EXISTS broker.brokershipment
(
	_id UUID GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	externalshipmentreference UUID NOT NULL,
	initiator BIGINT NOT NULL,
	initiatedtime TIMESTAMPTZ NOT NULL,
	shipmentstatus brokershipmentstatus DEFAULT 'Initiated',
	statustime TIMESTAMPTS NOT NULL
);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE broker.brokershipment TO platform_broker;


CREATE TABLE IF NOT EXISTS broker.storagereference
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	FileLocation TEXT NOT NULL
);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE broker.storagereference TO platform_notifications;


CREATE TABLE IF NOT EXISTS broker.brokerfile
(
	_id UUID GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	_shipmentId UUID REFERENCES broker.brokershipment(_id) ON DELETE CASCADE,
	externalfilerference UUID NOT NULL,
	uploader BIGINT NOT NULL,
	uploaded TIMESTAMPTZ NOT NULL,
	filestatus brokerfilestatus DEFAULT 'Uploaded',
	filestatustime TIMESTAMPTZ NOT NULL,
	_storagereferenceId BIGINT NOT NULL
);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE broker.brokerfile TO platform_notifications;


CREATE TABLE IF NOT EXISTS broker.actor
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	ActorExternalId TEXT NOT NULL
);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE broker.actor TO platform_notifications;


CREATE TABLE IF NOT EXISTS broker.actorfilestatus
(
	_actorid BIGINT PRIMARY KEY REFERENCES broker.actor(_id) ON DELETE CASCADE,
	_fileId UUID PRIMARY KEY REFERENCES broker.brokerfile(_id) ON DELETE CASCADE,
	status afstatus default 'Initiated',
	statustime TIMESTAMPTZ NOT NULL
);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE broker.actorfilestatus TO platform_notifications;


CREATE TABLE IF NOT EXISTS broker.brokershipmentmetadata
(
	_id BIGINT PRIMARY KEY REFERENCES,
	_shipmentId UUID REFERENCES broker.brokerfile(_id) ON DELETE CASCADE,
	mkey varchar(20) NOT NULL,
	mvalue varchar(200) NOT NULL
);
GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE broker.brokershipmentmetadata TO platform_notifications;


