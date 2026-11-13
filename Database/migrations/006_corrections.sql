-- Employee requests to correct their own attendance (missed check-in/out, wrong time).
CREATE TABLE IF NOT EXISTS t_acorrection (
    c_correctionid SERIAL       PRIMARY KEY,
    c_empid        INTEGER      NOT NULL REFERENCES t_aemployee (c_empid) ON DELETE CASCADE,
    c_date         DATE         NOT NULL,
    c_clockin      VARCHAR(5)   NOT NULL,              -- HH:mm
    c_clockout     VARCHAR(5),                         -- HH:mm, optional only for today
    c_worktype     VARCHAR(10)  NOT NULL,
    c_tasktype     VARCHAR(100),
    c_reason       VARCHAR(300) NOT NULL,
    c_status       VARCHAR(10)  NOT NULL DEFAULT 'Pending'
                   CHECK (c_status IN ('Pending', 'Approved', 'Rejected', 'Cancelled')),
    c_reviewedby   INTEGER      REFERENCES t_aemployee (c_empid) ON DELETE SET NULL,
    c_reviewnote   VARCHAR(300),
    c_reviewedat   TIMESTAMP,
    c_attendid     INTEGER      REFERENCES t_aattendance (c_attendid) ON DELETE SET NULL,
    c_createdat    TIMESTAMP    NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_acorrection_emp    ON t_acorrection (c_empid, c_date);
CREATE INDEX IF NOT EXISTS ix_acorrection_status ON t_acorrection (c_status);

-- Only one open request per employee per day.
CREATE UNIQUE INDEX IF NOT EXISTS ux_acorrection_pending
    ON t_acorrection (c_empid, c_date) WHERE c_status = 'Pending';
