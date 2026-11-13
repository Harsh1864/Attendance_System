-- Departments, and each employee's department.
CREATE TABLE IF NOT EXISTS t_adepartment (
    c_departmentid SERIAL       PRIMARY KEY,
    c_name         VARCHAR(50)  NOT NULL,
    c_description  VARCHAR(200),
    c_createdat    TIMESTAMP    NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_adepartment_name ON t_adepartment (LOWER(c_name));

ALTER TABLE t_aemployee
    ADD COLUMN IF NOT EXISTS c_departmentid INTEGER
        REFERENCES t_adepartment (c_departmentid) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_aemployee_department ON t_aemployee (c_departmentid);
