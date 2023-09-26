CREATE OR REPLACE PROCEDURE notifications.insertemailnotification(_orderid uuid, 
																  _alternateid uuid, 
																  _recipientid TEXT, 
																  _toaddress TEXT, 
																  _result text, 
																  _resulttime timestamptz, 
																  _expirytime timestamptz
																 )
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _orderid);
BEGIN

INSERT INTO notifications.emailnotifications(_orderid, alternateid, recipientid, toaddress, result, resulttime, expirytime)
	VALUES (__orderid, _alternateid, _recipientid, _toaddress, _result::emailnotificationresulttype, _resulttime, _expirytime);
END;
$BODY$