CREATE TABLE IF NOT EXISTS notifications.emailnotifications
(
	_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	_orderid BIGINT REFERENCES notifications.orders(_id) ON DELETE CASCADE NOT NULL,
	alternateid UUID UNIQUE NOT NULL,
	recipientid TEXT,
	toaddress TEXT NOT NULL,
	result emailnotificationresulttype NOT NULL,
	resulttime TIMESTAMPTZ NOT NULL,
	expirytime TIMESTAMPTZ NOT NULL	
);

GRANT SELECT,INSERT,UPDATE,DELETE ON TABLE notifications.emailnotifications TO platform_notifications;
