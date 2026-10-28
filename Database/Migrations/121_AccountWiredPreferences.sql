-- Account preferences of the Wired menu, kept per user. Idempotent: reruns keep stored values.

ALTER TABLE users_settings
  ADD COLUMN IF NOT EXISTS wired_menu_button TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS wired_inspect_button TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS wired_play_test TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS wired_whisper_disabled TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS wired_show_all_notifications TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS wired_ui_style VARCHAR(64) NOT NULL DEFAULT '';
