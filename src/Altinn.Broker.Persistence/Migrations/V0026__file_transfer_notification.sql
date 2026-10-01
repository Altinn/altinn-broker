ALTER TABLE broker.file_transfer ADD COLUMN has_notification boolean NOT NULL DEFAULT false;

CREATE TABLE broker.file_transfer_notification (
    file_transfer_notification_id_pk uuid PRIMARY KEY,
    file_transfer_id_fk uuid NOT NULL,
    actor_id_fk bigint NULL,
    custom_recipient text NULL,
    notification_template integer NOT NULL,
    notification_channel integer NOT NULL,
    requested_send_time timestamp without time zone NOT NULL,
    created timestamp without time zone NOT NULL,
    is_reminder boolean NOT NULL DEFAULT false,
    notification_sent timestamp without time zone NULL,
    notification_address character varying(500) NULL,
    notification_order_id uuid NULL,
    shipment_id uuid NULL,
    order_request text NULL,
    CONSTRAINT file_transfer_notification_file_transfer_fk
        FOREIGN KEY (file_transfer_id_fk) REFERENCES broker.file_transfer (file_transfer_id_pk) ON DELETE CASCADE,
    CONSTRAINT file_transfer_notification_actor_fk
        FOREIGN KEY (actor_id_fk) REFERENCES broker.actor (actor_id_pk) ON DELETE CASCADE,
    -- A row is either for a real file-transfer actor or an arbitrary custom recipient, never both or neither.
    CONSTRAINT file_transfer_notification_recipient_xor
        CHECK ((actor_id_fk IS NOT NULL) <> (custom_recipient IS NOT NULL))
);

CREATE INDEX file_transfer_notification_file_transfer_id_idx
    ON broker.file_transfer_notification (file_transfer_id_fk);

CREATE UNIQUE INDEX file_transfer_notification_actor_recipient_idx
    ON broker.file_transfer_notification (file_transfer_id_fk, actor_id_fk, is_reminder)
    WHERE actor_id_fk IS NOT NULL;

CREATE UNIQUE INDEX file_transfer_notification_custom_recipient_idx
    ON broker.file_transfer_notification (file_transfer_id_fk, custom_recipient, is_reminder)
    WHERE custom_recipient IS NOT NULL;

CREATE TABLE broker.notification_template (
    notification_template_id_pk integer PRIMARY KEY,
    notification_template integer NOT NULL,
    language character varying(10) NOT NULL,
    email_subject character varying(512) NULL,
    email_body character varying(10000) NULL,
    sms_body character varying(2144) NULL,
    reminder_email_subject character varying(512) NULL,
    reminder_email_body character varying(10000) NULL,
    reminder_sms_body character varying(2144) NULL,
    CONSTRAINT notification_template_type_language_uq UNIQUE (notification_template, language)
);

INSERT INTO broker.notification_template (
    notification_template_id_pk, notification_template, language,
    email_subject, email_body, sms_body,
    reminder_email_subject, reminder_email_body, reminder_sms_body
) VALUES
    (
        0, 1, 'nb',
        'En ny filoverføring er tilgjengelig i Altinn for $recipientName$',
        'Hei. $recipientName$ $recipientNumber$ har mottatt filen $fileName$ fra $sendersName$ gjennom Altinn Formidling. (For å se denne filoverføringen kreves tilgang til $resourceName$). {textToken}Logg inn i Altinn for å se filoverføringen.',
        'Hei. $recipientName$ $recipientNumber$ har mottatt filen $fileName$ fra $sendersName$ gjennom Altinn Formidling. (For å se denne filoverføringen kreves tilgang til $resourceName$). {textToken}Logg inn i Altinn for å se filoverføringen.',
        'Påminnelse - en ny filoverføring er tilgjengelig i Altinn for $recipientName$',
        'Hei. Dette er en påminnelse om at $recipientName$ har mottatt filen $fileName$ fra $sendersName$ gjennom Altinn Formidling. (For å se denne filoverføringen kreves tilgang til $resourceName$). {textToken}Logg inn i Altinn for å se filoverføringen.',
        'Hei. Dette er en påminnelse om at $recipientName$ har mottatt filen $fileName$ fra $sendersName$ gjennom Altinn Formidling. (For å se denne filoverføringen kreves tilgang til $resourceName$). {textToken}Logg inn i Altinn for å se filoverføringen.'
    ),
    (
        1, 1, 'nn',
        'Ei ny filoverføring er tilgjengeleg i Altinn for $recipientName$',
        'Hei. $recipientName$ $recipientNumber$ har motteke fila $fileName$ frå $sendersName$ gjennom Altinn Formidling. (For å sjå denne filoverføringa krevst tilgang til $resourceName$). {textToken}Logg inn i Altinn for å sjå filoverføringa.',
        'Hei. $recipientName$ $recipientNumber$ har motteke fila $fileName$ frå $sendersName$ gjennom Altinn Formidling. (For å sjå denne filoverføringa krevst tilgang til $resourceName$). {textToken}Logg inn i Altinn for å sjå filoverføringa.',
        'Påminning - ei ny filoverføring er tilgjengeleg i Altinn for $recipientName$',
        'Hei. Dette er ei påminning om at $recipientName$ har motteke fila $fileName$ frå $sendersName$ gjennom Altinn Formidling. (For å sjå denne filoverføringa krevst tilgang til $resourceName$). {textToken}Logg inn i Altinn for å sjå filoverføringa.',
        'Hei. Dette er ei påminning om at $recipientName$ har motteke fila $fileName$ frå $sendersName$ gjennom Altinn Formidling. (For å sjå denne filoverføringa krevst tilgang til $resourceName$). {textToken}Logg inn i Altinn for å sjå filoverføringa.'
    ),
    (
        2, 1, 'en',
        'A new file transfer is available in Altinn for $recipientName$',
        'Hi. $recipientName$ $recipientNumber$ has received the file $fileName$ from $sendersName$ through Altinn Broker. (Access to $resourceName$ is required to view this file transfer). {textToken}Log in to Altinn to view the file transfer.',
        'Hi. $recipientName$ $recipientNumber$ has received the file $fileName$ from $sendersName$ through Altinn Broker. (Access to $resourceName$ is required to view this file transfer). {textToken}Log in to Altinn to view the file transfer.',
        'Reminder - a new file transfer is available in Altinn for $recipientName$',
        'Hi. This is a reminder that $recipientName$ has received the file $fileName$ from $sendersName$ through Altinn Broker. (Access to $resourceName$ is required to view this file transfer). {textToken}Log in to Altinn to view the file transfer.',
        'Hi. This is a reminder that $recipientName$ has received the file $fileName$ from $sendersName$ through Altinn Broker. (Access to $resourceName$ is required to view this file transfer). {textToken}Log in to Altinn to view the file transfer.'
    );
