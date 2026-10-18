-- Free products a user claims once by claim id, e.g. the cursed dragon adoption window (AIR WIN63-202610151745-318700621,
-- catalog/cursed_dragon_adoption/CursedDragonAdoptionController.as:126-169 sends ClaimProduct with the claim id
-- 'cursed_dragon_adoption' and the picked package). The primary key keeps one claim per user and claim id; choice is the
-- picked option (-1 when the client sent none).
-- The cursed dragon is pet type 37; it gets the same room chatter as the other dragons (types 12 and 30).
-- Idempotent.

CREATE TABLE IF NOT EXISTS `user_claimed_products` (
  `user_id` int(11) NOT NULL,
  `claim_id` varchar(64) NOT NULL,
  `choice` int(11) NOT NULL DEFAULT -1,
  `claimed_at` datetime(6) NOT NULL,
  PRIMARY KEY (`user_id`,`claim_id`),
  KEY `claim_id` (`claim_id`),
  CONSTRAINT `fk_user_claimed_products_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO `bots_pet_responses` (`pet_id`, `responses`) VALUES ('speech.pet37', 'Grrr;');
