-- Mystery boxes. A user has one box colour and holds at most one key colour; an empty colour means none.
-- A box opens with a guest's key of the box colour and gives both users a prize from mystery_box_prizes. Idempotent.

CREATE TABLE IF NOT EXISTS `user_mystery_boxes` (
  `user_id` int(11) NOT NULL,
  `box_color` varchar(16) NOT NULL DEFAULT '',
  `key_color` varchar(16) NOT NULL DEFAULT '',
  PRIMARY KEY (`user_id`),
  CONSTRAINT `fk_user_mystery_boxes_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `mystery_box_prizes` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `furniture_id` int(11) unsigned NOT NULL,
  `weight` int(11) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `furniture_id` (`furniture_id`),
  CONSTRAINT `fk_mystery_box_prizes_furniture_id` FOREIGN KEY (`furniture_id`) REFERENCES `furniture` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- The box cannot be traded, recycled or sold, just like Habbo.
UPDATE `furniture`
SET `interaction_type` = 'mystery_box', `allow_trade` = 0, `allow_recycle` = 0, `allow_marketplace_sell` = 0
WHERE `item_name` = 'mystery_box';
