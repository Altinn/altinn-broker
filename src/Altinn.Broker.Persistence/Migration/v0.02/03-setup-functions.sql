DROP FUNCTION IF EXISTS notifications.getorders_pastsendtime_updatestatus();
CREATE OR REPLACE FUNCTION notifications.getorders_pastsendtime_updatestatus()
    RETURNS TABLE(notificationorders text)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
RETURN QUERY
	UPDATE notifications.orders
	SET processedstatus = 'Processing'
	WHERE _id IN (select _id 
				 from notifications.orders 
				 where processedstatus = 'Registered' 
				 and requestedsendtime <= now()
				 limit 50)
	RETURNING cast(notificationorder as text) AS notificationorders;
END;
$BODY$;

CREATE OR REPLACE FUNCTION notifications.getemails_statusnew_updatestatus()
RETURNS TABLE(
    alternateid UUID, 
    subject text,
	body text,
	fromaddress text,
	toaddress text,
	contenttype text
) 
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
RETURN query 
	WITH updated AS (
		UPDATE notifications.emailnotifications
			SET result = 'Sending'
			WHERE result = 'New' 
			RETURNING notifications.emailnotifications.alternateid, _orderid, notifications.emailnotifications.toaddress)
	SELECT u.alternateid, et.subject, et.body, et.fromaddress, u.toaddress, et.contenttype 
	FROM updated u, notifications.emailtexts et
	WHERE u._orderid = et._orderid;	
END;
$BODY$;