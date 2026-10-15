-- Make the Wired catalogue match official Habbo (hhus, client build WIN63-202610131318-860371703, NORMAL catalogue read r91
-- 2026-10-14, unchanged in r93 2026-10-15): page tree, page order, offers, offer order, prices, currencies and bulk flags.
-- Adds the ':chooser disabler' (conf_chooser_disable, furnidata 19391, revision 75953) with its own 'chooser_disable'
-- interaction and sells it on Wired > New Additions and Room Building > Configuration Items as hhus does.
--
-- Official tree under Furni > Wired: New Additions, Wired Triggers, Wired Effects, Wired Conditions, Wired Selectors,
-- Wired Add-Ons, Wired Variables {Variable Wired, Variables, Variable Add-Ons, Variable FX}, Wired Chests {Chests, Chest Wired,
-- Contracts}, Extras, Wired Archive. Leaderboards lives under Furni > Gaming, between Freeze! and Football.
-- Offers keep their ids: an official offer id is used when the offer already exists with that key, otherwise the offer already
-- sold on a Wired page with that key and product; only missing offers are created (with the official id when it is free).
-- Wired furni that hhus does not sell on these pages (unreleased October boxes, retired boxes, the starter chest) are not
-- dropped: they move to a last 'More Wired' page so nothing that was buyable disappears.
-- Idempotent: reruns end in the same state.

START TRANSACTION;

-- 1. The :chooser disabler furni (official furnidata: 1x1, height 2.0, not stackable, two states, configuration line).
INSERT INTO furniture (item_name, public_name, type, width, length, stack_height, can_stack, can_sit, is_walkable, sprite_id,
    allow_recycle, allow_trade, allow_marketplace_sell, allow_gift, allow_inventory_stack, interaction_type, interaction_modes_count,
    has_furnidata, revision, category, default_dir, xdim, ydim, name, description, custom_params, special_type,
    can_stand_on, can_sit_on, can_lay_on, can_put_stuff_on, height, furni_line, rare, tradeable, recyclable)
SELECT 'conf_chooser_disable', ':chooser disabler', 's', 1, 1, 2.0, 0, 0, 0, 19391,
    1, 1, 1, 1, 1, 'chooser_disable', 2,
    1, 75953, 'other', 0, 1, 1, ':chooser disabler', 'conf_chooser_disable desc', NULL, 1,
    0, 0, 0, 0, 2.0, 'configuration', 0, 1, 1
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM furniture f WHERE f.type = 's' AND f.furnidata_classname = 'conf_chooser_disable')
  AND NOT EXISTS (SELECT 1 FROM furniture f WHERE f.type = 's' AND f.furnidata_sprite_id = 19391);

UPDATE furniture SET interaction_type = 'chooser_disable', interaction_modes_count = 2
WHERE item_name = 'conf_chooser_disable' AND type = 's' AND interaction_type IN ('', 'default', 'default_floor', 'conf_chooser_disable');

-- 2. Pages. Wired Archive and the local More Wired page are created once; then every page gets its official parent and position.
INSERT INTO catalog_pages (parent_id, caption, icon, position, link, layout, visible, enabled, required_club_level)
SELECT w.id, 'Wired Archive', 80, 9, 'wired_archive', 'default_3x3', 1, 1, 0 FROM catalog_pages w
WHERE w.link = 'wired2' AND NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'wired_archive');
INSERT INTO catalog_pages (parent_id, caption, icon, position, link, layout, visible, enabled, required_club_level)
SELECT w.id, 'More Wired', 80, 10, 'plus_wired_more', 'default_3x3', 1, 1, 0 FROM catalog_pages w
WHERE w.link = 'wired2' AND NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'plus_wired_more');

INSERT IGNORE INTO catalog_page_images (page_id, slot, image)
SELECT c.id, s.slot, IF(s.slot = 0, 'catalog_wired_header1', '') FROM catalog_pages c
JOIN (SELECT 0 AS slot UNION ALL SELECT 1 UNION ALL SELECT 2) s
WHERE c.link IN ('wired_archive', 'plus_wired_more');
INSERT IGNORE INTO catalog_page_texts (page_id, slot, text)
SELECT c.id, s.slot, '' FROM catalog_pages c
JOIN (SELECT 0 AS slot UNION ALL SELECT 1 UNION ALL SELECT 2) s
WHERE c.link IN ('wired_archive', 'plus_wired_more');

