CREATE OR REPLACE FUNCTION notifications.getemailrecipients(_alternateid uuid)
RETURNS TABLE(
    recipientid text, 
    toaddress text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _alternateid);
BEGIN
RETURN query 
	SELECT e.recipientid, e.toaddress
	FROM notifications.emailnotifications e
	WHERE e._orderid = __orderid;
END;
$BODY$;