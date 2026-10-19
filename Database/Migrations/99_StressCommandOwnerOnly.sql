-- :stress spawns up to 2000 bots, so only hotel owners may run it: the role with slug 'owner' keeps command.stress,
-- every other role loses it, and a per-user grant stays only for users who hold the owner role. Idempotent.

DELETE permission FROM role_permissions AS permission
JOIN roles AS role ON role.id = permission.role_id
WHERE permission.permission_key = 'command.stress' AND role.slug <> 'owner';

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT id, 'command.stress' FROM roles WHERE slug = 'owner';

DELETE FROM user_permissions
WHERE permission_key = 'command.stress' AND effect = 'grant'
  AND user_id NOT IN (SELECT user_role.user_id FROM user_roles AS user_role JOIN roles AS role ON role.id = user_role.role_id WHERE role.slug = 'owner');