CREATE TEMPORARY TABLE wired_page_order_82 (
    link VARCHAR(128) NOT NULL PRIMARY KEY,
    parent_link VARCHAR(128) NOT NULL,
    position INT NOT NULL,
    icon INT NOT NULL,
    caption VARCHAR(128) NOT NULL
) ENGINE=MEMORY;
INSERT INTO wired_page_order_82 VALUES
    ('wired_new', 'wired2', 0, 215, 'New Additions'),
    ('wired_triggers', 'wired2', 1, 81, 'Wired Triggers'),
    ('wired_effects', 'wired2', 2, 82, 'Wired Effects'),
    ('wired_conditions', 'wired2', 3, 83, 'Wired Conditions'),
    ('wired_selectors', 'wired2', 4, 317, 'Wired Selectors'),
    ('wired_addons', 'wired2', 5, 318, 'Wired Add-Ons'),
    ('wired_variables_parent', 'wired2', 6, 337, 'Wired Variables'),
    ('wired_chests_parent', 'wired2', 7, 360, 'Wired Chests'),
    ('wired_extras', 'wired2', 8, 324, 'Extras'),
    ('wired_archive', 'wired2', 9, 80, 'Wired Archive'),
    ('plus_wired_more', 'wired2', 10, 80, 'More Wired'),
    ('wired_variables_wired', 'wired_variables_parent', 0, 80, 'Variable Wired'),
    ('wired_variables', 'wired_variables_parent', 1, 337, 'Variables'),
    ('wired_variables_addons', 'wired_variables_parent', 2, 336, 'Variable Add-Ons'),
    ('sept26variable', 'wired_variables_parent', 3, 370, 'Variable FX'),
    ('wired_chests', 'wired_chests_parent', 0, 360, 'Chests'),
    ('wired_chests_wired', 'wired_chests_parent', 1, 80, 'Chest Wired'),
    ('wired_chests_contracts', 'wired_chests_parent', 2, 361, 'Contracts'),
    ('gaming_new', 'gaming', 0, 215, 'New Additions'),
    ('gaming_furni', 'gaming', 1, 85, 'Gaming Furni'),
    ('set_rollers', 'gaming', 2, 222, 'Rollers'),
    ('hhe24gates', 'gaming', 3, 339, 'One Way Gates'),
    ('gameset_battle_banzai', 'gaming', 4, 78, 'Battle Banzai'),
    ('gameset_freeze', 'gaming', 5, 87, 'Freeze!'),
    ('wired_leaderboard', 'gaming', 6, 256, 'Leaderboards'),
    ('gameset_football', 'gaming', 7, 56, 'Football'),
    ('gameset_snowboard', 'gaming', 8, 140, 'Snowboarding'),
    ('gameset_ice_hockey', 'gaming', 9, 88, 'Ice hockey'),
    ('gameset_ice_tagging', 'gaming', 10, 86, 'Ice tag'),
    ('gameset_roller_skates', 'gaming', 11, 94, 'Rollerskating'),
    ('gaming_projectiles', 'gaming', 12, 363, 'Projectiles'),
    ('sound_fx', 'gaming', 13, 4, 'Sound FX');

UPDATE catalog_pages c
JOIN wired_page_order_82 o ON o.link = c.link
JOIN catalog_pages parent ON parent.link = o.parent_link
SET c.parent_id = parent.id, c.position = o.position, c.icon = o.icon, c.caption = o.caption;

