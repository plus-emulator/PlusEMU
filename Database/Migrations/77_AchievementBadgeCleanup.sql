-- On habbo.com a user holds one badge per achievement: the badge of the highest level reached. The old engine only
-- removed the previous level's badge from the loaded inventory, so the user_badges row of every earlier level stayed
-- and came back on the next login. Those earlier badges are removed here, keeping the highest level's badge. When the
-- kept badge is not worn and an earlier one was, the kept badge takes the lowest of their slots.
-- Running it again changes nothing.
DROP TEMPORARY TABLE IF EXISTS `migration_77_held`;
CREATE TEMPORARY TABLE `migration_77_held` AS
SELECT ub.`id`, ub.`user_id`, ub.`badge_slot`, a.`group_name`, a.`level`
FROM `user_badges` ub
JOIN `achievements` a ON ub.`badge_id` = COALESCE(a.`badge_code`, CONCAT(a.`group_name`, a.`level`));

-- The highest level held of every achievement a user holds more than one badge of.
DROP TEMPORARY TABLE IF EXISTS `migration_77_top`;
CREATE TEMPORARY TABLE `migration_77_top` AS
SELECT `user_id`, `group_name`, MAX(`level`) AS `level`
FROM `migration_77_held`
GROUP BY `user_id`, `group_name`
HAVING COUNT(*) > 1;

-- MariaDB opens a temporary table once per statement, so the badges to keep, the slots to carry and the badges to
-- remove are worked out in separate tables.
DROP TEMPORARY TABLE IF EXISTS `migration_77_keep`;
CREATE TEMPORARY TABLE `migration_77_keep` AS
SELECT h.`id`, h.`user_id`, h.`group_name`
FROM `migration_77_held` h
JOIN `migration_77_top` t ON t.`user_id` = h.`user_id` AND t.`group_name` = h.`group_name` AND t.`level` = h.`level`;

DROP TEMPORARY TABLE IF EXISTS `migration_77_slot`;
CREATE TEMPORARY TABLE `migration_77_slot` AS
SELECT h.`user_id`, h.`group_name`, MIN(h.`badge_slot`) AS `badge_slot`
FROM `migration_77_held` h
JOIN `migration_77_top` t ON t.`user_id` = h.`user_id` AND t.`group_name` = h.`group_name` AND h.`level` < t.`level`
WHERE h.`badge_slot` > 0
GROUP BY h.`user_id`, h.`group_name`;

DROP TEMPORARY TABLE IF EXISTS `migration_77_remove`;
CREATE TEMPORARY TABLE `migration_77_remove` AS
SELECT h.`id`
FROM `migration_77_held` h
JOIN `migration_77_top` t ON t.`user_id` = h.`user_id` AND t.`group_name` = h.`group_name` AND h.`level` < t.`level`;

UPDATE `user_badges` ub
JOIN `migration_77_keep` k ON k.`id` = ub.`id`
JOIN `migration_77_slot` s ON s.`user_id` = k.`user_id` AND s.`group_name` = k.`group_name`
SET ub.`badge_slot` = s.`badge_slot`
WHERE ub.`badge_slot` = 0;

DELETE ub FROM `user_badges` ub
JOIN `migration_77_remove` r ON r.`id` = ub.`id`;

DROP TEMPORARY TABLE `migration_77_held`;
DROP TEMPORARY TABLE `migration_77_top`;
DROP TEMPORARY TABLE `migration_77_keep`;
DROP TEMPORARY TABLE `migration_77_slot`;
DROP TEMPORARY TABLE `migration_77_remove`;
