-- Chat size, chat mode, bubble width, scroll speed and online indicator of an account. Idempotent: reruns keep stored values.

ALTER TABLE users_settings
  ADD COLUMN IF NOT EXISTS chat_font_size TINYINT NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS chat_mode TINYINT NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS chat_bubble_width TINYINT NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS chat_scroll_speed TINYINT NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS friend_online_indicator TINYINT NOT NULL DEFAULT 0;
