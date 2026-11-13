-- Company holidays. The absentee job does not mark anyone absent on these days,
-- and they are not counted as leave days.
CREATE TABLE IF NOT EXISTS t_aholiday (
    c_holidayid SERIAL       PRIMARY KEY,
    c_date      DATE         NOT NULL UNIQUE,
    c_name      VARCHAR(100) NOT NULL,
    c_createdby INTEGER      REFERENCES t_aemployee (c_empid) ON DELETE SET NULL,
    c_createdat TIMESTAMP    NOT NULL DEFAULT NOW()
);
