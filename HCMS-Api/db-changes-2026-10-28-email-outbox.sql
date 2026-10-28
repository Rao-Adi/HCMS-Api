-- Notification emails are no longer sent straight from the request. They are written here first and a background job sends
-- them at no more than EmailQueue:MaxPerMinute (200) a minute, because a role with many employees used to fire all its mails
-- at once and the SMTP server then refused to send any.
--
-- Status      Pending = waiting to be sent (or waiting for the next retry), Sent, Failed = gave up after MaxAttempts
-- IsDelivered TRUE once the SMTP server accepted the mail. (Accepted by the server, not read or placed in an inbox -- SMTP
--             cannot tell us more than that.)
-- Safe to run more than once.

CREATE TABLE IF NOT EXISTS EmailOutbox
(
    Id                    BIGSERIAL PRIMARY KEY,
    CompanyId             INT          NOT NULL,
    RecipientEmployeeCode VARCHAR(50),
    RecipientEmail        VARCHAR(320) NOT NULL,
    Subject               VARCHAR(500) NOT NULL,
    Body                  TEXT         NOT NULL,
    NotificationType      INT,
    RelatedEntityId       INT,
    Status                VARCHAR(20)  NOT NULL DEFAULT 'Pending',
    IsDelivered           BOOLEAN      NOT NULL DEFAULT FALSE,
    AttemptCount          INT          NOT NULL DEFAULT 0,
    LastError             TEXT,
    CreatedAt             TIMESTAMP    NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    NextAttemptAt         TIMESTAMP    NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    LastAttemptAt         TIMESTAMP,
    DeliveredAt           TIMESTAMP
);

-- The job asks "what is due" every few seconds and "how many attempts in the last minute" before each batch.
CREATE INDEX IF NOT EXISTS ix_emailoutbox_due ON EmailOutbox (Status, NextAttemptAt);
CREATE INDEX IF NOT EXISTS ix_emailoutbox_lastattempt ON EmailOutbox (LastAttemptAt);

-- To see what happened:
--   SELECT Status, IsDelivered, COUNT(*) FROM EmailOutbox GROUP BY 1, 2;
--   SELECT Id, RecipientEmail, Subject, AttemptCount, LastError FROM EmailOutbox WHERE Status = 'Failed' ORDER BY Id DESC;
-- To send a failed one again:
--   UPDATE EmailOutbox SET Status = 'Pending', AttemptCount = 0, NextAttemptAt = (NOW() AT TIME ZONE 'UTC') WHERE Id = <id>;
