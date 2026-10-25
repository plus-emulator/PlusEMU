INSERT IGNORE INTO acl_permissions (`key`, category, description, is_orphan)
VALUES ('command.takebadge', 'command', 'Remove a badge from another user.', FALSE);

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT role_id, 'command.takebadge' FROM role_permissions WHERE permission_key = 'command.givebadge';