-- 3. Offers per official page, in official order: (page, position, official offer id, key, credits, activity points, point type,
-- bulk, furnidata sprite, amount, set price). The Configuration Items rows other than the :chooser disabler keep their prices.
CREATE TEMPORARY TABLE wired_offer_plan_82 (
    page_link VARCHAR(128) NOT NULL,
    position INT NOT NULL,
    official_offer_id INT NOT NULL,
    offer_key VARCHAR(100) NOT NULL,
    cost_credits INT UNSIGNED NOT NULL,
    cost_points INT UNSIGNED NOT NULL,
    points_type INT UNSIGNED NOT NULL,
    bulk_purchase TINYINT(1) NOT NULL,
    sprite_id INT NOT NULL,
    amount INT UNSIGNED NOT NULL,
    set_price TINYINT(1) NOT NULL,
    page_id INT NULL,
    furniture_id INT UNSIGNED NULL,
    offer_id INT NULL,
    PRIMARY KEY (page_link, position)
) ENGINE=MEMORY;
INSERT INTO wired_offer_plan_82 (page_link, position, official_offer_id, offer_key, cost_credits, cost_points, points_type,
    bulk_purchase, sprite_id, amount, set_price) VALUES
    ('wired_new', 0, 34965, 'wf_xtra_var_web_api', 150, 150, 5, 1, 19190, 1, 1),
    ('wired_new', 1, 35273, 'wf_xtra_var_web_api_deal1', 300, 300, 5, 0, 19190, 3, 1),
    ('wired_new', 2, 35246, 'conf_chooser_disable', 5, 0, 0, 1, 19391, 1, 1),
    ('wired_new', 3, 35263, 'wf_xtra_var_leaderboard', 15, 0, 0, 1, 19408, 1, 1),
    ('wired_new', 4, 35253, 'wf_label', 3, 0, 0, 1, 19398, 1, 1),
    ('wired_new', 5, 34996, 'wf_xtra_varfx_hp', 15, 0, 0, 1, 19196, 1, 1),
    ('wired_new', 6, 34999, 'wf_xtra_varfx_prog', 15, 0, 0, 1, 19199, 1, 1),
    ('wired_new', 7, 35000, 'wf_xtra_varfx_status', 15, 0, 0, 1, 19200, 1, 1),
    ('wired_new', 8, 34997, 'wf_xtra_varfx_levelling', 15, 0, 0, 1, 19197, 1, 1),
    ('wired_new', 9, 34998, 'wf_xtra_varfx_number', 15, 0, 0, 1, 19198, 1, 1),
    ('wired_new', 10, 34995, 'wf_xtra_varfx_boss', 20, 0, 0, 1, 19195, 1, 1),
    ('wired_triggers', 0, 9138, 'wf_trg_walks_on_furni', 4, 0, 0, 1, 3703, 1, 1),
    ('wired_triggers', 1, 9113, 'wf_trg_walks_off_furni', 4, 0, 0, 1, 3678, 1, 1),
    ('wired_triggers', 2, 9110, 'wf_trg_says_something', 4, 0, 0, 1, 3675, 1, 1),
    ('wired_triggers', 3, 9103, 'wf_trg_state_changed', 4, 0, 0, 1, 3668, 1, 1),
    ('wired_triggers', 4, 28109, 'wf_trg_stuff_state', 4, 0, 0, 1, 14091, 1, 1),
    ('wired_triggers', 5, 9118, 'wf_trg_enter_room', 5, 0, 0, 1, 3683, 1, 1),
    ('wired_triggers', 6, 29886, 'wf_trg_leave_room', 5, 0, 0, 1, 15343, 1, 1),
    ('wired_triggers', 7, 28107, 'wf_trg_click_furni', 7, 0, 0, 1, 14089, 1, 1),
    ('wired_triggers', 8, 31820, 'wf_trg_click_user', 5, 0, 0, 1, 16878, 1, 1),
    ('wired_triggers', 9, 28808, 'wf_trg_click_tile', 5, 0, 0, 1, 14554, 1, 1),
    ('wired_triggers', 10, 9106, 'wf_trg_periodically', 6, 0, 0, 1, 3671, 1, 1),
    ('wired_triggers', 11, 28195, 'wf_trg_period_short', 7, 0, 0, 1, 14128, 1, 1),
    ('wired_triggers', 12, 12506, 'wf_trg_period_long', 7, 0, 0, 1, 5042, 1, 1),
    ('wired_triggers', 13, 27634, 'wf_trg_user_performs_action', 7, 0, 0, 1, 13807, 1, 1),
    ('wired_triggers', 14, 12514, 'wf_trg_collision', 7, 0, 0, 1, 5050, 1, 1),
    ('wired_triggers', 15, 28108, 'wf_trg_recv_signal', 7, 0, 0, 1, 14090, 1, 1),
    ('wired_triggers', 16, 26581, 'wf_trg_clock_counter', 7, 0, 0, 1, 13385, 1, 1),
    ('wired_triggers', 17, 9115, 'wf_trg_game_ends', 5, 0, 0, 1, 3680, 1, 1),
    ('wired_triggers', 18, 9137, 'wf_trg_game_starts', 5, 0, 0, 1, 3702, 1, 1),
    ('wired_triggers', 19, 9108, 'wf_trg_score_achieved', 5, 0, 0, 1, 3673, 1, 1),
    ('wired_effects', 0, 9120, 'wf_act_toggle_state', 4, 0, 0, 1, 3685, 1, 1),
    ('wired_effects', 1, 9098, 'wf_act_move_rotate', 4, 0, 0, 1, 3663, 1, 1),
    ('wired_effects', 2, 29896, 'wf_act_move_rotate_user', 4, 0, 0, 1, 15353, 1, 1),
    ('wired_effects', 3, 9135, 'wf_act_match_to_sshot', 8, 0, 0, 1, 3700, 1, 1),
    ('wired_effects', 4, 9109, 'wf_act_teleport_to', 6, 0, 0, 1, 3674, 1, 1),
    ('wired_effects', 5, 9116, 'wf_act_show_message', 4, 0, 0, 1, 3681, 1, 1),
    ('wired_effects', 6, 13132, 'wf_act_toggle_to_rnd', 7, 0, 0, 1, 5451, 1, 1),
    ('wired_effects', 7, 11865, 'wf_act_call_stacks', 10, 0, 0, 1, 4834, 1, 1),
    ('wired_effects', 8, 26586, 'wf_act_control_clock', 7, 0, 0, 1, 13390, 1, 1),
    ('wired_effects', 9, 28809, 'wf_act_adjust_clock', 7, 0, 0, 1, 14555, 1, 1),
    ('wired_effects', 10, 28103, 'wf_act_send_signal', 7, 0, 0, 1, 14085, 1, 1),
    ('wired_effects', 11, 31666, 'wf_act_teleport_to_room', 8, 0, 0, 1, 16743, 1, 1),
    ('wired_effects', 12, 28110, 'wf_act_set_altitude', 7, 0, 0, 1, 14092, 1, 1),
    ('wired_effects', 13, 12512, 'wf_act_move_to_dir', 7, 0, 0, 1, 5048, 1, 1),
    ('wired_effects', 14, 28415, 'wf_act_furni_to_furni', 7, 0, 0, 1, 14328, 1, 1),
    ('wired_effects', 15, 28427, 'wf_act_furni_to_user', 7, 0, 0, 1, 14340, 1, 1),
    ('wired_effects', 16, 29898, 'wf_act_user_to_furni', 7, 0, 0, 1, 15355, 1, 1),
    ('wired_effects', 17, 28194, 'wf_act_rel_mov', 7, 0, 0, 1, 14127, 1, 1),
    ('wired_effects', 18, 34391, 'wf_act_place_furni', 8, 0, 0, 1, 18784, 1, 1),
    ('wired_effects', 19, 34392, 'wf_act_remove_furni', 8, 0, 0, 1, 18785, 1, 1),
    ('wired_effects', 20, 34390, 'wf_act_move_furni_as_group', 8, 0, 0, 1, 18783, 1, 1),
    ('wired_effects', 21, 12519, 'wf_act_chase', 7, 0, 0, 1, 5055, 1, 1),
    ('wired_effects', 22, 12526, 'wf_act_join_team', 7, 0, 0, 1, 5062, 1, 1),
    ('wired_effects', 23, 12513, 'wf_act_leave_team', 7, 0, 0, 1, 5049, 1, 1),
    ('wired_effects', 24, 9132, 'wf_act_give_score', 10, 0, 0, 1, 3697, 1, 1),
    ('wired_effects', 25, 12507, 'wf_act_give_score_tm', 10, 0, 0, 1, 5043, 1, 1),
    ('wired_effects', 26, 28193, 'wf_act_freeze', 7, 0, 0, 1, 14126, 1, 1),
    ('wired_effects', 27, 28197, 'wf_act_unfreeze', 7, 0, 0, 1, 14130, 1, 1),
    ('wired_effects', 28, 34242, 'wf_act_click_conf', 8, 0, 0, 1, 18673, 1, 1),
    ('wired_effects', 29, 12303, 'wf_act_kick_user', 10, 0, 0, 1, 4947, 1, 1),
    ('wired_effects', 30, 16068, 'wf_act_bot_give_handitem', 10, 0, 0, 1, 7852, 1, 1),
    ('wired_effects', 31, 16069, 'wf_act_bot_move', 10, 0, 0, 1, 7853, 1, 1),
    ('wired_effects', 32, 16073, 'wf_act_bot_talk', 10, 0, 0, 1, 7857, 1, 1),
    ('wired_effects', 33, 16071, 'wf_act_bot_talk_to_avatar', 10, 0, 0, 1, 7855, 1, 1),
    ('wired_effects', 34, 16064, 'wf_act_bot_clothes', 10, 0, 0, 1, 7848, 1, 1),
    ('wired_effects', 35, 32843, 'wf_act_log', 4, 0, 0, 1, 17599, 1, 1),
    ('wired_effects', 36, 28811, 'wf_act_neg_call_stacks', 7, 0, 0, 1, 14557, 1, 1),
    ('wired_effects', 37, 28810, 'wf_act_neg_send_signal', 7, 0, 0, 1, 14556, 1, 1),
    ('wired_effects', 38, 32842, 'wf_act_neg_log', 4, 0, 0, 1, 17598, 1, 1),
    ('wired_conditions', 0, 9130, 'wf_cnd_match_snapshot', 5, 0, 0, 1, 3695, 1, 1),
    ('wired_conditions', 1, 9129, 'wf_cnd_trggrer_on_frn', 5, 0, 0, 1, 3694, 1, 1),
    ('wired_conditions', 2, 9127, 'wf_cnd_furnis_hv_avtrs', 5, 0, 0, 1, 3692, 1, 1),
    ('wired_conditions', 3, 9558, 'wf_cnd_has_furni_on', 5, 0, 0, 1, 3857, 1, 1),
    ('wired_conditions', 4, 27632, 'wf_cnd_triggerer_match', 5, 0, 0, 1, 13805, 1, 1),
    ('wired_conditions', 5, 28196, 'wf_cnd_actor_dir', 5, 0, 0, 1, 14129, 1, 1),
    ('wired_conditions', 6, 27636, 'wf_cnd_user_performs_action', 5, 0, 0, 1, 13809, 1, 1),
    ('wired_conditions', 7, 10597, 'wf_cnd_actor_in_group', 5, 0, 0, 1, 4281, 1, 1),
    ('wired_conditions', 8, 16070, 'wf_cnd_has_handitem', 5, 0, 0, 1, 7854, 1, 1),
    ('wired_conditions', 9, 27637, 'wf_cnd_counter_time_matches', 5, 0, 0, 1, 13810, 1, 1),
    ('wired_conditions', 10, 28111, 'wf_cnd_has_altitude', 5, 0, 0, 1, 14093, 1, 1),
    ('wired_conditions', 11, 13128, 'wf_cnd_stuff_is', 5, 0, 0, 1, 5447, 1, 1),
    ('wired_conditions', 12, 28422, 'wf_cnd_slc_quantity', 5, 0, 0, 1, 14335, 1, 1),
    ('wired_conditions', 13, 28807, 'wf_cnd_valid_moves', 5, 0, 0, 1, 14553, 1, 1),
    ('wired_conditions', 14, 12520, 'wf_cnd_actor_in_team', 5, 0, 0, 1, 5056, 1, 1),
    ('wired_conditions', 15, 27639, 'wf_cnd_team_has_score', 5, 0, 0, 1, 13812, 1, 1),
    ('wired_conditions', 16, 27638, 'wf_cnd_team_has_rank', 5, 0, 0, 1, 13811, 1, 1),
    ('wired_conditions', 17, 27631, 'wf_cnd_match_time', 5, 0, 0, 1, 13804, 1, 1),
    ('wired_conditions', 18, 27630, 'wf_cnd_match_date', 5, 0, 0, 1, 13803, 1, 1),
    ('wired_conditions', 19, 13133, 'wf_cnd_not_match_snap', 5, 0, 0, 1, 5452, 1, 1),
    ('wired_conditions', 20, 13119, 'wf_cnd_not_trggrer_on', 5, 0, 0, 1, 5438, 1, 1),
    ('wired_conditions', 21, 13122, 'wf_cnd_not_hv_avtrs', 5, 0, 0, 1, 5441, 1, 1),
    ('wired_conditions', 22, 13121, 'wf_cnd_not_furni_on', 5, 0, 0, 1, 5440, 1, 1),
    ('wired_conditions', 23, 27635, 'wf_cnd_not_triggerer_match', 5, 0, 0, 1, 13808, 1, 1),
    ('wired_conditions', 24, 27629, 'wf_cnd_not_user_performs_action', 5, 0, 0, 1, 13802, 1, 1),
    ('wired_conditions', 25, 13129, 'wf_cnd_not_in_group', 5, 0, 0, 1, 5448, 1, 1),
    ('wired_conditions', 26, 27633, 'wf_cnd_not_has_handitem', 5, 0, 0, 1, 13806, 1, 1),
    ('wired_conditions', 27, 13130, 'wf_cnd_not_stuff_is', 5, 0, 0, 1, 5449, 1, 1),
    ('wired_conditions', 28, 13120, 'wf_cnd_not_in_team', 5, 0, 0, 1, 5439, 1, 1),
    ('wired_selectors', 0, 28420, 'wf_slc_furni_area', 5, 0, 0, 1, 14333, 1, 1),
    ('wired_selectors', 1, 28429, 'wf_slc_furni_neighborhood', 5, 0, 0, 1, 14342, 1, 1),
    ('wired_selectors', 2, 28421, 'wf_slc_furni_bytype', 5, 0, 0, 1, 14334, 1, 1),
    ('wired_selectors', 3, 28417, 'wf_slc_furni_altitude', 5, 0, 0, 1, 14330, 1, 1),
    ('wired_selectors', 4, 28416, 'wf_slc_furni_onfurni', 5, 0, 0, 1, 14329, 1, 1),
    ('wired_selectors', 5, 28424, 'wf_slc_furni_picks', 5, 0, 0, 1, 14337, 1, 1),
    ('wired_selectors', 6, 28413, 'wf_slc_furni_signal', 5, 0, 0, 1, 14326, 1, 1),
    ('wired_selectors', 7, 28412, 'wf_slc_users_area', 5, 0, 0, 1, 14325, 1, 1),
    ('wired_selectors', 8, 28433, 'wf_slc_users_neighborhood', 5, 0, 0, 1, 14346, 1, 1),
    ('wired_selectors', 9, 28430, 'wf_slc_users_bytype', 5, 0, 0, 1, 14343, 1, 1),
    ('wired_selectors', 10, 28425, 'wf_slc_users_signal', 5, 0, 0, 1, 14338, 1, 1),
    ('wired_selectors', 11, 28432, 'wf_slc_users_team', 5, 0, 0, 1, 14345, 1, 1),
    ('wired_selectors', 12, 28426, 'wf_slc_users_byaction', 5, 0, 0, 1, 14339, 1, 1),
    ('wired_selectors', 13, 28414, 'wf_slc_users_byname', 5, 0, 0, 1, 14327, 1, 1),
    ('wired_selectors', 14, 28428, 'wf_slc_users_onfurni', 5, 0, 0, 1, 14341, 1, 1),
    ('wired_selectors', 15, 28423, 'wf_slc_users_handitem', 5, 0, 0, 1, 14336, 1, 1),
    ('wired_selectors', 16, 28418, 'wf_slc_users_group', 5, 0, 0, 1, 14331, 1, 1),
    ('wired_selectors', 17, 28419, 'wf_xtra_filter_users', 5, 0, 0, 1, 14332, 1, 1),
    ('wired_selectors', 18, 28431, 'wf_xtra_filter_furni', 5, 0, 0, 1, 14344, 1, 1),
    ('wired_selectors', 19, 29900, 'wf_slc_remote', 5, 0, 0, 1, 15357, 1, 1),
    ('wired_addons', 0, 28116, 'wf_xtra_execution_limit', 5, 0, 0, 1, 14098, 1, 1),
    ('wired_addons', 1, 28114, 'wf_xtra_mov_carry_users', 5, 0, 0, 1, 14096, 1, 1),
    ('wired_addons', 2, 28104, 'wf_xtra_mov_physics', 5, 0, 0, 1, 14086, 1, 1),
    ('wired_addons', 3, 28106, 'wf_xtra_mov_no_animation', 5, 0, 0, 1, 14088, 1, 1),
    ('wired_addons', 4, 28198, 'wf_xtra_anim_time', 5, 0, 0, 1, 14131, 1, 1),
    ('wired_addons', 5, 29906, 'wf_xtra_exec_in_order', 5, 0, 0, 1, 15363, 1, 1),
    ('wired_addons', 6, 34243, 'wf_xtra_mov_curve', 5, 0, 0, 1, 18674, 1, 1),
    ('wired_addons', 7, 34244, 'wf_xtra_rotate_to_dir', 20, 0, 0, 1, 18675, 1, 1),
    ('wired_addons', 8, 9104, 'wf_xtra_random', 5, 0, 0, 1, 3669, 1, 1),
    ('wired_addons', 9, 9105, 'wf_xtra_unseen', 5, 0, 0, 1, 3670, 1, 1),
    ('wired_addons', 10, 26129, 'wf_xtra_or_eval', 5, 0, 0, 1, 13272, 1, 1),
    ('wired_addons', 11, 29899, 'wf_xtra_text_output_username', 5, 0, 0, 1, 15356, 1, 1),
    ('wired_addons', 12, 33989, 'wf_xtra_text_output_furni_name', 5, 0, 0, 1, 18495, 1, 1),
    ('wired_variables_wired', 0, 29895, 'wf_act_give_var', 7, 0, 0, 1, 15352, 1, 1),
    ('wired_variables_wired', 1, 29887, 'wf_act_change_var_val', 7, 0, 0, 1, 15344, 1, 1),
    ('wired_variables_wired', 2, 29897, 'wf_act_remove_var', 7, 0, 0, 1, 15354, 1, 1),
    ('wired_variables_wired', 3, 29904, 'wf_cnd_has_var', 5, 0, 0, 1, 15361, 1, 1),
    ('wired_variables_wired', 4, 29901, 'wf_cnd_neg_has_var', 5, 0, 0, 1, 15358, 1, 1),
    ('wired_variables_wired', 5, 29885, 'wf_cnd_var_val_match', 5, 0, 0, 1, 15342, 1, 1),
    ('wired_variables_wired', 6, 29892, 'wf_cnd_var_age_match', 5, 0, 0, 1, 15349, 1, 1),
    ('wired_variables_wired', 7, 29903, 'wf_slc_users_with_var', 5, 0, 0, 1, 15360, 1, 1),
    ('wired_variables_wired', 8, 29882, 'wf_slc_furni_with_var', 5, 0, 0, 1, 15339, 1, 1),
    ('wired_variables_wired', 9, 29888, 'wf_xtra_filter_users_by_var', 5, 0, 0, 1, 15345, 1, 1),
    ('wired_variables_wired', 10, 29884, 'wf_xtra_filter_furni_by_var', 5, 0, 0, 1, 15341, 1, 1),
    ('wired_variables_wired', 11, 29883, 'wf_trg_var_changed', 5, 0, 0, 1, 15340, 1, 1),
    ('wired_variables_wired', 12, 29893, 'wf_xtra_text_output_variable', 5, 0, 0, 1, 15350, 1, 1),
    ('wired_variables_wired', 13, 29902, 'wf_xtra_text_input_variable', 5, 0, 0, 1, 15359, 1, 1),
    ('wired_variables', 0, 29890, 'wf_var_user', 7, 0, 0, 1, 15347, 1, 1),
    ('wired_variables', 1, 29894, 'wf_var_furni', 7, 0, 0, 1, 15351, 1, 1),
    ('wired_variables', 2, 29907, 'wf_var_room', 7, 0, 0, 1, 15364, 1, 1),
    ('wired_variables', 3, 29889, 'wf_var_context', 7, 0, 0, 1, 15346, 1, 1),
    ('wired_variables', 4, 29905, 'wf_var_reference', 10, 0, 0, 1, 15362, 1, 1),
    ('wired_variables', 5, 31921, 'wf_var_echo', 6, 0, 0, 1, 16910, 1, 1),
    ('wired_variables_addons', 0, 29891, 'wf_xtra_var_text_connector', 6, 0, 0, 1, 15348, 1, 1),
    ('wired_variables_addons', 1, 31920, 'wf_xtra_var_lvlup_system', 8, 0, 0, 1, 16909, 1, 1),
    ('wired_variables_addons', 2, 31919, 'wf_xtra_var_time_util', 8, 0, 0, 1, 16908, 1, 1),
    ('wired_variables_addons', 3, 35263, 'wf_xtra_var_leaderboard', 15, 0, 0, 1, 19408, 1, 1),
    ('wired_variables_addons', 4, 34965, 'wf_xtra_var_web_api', 150, 150, 5, 1, 19190, 1, 1),
    ('wired_variables_addons', 5, 35273, 'wf_xtra_var_web_api_deal1', 300, 300, 5, 0, 19190, 3, 1),
    ('sept26variable', 0, 34996, 'wf_xtra_varfx_hp', 15, 0, 0, 1, 19196, 1, 1),
    ('sept26variable', 1, 34999, 'wf_xtra_varfx_prog', 15, 0, 0, 1, 19199, 1, 1),
    ('sept26variable', 2, 35000, 'wf_xtra_varfx_status', 15, 0, 0, 1, 19200, 1, 1),
    ('sept26variable', 3, 34997, 'wf_xtra_varfx_levelling', 15, 0, 0, 1, 19197, 1, 1),
    ('sept26variable', 4, 34998, 'wf_xtra_varfx_number', 15, 0, 0, 1, 19198, 1, 1),
    ('sept26variable', 5, 34995, 'wf_xtra_varfx_boss', 20, 0, 0, 1, 19195, 1, 1),
    ('wired_chests', 0, 33982, 'wf_storage_furni1', 50, 0, 0, 0, 18488, 1, 1),
    ('wired_chests', 1, 33981, 'wf_storage_coins2', 50, 0, 0, 0, 18487, 1, 1),
    ('wired_chests', 2, 34015, 'wf_storage_furni_bd', 250, 0, 0, 0, 18488, 1, 1),
    ('wired_chests', 3, 34014, 'wf_storage_coins_bd', 250, 0, 0, 0, 18487, 1, 1),
    ('wired_chests_wired', 0, 33972, 'wf_act_give_currency', 5, 0, 0, 1, 18478, 1, 1),
    ('wired_chests_wired', 1, 33973, 'wf_act_give_furni', 5, 0, 0, 1, 18479, 1, 1),
    ('wired_chests_wired', 2, 33976, 'wf_cnd_chest_has_items', 5, 0, 0, 1, 18482, 1, 1),
    ('wired_chests_wired', 3, 33975, 'wf_cnd_chest_has_item_type', 5, 0, 0, 1, 18481, 1, 1),
    ('wired_chests_wired', 4, 33988, 'wf_xtra_scan_chest_furni_by_type', 5, 0, 0, 1, 18494, 1, 1),
    ('wired_chests_wired', 5, 33974, 'wf_act_init_transaction', 7, 0, 0, 1, 18480, 1, 1),
    ('wired_chests_wired', 6, 33971, 'wf_act_cancel_transaction', 7, 0, 0, 1, 18477, 1, 1),
    ('wired_chests_wired', 7, 33985, 'wf_trg_transaction_complete', 7, 0, 0, 1, 18491, 1, 1),
    ('wired_chests_wired', 8, 33986, 'wf_trg_transaction_fail', 7, 0, 0, 1, 18492, 1, 1),
    ('wired_chests_wired', 9, 33987, 'wf_xtra_custom_contract', 10, 0, 0, 1, 18493, 1, 1),
    ('wired_chests_contracts', 0, 33977, 'wf_contract_payment', 10, 0, 0, 1, 18483, 1, 1),
    ('wired_chests_contracts', 1, 33979, 'wf_contract_trade', 10, 0, 0, 1, 18485, 1, 1),
    ('wired_chests_contracts', 2, 33978, 'wf_contract_reward', 10, 0, 0, 1, 18484, 1, 1),
    ('wired_chests_contracts', 3, 33987, 'wf_xtra_custom_contract', 10, 0, 0, 1, 18493, 1, 1),
    ('wired_extras', 0, 26591, 'wf_game_upcounter1', 7, 0, 0, 1, 13395, 1, 1),
    ('wired_extras', 1, 26580, 'wf_game_upcounter2', 7, 0, 0, 1, 13384, 1, 1),
    ('wired_extras', 2, 26579, 'wf_upcounter1', 5, 0, 0, 1, 13383, 1, 1),
    ('wired_extras', 3, 26592, 'wf_upcounter2', 5, 0, 0, 1, 13396, 1, 1),
    ('wired_extras', 4, 28115, 'wf_antenna1', 5, 0, 0, 1, 14097, 1, 1),
    ('wired_extras', 5, 28113, 'wf_antenna2', 3, 0, 0, 1, 14095, 1, 1),
    ('wired_extras', 6, 31667, 'wf_room_linker', 2, 0, 0, 0, 16744, 1, 1),
    ('wired_extras', 7, 35253, 'wf_label', 3, 0, 0, 1, 19398, 1, 1),
    ('wired_archive', 0, 9114, 'wf_trg_at_given_time', 6, 0, 0, 1, 3679, 1, 1),
    ('wired_archive', 1, 16072, 'wf_trg_bot_reached_avtr', 7, 0, 0, 1, 7856, 1, 1),
    ('wired_archive', 2, 16066, 'wf_trg_bot_reached_stf', 7, 0, 0, 1, 7850, 1, 1),
    ('wired_archive', 3, 9126, 'wf_act_reset_timers', 6, 0, 0, 1, 3691, 1, 1),
    ('wired_archive', 4, 12525, 'wf_act_flee', 7, 0, 0, 1, 5061, 1, 1),
    ('wired_archive', 5, 16065, 'wf_act_bot_teleport', 10, 0, 0, 1, 7849, 1, 1),
    ('wired_archive', 6, 16067, 'wf_act_bot_follow_avatar', 10, 0, 0, 1, 7851, 1, 1),
    ('wired_archive', 7, 9117, 'wf_cnd_time_less_than', 5, 0, 0, 1, 3682, 1, 1),
    ('wired_archive', 8, 9100, 'wf_cnd_time_more_than', 5, 0, 0, 1, 3665, 1, 1),
    ('wired_archive', 9, 13126, 'wf_cnd_user_count_in', 5, 0, 0, 1, 5445, 1, 1),
    ('wired_archive', 10, 13124, 'wf_cnd_not_user_count', 5, 0, 0, 1, 5443, 1, 1),
    ('wired_archive', 11, 13753, 'wf_cnd_date_rng_active', 5, 0, 0, 1, 5861, 1, 1),
    ('wired_leaderboard', 0, 26582, 'highscore_fastesttime_alltime', 7, 0, 0, 0, 13386, 1, 1),
    ('wired_leaderboard', 1, 26583, 'highscore_fastesttime_daily', 7, 0, 0, 0, 13387, 1, 1),
    ('wired_leaderboard', 2, 26584, 'highscore_fastesttime_weekly', 7, 0, 0, 0, 13388, 1, 1),
    ('wired_leaderboard', 3, 26585, 'highscore_fastesttime_monthly', 7, 0, 0, 0, 13389, 1, 1),
    ('wired_leaderboard', 4, 26587, 'highscore_longesttime_alltime', 7, 0, 0, 0, 13391, 1, 1),
    ('wired_leaderboard', 5, 26588, 'highscore_longesttime_daily', 7, 0, 0, 0, 13392, 1, 1),
    ('wired_leaderboard', 6, 26589, 'highscore_longesttime_weekly', 7, 0, 0, 0, 13393, 1, 1),
    ('wired_leaderboard', 7, 26590, 'highscore_longesttime_monthly', 7, 0, 0, 0, 13394, 1, 1),
    ('wired_leaderboard', 8, 12508, 'highscore_classic_alltime', 7, 0, 0, 0, 5044, 1, 1),
    ('wired_leaderboard', 9, 12509, 'highscore_classic_daily', 7, 0, 0, 0, 5045, 1, 1),
    ('wired_leaderboard', 10, 12510, 'highscore_classic_weekly', 7, 0, 0, 0, 5046, 1, 1),
    ('wired_leaderboard', 11, 12511, 'highscore_classic_monthly', 7, 0, 0, 0, 5047, 1, 1),
    ('wired_leaderboard', 12, 12515, 'highscore_perteam_alltime', 7, 0, 0, 0, 5051, 1, 1),
    ('wired_leaderboard', 13, 12516, 'highscore_perteam_daily', 7, 0, 0, 0, 5052, 1, 1),
    ('wired_leaderboard', 14, 12517, 'highscore_perteam_weekly', 7, 0, 0, 0, 5053, 1, 1),
    ('wired_leaderboard', 15, 12518, 'highscore_perteam_monthly', 7, 0, 0, 0, 5054, 1, 1),
    ('wired_leaderboard', 16, 12521, 'highscore_mostwin_alltime', 7, 0, 0, 0, 5057, 1, 1),
    ('wired_leaderboard', 17, 12522, 'highscore_mostwin_daily', 7, 0, 0, 0, 5058, 1, 1),
    ('wired_leaderboard', 18, 12523, 'highscore_mostwin_weekly', 7, 0, 0, 0, 5059, 1, 1),
    ('wired_leaderboard', 19, 12524, 'highscore_mostwin_monthly', 7, 0, 0, 0, 5060, 1, 1),
    ('wired_leaderboard', 20, 35263, 'wf_xtra_var_leaderboard', 15, 0, 0, 1, 19408, 1, 1),
    ('configitems', 0, 28805, 'conf_queue_speed', 0, 0, 0, 1, 14551, 1, 0),
    ('configitems', 1, 28803, 'conf_invis_control', 0, 0, 0, 1, 14549, 1, 0),
    ('configitems', 2, 28806, 'conf_handitem_block', 0, 0, 0, 1, 14552, 1, 0),
    ('configitems', 3, 28804, 'conf_wired_disable', 0, 0, 0, 1, 14550, 1, 0),
    ('configitems', 4, 29681, 'conf_area_hide', 0, 0, 0, 1, 15215, 1, 0),
    ('configitems', 5, 35246, 'conf_chooser_disable', 5, 0, 0, 1, 19391, 1, 1);

