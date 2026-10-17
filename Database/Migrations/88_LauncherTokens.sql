-- Tokens the AIR launcher sends (Authorization: Bearer) to POST /api/client/air-ticket for a game ticket.
-- Only the SHA-256 hex digest of a token is stored: token_hash = SHA2(<token>, 256). A token is live while it is not
-- revoked, not expired, and users.credential_generation still equals the generation it was issued under.
-- Idempotent.

CREATE TABLE IF NOT EXISTS `user_launcher_tokens` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `user_id` int(11) NOT NULL,
  `token_hash` char(64) NOT NULL,
  `credential_generation` int(11) unsigned NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `expires_at` datetime(6) NOT NULL,
  `last_used_at` datetime(6) DEFAULT NULL,
  `revoked_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `token_hash` (`token_hash`),
  KEY `user_id` (`user_id`),
  KEY `expires_at` (`expires_at`),
  CONSTRAINT `fk_user_launcher_tokens_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
