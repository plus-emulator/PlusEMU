-- Master of Words (ACH_Motto) is listed in the achievements window again. Migration 76 hid it as a hotel achievement,
-- but changing the motto still awards its badge and the notification, so the list never showed what the user earned.
-- Idempotent.

UPDATE `achievements` SET `visible` = 1 WHERE `group_name` = 'ACH_Motto';
