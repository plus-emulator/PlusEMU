-- Wired UI style text of an account. TEXT holds the longest text a client frame can carry. Idempotent: reruns keep stored values.

ALTER TABLE users_settings
  MODIFY COLUMN IF EXISTS wired_ui_style TEXT NOT NULL DEFAULT '';
