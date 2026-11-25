-- The moderation tool and :userinfo show the email address of a user only to staff with moderation.user_email.
-- Roles at the top security level get it. Idempotent.

INSERT IGNORE INTO acl_permissions (`key`, category, description, is_orphan)
VALUES ('moderation.user_email', 'moderation', 'See the email address of a user in the moderation tool and :userinfo.', FALSE);

INSERT IGNORE INTO role_permissions (role_id, permission_key)
SELECT id, 'moderation.user_email' FROM roles WHERE security_level = 7;
