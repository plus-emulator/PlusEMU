-- A WIRED Link furni keeps its Self-receiving setting when it has no network or its network is deleted, as in Habbo.
-- Idempotent: reruns end in the same state. Two statements, because MariaDB refuses to drop and add a key of the same name in one ALTER.

ALTER TABLE `wired_link_endpoints` DROP FOREIGN KEY IF EXISTS `fk_wired_link_endpoints_network_id`;

ALTER TABLE `wired_link_endpoints`
    MODIFY `network_id` INT(10) UNSIGNED NULL DEFAULT NULL,
    ADD CONSTRAINT `fk_wired_link_endpoints_network_id` FOREIGN KEY (`network_id`) REFERENCES `wired_link_networks` (`id`) ON DELETE SET NULL;
