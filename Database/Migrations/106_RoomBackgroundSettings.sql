-- AIR lets a user with security level 4 or higher save the settings of a room background (InfoStandFurniView.as), so every role at that
-- level gets room.item_save_branding_items. room.background.allowed_hosts lists the websites a background image may come from (comma
-- separated, subdomains included); empty allows any website. Idempotent: reruns keep a list that was already set.

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT id, 'room.item_save_branding_items' FROM roles WHERE security_level >= 4;

INSERT IGNORE INTO server_settings (`key`, `value`) VALUES ('room.background.allowed_hosts', '');