UPDATE wired_offer_plan_82 p JOIN catalog_pages c ON c.link = p.page_link SET p.page_id = c.id;
UPDATE wired_offer_plan_82 p JOIN furniture f ON f.type = 's' AND f.furnidata_sprite_id = p.sprite_id SET p.furniture_id = f.id;

-- The pages whose offers this migration owns (Configuration Items and Leaderboards only gain or reorder offers).
CREATE TEMPORARY TABLE wired_managed_pages_82 (
    page_id INT NOT NULL PRIMARY KEY,
    page_rank INT NOT NULL,
    wired TINYINT(1) NOT NULL
) ENGINE=MEMORY;
INSERT INTO wired_managed_pages_82 (page_id, page_rank, wired)
SELECT c.id, o.page_rank, o.wired FROM catalog_pages c JOIN (
    SELECT 'wired_new' AS link, 0 AS page_rank, 1 AS wired UNION ALL SELECT 'wired_triggers', 1, 1 UNION ALL SELECT 'wired_effects', 2, 1
    UNION ALL SELECT 'wired_conditions', 3, 1 UNION ALL SELECT 'wired_selectors', 4, 1 UNION ALL SELECT 'wired_addons', 5, 1
    UNION ALL SELECT 'wired_variables_wired', 6, 1 UNION ALL SELECT 'wired_variables', 7, 1 UNION ALL SELECT 'wired_variables_addons', 8, 1
    UNION ALL SELECT 'sept26variable', 9, 1 UNION ALL SELECT 'wired_chests', 10, 1 UNION ALL SELECT 'wired_chests_wired', 11, 1
    UNION ALL SELECT 'wired_chests_contracts', 12, 1 UNION ALL SELECT 'wired_extras', 13, 1 UNION ALL SELECT 'wired_archive', 14, 1
    UNION ALL SELECT 'plus_wired_more', 15, 1 UNION ALL SELECT 'wired_leaderboard', 16, 0 UNION ALL SELECT 'configitems', 17, 0
) o ON o.link = c.link;

