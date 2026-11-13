-- Leave requests. Approved leave days are not marked Absent by the absentee job.
CREATE TABLE IF NOT EXISTS t_aleave (
    c_leaveid    SERIAL        PRIMARY KEY,
    c_empid      INTEGER       NOT NULL REFERENCES t_aemployee (c_empid) ON DELETE CASCADE,
    c_fromdate   DATE          NOT NULL,
    c_todate     DATE          NOT NULL,
    c_leavetype  VARCHAR(20)   NOT NULL CHECK (c_leavetype IN ('Casual', 'Sick', 'Earned', 'Unpaid')),
    c_halfday    BOOLEAN       NOT NULL DEFAULT FALSE,
    c_days       NUMERIC(4, 1) NOT NULL CHECK (c_days > 0),
    c_reason     VARCHAR(500),
    c_status     VARCHAR(10)   NOT NULL DEFAULT 'Pending'
                 CHECK (c_status IN ('Pending', 'Approved', 'Rejected', 'Cancelled')),
    c_reviewedby INTEGER       REFERENCES t_aemployee (c_empid) ON DELETE SET NULL,
    c_reviewnote VARCHAR(500),
    c_reviewedat TIMESTAMP,
    c_createdat  TIMESTAMP     NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_aleave_dates CHECK (c_todate >= c_fromdate)
);

CREATE INDEX IF NOT EXISTS ix_aleave_emp    ON t_aleave (c_empid, c_fromdate);
CREATE INDEX IF NOT EXISTS ix_aleave_status ON t_aleave (c_status);
