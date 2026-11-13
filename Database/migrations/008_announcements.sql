-- Notices from admins, shown to employees between the start and end dates.
CREATE TABLE IF NOT EXISTS t_aannouncement (
    c_announcementid SERIAL        PRIMARY KEY,
    c_title          VARCHAR(120)  NOT NULL,
    c_body           VARCHAR(2000) NOT NULL,
    c_priority       VARCHAR(10)   NOT NULL DEFAULT 'Normal' CHECK (c_priority IN ('Normal', 'Important')),
    c_startdate      DATE          NOT NULL DEFAULT CURRENT_DATE,
    c_enddate        DATE,
    c_createdby      INTEGER       REFERENCES t_aemployee (c_empid) ON DELETE SET NULL,
    c_createdat      TIMESTAMP     NOT NULL DEFAULT NOW(),
    c_updatedat      TIMESTAMP     NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_aannouncement_dates CHECK (c_enddate IS NULL OR c_enddate >= c_startdate)
);

CREATE INDEX IF NOT EXISTS ix_aannouncement_dates ON t_aannouncement (c_startdate, c_enddate);
