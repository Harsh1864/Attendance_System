-- Attendance System - PostgreSQL schema
--
-- Reconstructed from the queries in Services/ and Controllers/. Column
-- names and nullability match how the application reads and writes them.
-- Compare with your live database before using this on an existing install.
--
-- Usage:  psql -h <host> -p <port> -U <user> -d <database> -f Database/schema.sql

CREATE TABLE IF NOT EXISTS t_aemployee (
    c_empid        SERIAL       PRIMARY KEY,
    c_name         VARCHAR(50)  NOT NULL,
    c_email        VARCHAR(100) NOT NULL UNIQUE,
    c_password     VARCHAR(255) NOT NULL,          -- PBKDF2 hash (see Services/PasswordHasher.cs)
    c_gender       VARCHAR(10)  NOT NULL,
    c_status       VARCHAR(10)  NOT NULL DEFAULT 'Active'
                   CHECK (c_status IN ('Active', 'Inactive')),
    c_profileimage VARCHAR(255),
    c_role         VARCHAR(20)  NOT NULL DEFAULT 'Employee'
                   CHECK (c_role IN ('Admin', 'Employee'))
);

-- Case-insensitive email lookups are used at login.
CREATE UNIQUE INDEX IF NOT EXISTS ux_aemployee_email_lower ON t_aemployee (LOWER(c_email));

CREATE TABLE IF NOT EXISTS t_aattendance (
    c_attendid     SERIAL       PRIMARY KEY,
    c_empid        INTEGER      NOT NULL REFERENCES t_aemployee (c_empid) ON DELETE CASCADE,
    c_attenddate   DATE         NOT NULL DEFAULT CURRENT_DATE,
    c_clockinhour  INTEGER      CHECK (c_clockinhour  BETWEEN 0 AND 23),
    c_clockinmin   INTEGER      CHECK (c_clockinmin   BETWEEN 0 AND 59),
    c_clockouthour INTEGER      CHECK (c_clockouthour BETWEEN 0 AND 23),
    c_clockoutmin  INTEGER      CHECK (c_clockoutmin  BETWEEN 0 AND 59),
    c_worktype     VARCHAR(10),                    -- Remote / Office / Field
    c_tasktype     VARCHAR(100),                   -- comma-separated task names
    c_workinghour  INTEGER      CHECK (c_workinghour > 0),
    c_attendstatus VARCHAR(10)  NOT NULL DEFAULT 'Regular'
                   CHECK (c_attendstatus IN ('Regular', 'LateIn', 'EarlyOut', 'LateEarly', 'Absent')),
    CONSTRAINT ux_aattendance_emp_date UNIQUE (c_empid, c_attenddate)
);

CREATE INDEX IF NOT EXISTS ix_aattendance_date ON t_aattendance (c_attenddate);

-- The application treats c_empid = 1 as the administrator account and
-- excludes it from reports. Create it first, then change the password
-- from the profile page. The plain-text value below is upgraded to a
-- hash automatically on first login.
INSERT INTO t_aemployee (c_empid, c_name, c_email, c_password, c_gender, c_status, c_role)
VALUES (1, 'Administrator', 'admin@example.com', 'ChangeMe123', 'Other', 'Active', 'Admin')
ON CONFLICT (c_empid) DO NOTHING;

SELECT setval(pg_get_serial_sequence('t_aemployee', 'c_empid'),
              GREATEST((SELECT MAX(c_empid) FROM t_aemployee), 1));

-- ---------------------------------------------------------------------------
-- Audit log (migration 002)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS t_aaudit (
    c_auditid    SERIAL       PRIMARY KEY,
    c_actorid    INTEGER,
    c_actoremail VARCHAR(100),
    c_action     VARCHAR(50)  NOT NULL,
    c_entity     VARCHAR(30)  NOT NULL,
    c_entityid   INTEGER,
    c_details    TEXT,
    c_ipaddress  VARCHAR(45),
    c_createdat  TIMESTAMP    NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_aaudit_createdat ON t_aaudit (c_createdat DESC);
CREATE INDEX IF NOT EXISTS ix_aaudit_entity    ON t_aaudit (c_entity, c_entityid);
