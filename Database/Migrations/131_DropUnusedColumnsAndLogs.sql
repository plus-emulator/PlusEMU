-- Drops columns and log tables that neither the emulator nor the CMS reads or writes.
-- wired_items stays on hotels that have it: it holds their old Wired settings.

-- Gift achievements count gifts, quests keep their progress in user_quests and forum post counts come from the
-- forum messages.
ALTER TABLE `users` DROP COLUMN IF EXISTS `vip`;
ALTER TABLE `user_statistics`
    DROP COLUMN IF EXISTS `GiftsGiven`, DROP COLUMN IF EXISTS `GiftsReceived`,
    DROP COLUMN IF EXISTS `quest_progress`, DROP COLUMN IF EXISTS `forum_posts`;
ALTER TABLE `room_models` DROP COLUMN IF EXISTS `public_items`;

-- Logs of the removed housekeeping and furni editor pages.
DROP TABLE IF EXISTS `housekeeping_log`;
DROP TABLE IF EXISTS `housekeeping_online_peaks`;
DROP TABLE IF EXISTS `furni_editor_log`;