-- Resolve each planned offer: the official id with the same key, else the lowest offer with that key and product that is
-- already on one of these pages (never e.g. a free staff-page copy).
UPDATE wired_offer_plan_82 p JOIN catalog_offers o ON o.id = p.official_offer_id AND o.localization_key = p.offer_key
SET p.offer_id = o.id;
UPDATE wired_offer_plan_82 p SET p.offer_id = (
    SELECT MIN(o.id) FROM catalog_offers o
    JOIN catalog_offer_products op ON op.offer_id = o.id AND op.product_type = 'furni' AND op.furniture_id = p.furniture_id AND op.amount = p.amount
    JOIN catalog_page_offers po ON po.offer_id = o.id
    JOIN wired_managed_pages_82 m ON m.page_id = po.page_id
    WHERE o.localization_key = p.offer_key)
WHERE p.offer_id IS NULL AND p.furniture_id IS NOT NULL;

-- Create what is still missing under its official id (only if that id is free).
INSERT INTO catalog_offers (id, localization_key, cost_credits, cost_points, points_type, club_level, bulk_purchase, enabled, preview_image)
SELECT DISTINCT p.official_offer_id, p.offer_key, p.cost_credits, p.cost_points, p.points_type, 0, p.bulk_purchase, 1, ''
FROM wired_offer_plan_82 p
WHERE p.offer_id IS NULL AND p.furniture_id IS NOT NULL AND p.set_price = 1
  AND NOT EXISTS (SELECT 1 FROM catalog_offers o WHERE o.id = p.official_offer_id);
