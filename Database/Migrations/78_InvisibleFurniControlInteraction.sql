UPDATE furniture SET interaction_type = 'invis_control'
WHERE item_name = 'conf_invis_control' AND type = 's'
  AND interaction_type IN ('default', 'default_floor', 'conf_invis_control');
