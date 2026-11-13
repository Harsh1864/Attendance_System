-- Audit log: who did what, and when.
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
