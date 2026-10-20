-- What a Wired Share add-on published about a shared variable, kept for each reference variable that received it
-- (AIR WIN63-202610201032-107964540, VariableAddonShareAddon.as, VariableAddonImportAddon.as). The addons column holds the JSON of the
-- published text connector, level-up system and Variable FX. The Import add-on stacked with the reference picks what it shows.
CREATE TABLE IF NOT EXISTS `wired_shared_variable_addons` (
  `reference_item_id` int(10) unsigned NOT NULL,
  `addons` longtext NOT NULL,
  PRIMARY KEY (`reference_item_id`),
  CONSTRAINT `fk_wired_shared_variable_addons_item_id` FOREIGN KEY (`reference_item_id`) REFERENCES `items` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
