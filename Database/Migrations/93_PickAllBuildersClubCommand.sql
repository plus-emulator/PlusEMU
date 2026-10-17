-- :pickallbc returns every Builders Club furni in an owned room to the warehouse; whoever may use :pickall may use it.
INSERT IGNORE INTO acl_permissions (`key`, category, description, is_orphan)
VALUES ('command.pickallbc', 'command', 'Pick up all Builders Club furniture in an owned room.', FALSE);

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT role_id, 'command.pickallbc' FROM role_permissions WHERE permission_key = 'command.pickall';
