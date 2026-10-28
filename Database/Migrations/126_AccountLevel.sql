-- The account level shown on the profile, read by @level and the User Level condition. A room's Wired execution cost cap is
-- 312.5 x (owner level + 2). Every account starts at level 8, which gives its rooms a 3125 cap. Idempotent.

ALTER TABLE users
  ADD COLUMN IF NOT EXISTS account_level INT NOT NULL DEFAULT 8;
