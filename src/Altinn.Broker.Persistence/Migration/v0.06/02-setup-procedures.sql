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
			SET result = 'Sending', resulttime = now()
			WHERE result = 'New' 
			RETURNING notifications.emailnotifications.alternateid, _orderid, notifications.emailnotifications.toaddress)
	SELECT u.alternateid, et.subject, et.body, et.fromaddress, u.toaddress, et.contenttype 
	FROM updated u, notifications.emailtexts et
	WHERE u._orderid = et._orderid;	
END;
$BODY$;

CREATE OR REPLACE PROCEDURE notifications.updateemailstatus(_alternateid UUID, _result text, _operationid text)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
	UPDATE notifications.emailnotifications 
	SET result = _result::emailnotificationresulttype, resulttime = now(), operationid = _operationid
	WHERE alternateid = _alternateid;
END;
$BODY$;