UPDATE wired_offer_plan_82 p JOIN catalog_offers o ON o.id = p.official_offer_id AND o.localization_key = p.offer_key
SET p.offer_id = o.id WHERE p.offer_id IS NULL;
INSERT INTO catalog_offer_products (offer_id, position, product_type, furniture_id, amount, extra_param)
SELECT DISTINCT p.offer_id, 0, 'furni', p.furniture_id, p.amount, ''
FROM wired_offer_plan_82 p
WHERE p.offer_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM catalog_offer_products op WHERE op.offer_id = p.offer_id);

-- Official prices, currencies and bulk flags.
UPDATE catalog_offers o JOIN wired_offer_plan_82 p ON p.offer_id = o.id AND p.set_price = 1
SET o.cost_credits = p.cost_credits, o.cost_points = p.cost_points, o.points_type = p.points_type,
    o.bulk_purchase = p.bulk_purchase, o.club_level = 0, o.enabled = 1;

-- 4. Page contents. Offers on the Wired pages that hhus does not sell there keep being sold on More Wired, in their old order.
CREATE TEMPORARY TABLE wired_more_82 (
    offer_id INT NOT NULL PRIMARY KEY,
    sort_key BIGINT NOT NULL
) ENGINE=MEMORY;
INSERT INTO wired_more_82 (offer_id, sort_key)
SELECT po.offer_id, MIN(m.page_rank * 100000 + po.position)
FROM catalog_page_offers po JOIN wired_managed_pages_82 m ON m.page_id = po.page_id AND m.wired = 1
WHERE NOT EXISTS (SELECT 1 FROM wired_offer_plan_82 p WHERE p.offer_id = po.offer_id)
GROUP BY po.offer_id;

DELETE po FROM catalog_page_offers po JOIN wired_managed_pages_82 m ON m.page_id = po.page_id;

INSERT INTO catalog_page_offers (page_id, offer_id, position)
SELECT p.page_id, p.offer_id, MIN(p.position) FROM wired_offer_plan_82 p
WHERE p.page_id IS NOT NULL AND p.offer_id IS NOT NULL
GROUP BY p.page_id, p.offer_id;

INSERT INTO catalog_page_offers (page_id, offer_id, position)
SELECT c.id, w.offer_id, ROW_NUMBER() OVER (ORDER BY w.sort_key, w.offer_id) - 1
FROM wired_more_82 w JOIN catalog_pages c ON c.link = 'plus_wired_more';

DROP TEMPORARY TABLE wired_more_82;
DROP TEMPORARY TABLE wired_managed_pages_82;
DROP TEMPORARY TABLE wired_offer_plan_82;
DROP TEMPORARY TABLE wired_page_order_82;

COMMIT;
