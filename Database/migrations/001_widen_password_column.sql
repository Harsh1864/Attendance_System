-- Passwords are now stored as PBKDF2 hashes in the form
--   pbkdf2$<iterations>$<base64 salt>$<base64 hash>
-- which is about 80 characters. Make sure the column can hold it.
-- Existing plain-text passwords are upgraded automatically on next login.

ALTER TABLE t_aemployee
    ALTER COLUMN c_password TYPE VARCHAR(255);
