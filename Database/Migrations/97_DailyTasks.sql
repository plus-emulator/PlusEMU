-- Daily tasks (AIR WIN63-202610151745-318700621, quest/dailytasks/DailyTasksController.as; task data
-- package_180/class_2919.as:39-61, reward package_180/class_4315.as:17-24).
-- daily_tasks: code names the texts dailytask.<code>.name/.desc/.hint and the image <image.library.dailytasks.url><code><image_version>.png;
-- action_type is the server progress action (reward track action names, plus show_sign); a non-empty parameters list
-- (comma separated) narrows it, e.g. to hand item ids. bonus tasks open once all regular tasks of the day are done.
-- daily_task_rewards follow the client's product icon (window/widgets/ProductIconWidget.as:91-160): product_item_type_id 8
-- is a currency whose type is reward_type_id (-1 credits, 0 duckets, 5 diamonds), 4 a badge whose code is reward_type_id.
-- user_daily_tasks: one row per user, day (server time zone) and task; its id is the task id the client claims.
-- The seed uses Habbo's task codes (texts and images exist on Habbo) with the actions their hints describe; repeats and
-- duckets are our choice. Idempotent.

CREATE TABLE IF NOT EXISTS `daily_tasks` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `code` varchar(64) NOT NULL,
  `action_type` varchar(64) NOT NULL,
  `parameters` varchar(512) NOT NULL DEFAULT '',
  `quest_type_code` varchar(64) NOT NULL DEFAULT '',
  `required_repeats` int(11) NOT NULL DEFAULT 1,
  `image_version` varchar(32) NOT NULL DEFAULT '',
  `catalog_name` varchar(100) NOT NULL DEFAULT '',
  `bonus` tinyint(1) NOT NULL DEFAULT 0,
  `weight` int(11) NOT NULL DEFAULT 1,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `daily_task_rewards` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `task_id` int(11) NOT NULL,
  `product_item_type_id` smallint(6) NOT NULL DEFAULT 8,
  `reward_type_id` varchar(64) NOT NULL,
  `extra_params` varchar(255) NOT NULL DEFAULT '',
  `amount` int(11) NOT NULL DEFAULT 1,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `task_id` (`task_id`),
  CONSTRAINT `fk_daily_task_rewards_task_id` FOREIGN KEY (`task_id`) REFERENCES `daily_tasks` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `user_daily_tasks` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `user_id` int(11) NOT NULL,
  `task_id` int(11) NOT NULL,
  `day` date NOT NULL,
  `expires_at` datetime(6) NOT NULL,
  `progress` int(11) NOT NULL DEFAULT 0,
  `status` tinyint(4) NOT NULL DEFAULT 0,
  `completed_at` datetime(6) DEFAULT NULL,
  `claimed_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `user_day_task` (`user_id`,`day`,`task_id`),
  KEY `user_status_day` (`user_id`,`status`,`day`),
  KEY `task_id` (`task_id`),
  CONSTRAINT `fk_user_daily_tasks_task_id` FOREIGN KEY (`task_id`) REFERENCES `daily_tasks` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_user_daily_tasks_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO `daily_tasks` (`id`, `code`, `action_type`, `parameters`, `quest_type_code`, `required_repeats`, `image_version`, `catalog_name`, `bonus`, `weight`, `enabled`) VALUES
(1,'1741850663726_G','chat_with_someone','','chat_with_someone',10,'','',0,1,1),
(2,'1741855402906_G','dance','','dance',5,'','',0,1,1),
(3,'1741855705682_G','dance','','dance',3,'','',0,1,1),
(4,'1741863086174_G','switch_item_state','','switch_item_state',10,'','',0,1,1),
(5,'1741864304637_G','swim','','swim',1,'','',0,1,1),
(6,'1741864788753_G','wave','','wave',3,'','',0,1,1),
(7,'1742193348839_G','find_hand_item','1,8,10,11,12,13,14,15,16,17,27,53,91,146,149','find_hand_item',1,'','',0,1,1),
(8,'1742194750225_G','find_hand_item','66,106,113,114,115','find_hand_item',1,'','',0,1,1),
(9,'1742195901294_G','find_hand_item','4,26,75,76,77,120,121,127,128,137,160,161,162,163,167,180','find_hand_item',1,'','',0,1,1),
(10,'1742198483809_G','pet_respect','','pet_respect',3,'','',0,1,1),
(11,'1742199338599_G','find_hand_item','2,24,29,40,42,50,64,101,107,157,158,159','find_hand_item',1,'','',0,1,1),
(12,'1742369952117_G','teleport','','teleport',3,'','',0,1,1),
(13,'1742372923261_G','change_motto','','change_motto',1,'','',0,1,1),
(14,'1742376992244_G','place_item','','place_item',5,'','',0,1,1),
(15,'1742377579479_G','move_item','','move_item',10,'','',0,1,1),
(16,'1742378786317_G','request_friend','','request_friend',3,'','',0,1,1),
(17,'1734011389107_G','enter_other_users_room','','enter_other_users_room',5,'','',0,1,1),
(18,'1742375669365_G','show_sign','10','show_sign',1,'','',1,1,1);

INSERT IGNORE INTO `daily_task_rewards` (`id`, `task_id`, `product_item_type_id`, `reward_type_id`, `extra_params`, `amount`, `sort_order`) VALUES
(1,1,8,'0','',25,0),
(2,2,8,'0','',25,0),
(3,3,8,'0','',25,0),
(4,4,8,'0','',25,0),
(5,5,8,'0','',25,0),
(6,6,8,'0','',25,0),
(7,7,8,'0','',25,0),
(8,8,8,'0','',25,0),
(9,9,8,'0','',25,0),
(10,10,8,'0','',25,0),
(11,11,8,'0','',25,0),
(12,12,8,'0','',25,0),
(13,13,8,'0','',25,0),
(14,14,8,'0','',25,0),
(15,15,8,'0','',25,0),
(16,16,8,'0','',25,0),
(17,17,8,'0','',25,0),
(18,18,8,'0','',100,0);
