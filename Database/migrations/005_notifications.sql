-- In-app notifications shown under the bell icon.
CREATE TABLE IF NOT EXISTS t_anotification (
    c_notificationid SERIAL       PRIMARY KEY,
    c_empid          INTEGER      NOT NULL REFERENCES t_aemployee (c_empid) ON DELETE CASCADE,
    c_title          VARCHAR(100) NOT NULL,
    c_message        VARCHAR(500) NOT NULL,
    c_link           VARCHAR(200),
    c_isread         BOOLEAN      NOT NULL DEFAULT FALSE,
    c_createdat      TIMESTAMP    NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_anotification_emp ON t_anotification (c_empid, c_isread, c_createdat DESC);
