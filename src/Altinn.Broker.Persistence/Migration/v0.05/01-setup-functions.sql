CREATE OR REPLACE FUNCTION notifications.insertorder(_alternateid UUID, _creatorname TEXT, _sendersreference TEXT, _created TIMESTAMPTZ, _requestedsendtime TIMESTAMPTZ, _notificationorder JSONB)
RETURNS BIGINT
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
_orderid BIGINT;
BEGIN
	INSERT INTO notifications.orders(alternateid, creatorname, sendersreference, created, requestedsendtime, processed, notificationorder) 
	VALUES (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _created, _notificationorder)
   RETURNING _id INTO _orderid;
   
   RETURN _orderid;
END;
$BODY$;

CREATE OR REPLACE FUNCTION notifications.getorder_includestatus(_alternateid UUID, _creatorname TEXT)
RETURNS TABLE (
    alternateid UUID,
    creatorname TEXT,
    sendersreference TEXT,
    created TIMESTAMPTZ,
    requestedsendtime TIMESTAMPTZ,
    processed TIMESTAMPTZ,
    processedstatus TEXT,
    generatedEmailCount BIGINT,
    succeededEmailCount BIGINT
)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY
    SELECT 
        orders.alternateid,
        orders.creatorname,
        orders.sendersreference,
        orders.created,
        orders.requestedsendtime,
        orders.processed,
        orders.processedstatus :: text,
        emails.generatedEmailCount,
        emails.succeededEmailCount
    FROM
        notifications.orders AS orders
    LEFT JOIN
    (
        SELECT 
            _orderid,
            SUM(CASE WHEN result = 'Succeeded' THEN 1 ELSE 0 END) AS succeededEmailCount,
            COUNT(1) AS generatedEmailCount
        FROM 
            notifications.emailnotifications
        GROUP BY
            _orderid
    ) AS emails
    ON
        emails._orderid = orders._id
    WHERE 
        orders.alternateid = _alternateid and orders.creatorname = _creatorname;
END;
$BODY$;