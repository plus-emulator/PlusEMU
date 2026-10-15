-- Wired furniture rows resolve to the box PlusEMU implements, and every implemented box a player can configure is sold.
-- Interaction rules (ItemDataManager.ReadInteractionType, ItemDefinition.WiredDescriptor, WiredComponent.DescriptorOf):
-- a row whose item_name is a WiredBoxRegistry canonical name keeps its generic category interaction (wired_effect,
-- wired_trigger, wired_condition, wired_selector, wired_addon, wired_variable) or the canonical name itself. Rows
-- named after a different client asset store the canonical name. Chests and contracts use their own interactions,
-- counters their own item name, antennas 'antenna', and the click tile 'room_invisible_click_tile'.
-- Sprite ids are checked against official furnidata (WIN63-202610131318). Idempotent: reruns change nothing.
START TRANSACTION;

-- 1. The 182 canonical boxes: a missing or wrong category becomes the box's generic category interaction.
CREATE TEMPORARY TABLE wired_canonical_79 (
    item_name VARCHAR(70) NOT NULL PRIMARY KEY,
    interaction VARCHAR(25) NOT NULL
) ENGINE=MEMORY;
INSERT INTO wired_canonical_79 (item_name, interaction) VALUES
    ('wf_act_adjust_clock','wired_effect'), ('wf_act_bot_clothes','wired_effect'),
    ('wf_act_bot_follow_avatar','wired_effect'), ('wf_act_bot_give_handitem','wired_effect'),
    ('wf_act_bot_move','wired_effect'), ('wf_act_bot_talk','wired_effect'),
    ('wf_act_bot_talk_to_avatar','wired_effect'), ('wf_act_bot_teleport','wired_effect'),
    ('wf_act_call_stacks','wired_effect'), ('wf_act_cancel_transaction','wired_effect'),
    ('wf_act_change_var_val','wired_effect'), ('wf_act_chase','wired_effect'), ('wf_act_click_conf','wired_effect'),
    ('wf_act_control_clock','wired_effect'), ('wf_act_flee','wired_effect'), ('wf_act_freeze','wired_effect'),
    ('wf_act_furni_to_furni','wired_effect'), ('wf_act_furni_to_user','wired_effect'),
    ('wf_act_give_currency','wired_effect'), ('wf_act_give_furni','wired_effect'),
    ('wf_act_give_reward','wired_effect'), ('wf_act_give_score','wired_effect'),
    ('wf_act_give_score_tm','wired_effect'), ('wf_act_give_var','wired_effect'),
    ('wf_act_init_transaction','wired_effect'), ('wf_act_join_team','wired_effect'),
    ('wf_act_kick_user','wired_effect'), ('wf_act_leave_team','wired_effect'), ('wf_act_log','wired_effect'),
    ('wf_act_match_to_sshot','wired_effect'), ('wf_act_move_furni_as_group','wired_effect'),
    ('wf_act_move_furni_to','wired_effect'), ('wf_act_move_rotate','wired_effect'),
    ('wf_act_move_rotate_user','wired_effect'), ('wf_act_move_to_dir','wired_effect'),
    ('wf_act_mute_triggerer','wired_effect'), ('wf_act_neg_call_stacks','wired_effect'),
    ('wf_act_neg_log','wired_effect'), ('wf_act_neg_send_signal','wired_effect'),
    ('wf_act_place_furni','wired_effect'), ('wf_act_rel_mov','wired_effect'), ('wf_act_remove_furni','wired_effect'),
    ('wf_act_remove_var','wired_effect'), ('wf_act_reset_timers','wired_effect'),
    ('wf_act_send_signal','wired_effect'), ('wf_act_set_altitude','wired_effect'),
    ('wf_act_show_message','wired_effect'), ('wf_act_teleport_to','wired_effect'),
    ('wf_act_teleport_to_room','wired_effect'), ('wf_act_toggle_state','wired_effect'),
    ('wf_act_toggle_to_rnd','wired_effect'), ('wf_act_unfreeze','wired_effect'),
    ('wf_act_user_to_furni','wired_effect'), ('wf_cnd_actor_dir','wired_condition'),
    ('wf_cnd_actor_in_group','wired_condition'), ('wf_cnd_actor_in_team','wired_condition'),
    ('wf_cnd_chest_has_item_type','wired_condition'), ('wf_cnd_chest_has_items','wired_condition'),
    ('wf_cnd_counter_time_matches','wired_condition'), ('wf_cnd_date_rng_active','wired_condition'),
    ('wf_cnd_furnis_hv_avtrs','wired_condition'), ('wf_cnd_has_altitude','wired_condition'),
    ('wf_cnd_has_furni_on','wired_condition'), ('wf_cnd_has_handitem','wired_condition'),
    ('wf_cnd_has_var','wired_condition'), ('wf_cnd_match_date','wired_condition'),
    ('wf_cnd_match_snapshot','wired_condition'), ('wf_cnd_match_time','wired_condition'),
    ('wf_cnd_neg_has_var','wired_condition'), ('wf_cnd_not_furni_on','wired_condition'),
    ('wf_cnd_not_has_handitem','wired_condition'), ('wf_cnd_not_hv_avtrs','wired_condition'),
    ('wf_cnd_not_in_group','wired_condition'), ('wf_cnd_not_in_team','wired_condition'),
    ('wf_cnd_not_match_snap','wired_condition'), ('wf_cnd_not_stuff_is','wired_condition'),
    ('wf_cnd_not_trggrer_on','wired_condition'), ('wf_cnd_not_triggerer_match','wired_condition'),
    ('wf_cnd_not_user_count','wired_condition'), ('wf_cnd_not_user_performs_action','wired_condition'),
    ('wf_cnd_not_wearing_b','wired_condition'), ('wf_cnd_not_wearing_fx','wired_condition'),
    ('wf_cnd_slc_quantity','wired_condition'), ('wf_cnd_stuff_is','wired_condition'),
    ('wf_cnd_team_has_rank','wired_condition'), ('wf_cnd_team_has_score','wired_condition'),
    ('wf_cnd_time_less_than','wired_condition'), ('wf_cnd_time_more_than','wired_condition'),
    ('wf_cnd_trggrer_on_frn','wired_condition'), ('wf_cnd_triggerer_match','wired_condition'),
    ('wf_cnd_user_count_in','wired_condition'), ('wf_cnd_user_performs_action','wired_condition'),
    ('wf_cnd_valid_moves','wired_condition'), ('wf_cnd_var_age_match','wired_condition'),
    ('wf_cnd_var_val_match','wired_condition'), ('wf_cnd_wearing_badge','wired_condition'),
    ('wf_cnd_wearing_effect','wired_condition'), ('wf_slc_furni_altitude','wired_selector'),
    ('wf_slc_furni_area','wired_selector'), ('wf_slc_furni_bytype','wired_selector'),
    ('wf_slc_furni_neighborhood','wired_selector'), ('wf_slc_furni_onfurni','wired_selector'),
    ('wf_slc_furni_picks','wired_selector'), ('wf_slc_furni_signal','wired_selector'),
    ('wf_slc_furni_with_var','wired_selector'), ('wf_slc_remote','wired_selector'),
    ('wf_slc_users_area','wired_selector'), ('wf_slc_users_byaction','wired_selector'),
    ('wf_slc_users_byname','wired_selector'), ('wf_slc_users_bytype','wired_selector'),
    ('wf_slc_users_group','wired_selector'), ('wf_slc_users_handitem','wired_selector'),
    ('wf_slc_users_neighborhood','wired_selector'), ('wf_slc_users_onfurni','wired_selector'),
    ('wf_slc_users_signal','wired_selector'), ('wf_slc_users_team','wired_selector'),
    ('wf_slc_users_with_var','wired_selector'), ('wf_trg_at_given_time','wired_trigger'),
    ('wf_trg_at_time_long','wired_trigger'), ('wf_trg_bot_reached_avtr','wired_trigger'),
    ('wf_trg_bot_reached_stf','wired_trigger'), ('wf_trg_click_furni','wired_trigger'),
    ('wf_trg_click_tile','wired_trigger'), ('wf_trg_click_user','wired_trigger'),
    ('wf_trg_clock_counter','wired_trigger'), ('wf_trg_collision','wired_trigger'),
    ('wf_trg_enter_room','wired_trigger'), ('wf_trg_game_ends','wired_trigger'),
    ('wf_trg_game_starts','wired_trigger'), ('wf_trg_leave_room','wired_trigger'),
    ('wf_trg_period_long','wired_trigger'), ('wf_trg_period_short','wired_trigger'),
    ('wf_trg_periodically','wired_trigger'), ('wf_trg_recv_signal','wired_trigger'),
    ('wf_trg_says_something','wired_trigger'), ('wf_trg_score_achieved','wired_trigger'),
    ('wf_trg_state_changed','wired_trigger'), ('wf_trg_stuff_state','wired_trigger'),
    ('wf_trg_transaction_complete','wired_trigger'), ('wf_trg_transaction_fail','wired_trigger'),
    ('wf_trg_user_performs_action','wired_trigger'), ('wf_trg_var_changed','wired_trigger'),
    ('wf_trg_walks_off_furni','wired_trigger'), ('wf_trg_walks_on_furni','wired_trigger'),
    ('wf_var_context','wired_variable'), ('wf_var_echo','wired_variable'), ('wf_var_furni','wired_variable'),
    ('wf_var_quest','wired_variable'), ('wf_var_quest_chain','wired_variable'), ('wf_var_reference','wired_variable'),
    ('wf_var_room','wired_variable'), ('wf_var_user','wired_variable'), ('wf_xtra_anim_time','wired_addon'),
    ('wf_xtra_custom_contract','wired_addon'), ('wf_xtra_exec_in_order','wired_addon'),
    ('wf_xtra_execution_limit','wired_addon'), ('wf_xtra_filter_furni','wired_addon'),
    ('wf_xtra_filter_furni_by_var','wired_addon'), ('wf_xtra_filter_users','wired_addon'),
    ('wf_xtra_filter_users_by_var','wired_addon'), ('wf_xtra_mov_carry_users','wired_addon'),
    ('wf_xtra_mov_curve','wired_addon'), ('wf_xtra_mov_no_animation','wired_addon'),
    ('wf_xtra_mov_physics','wired_addon'), ('wf_xtra_or_eval','wired_addon'), ('wf_xtra_random','wired_addon'),
    ('wf_xtra_rotate_to_dir','wired_addon'), ('wf_xtra_scan_chest_furni_by_type','wired_addon'),
    ('wf_xtra_text_input_variable','wired_addon'), ('wf_xtra_text_output_furni_name','wired_addon'),
    ('wf_xtra_text_output_username','wired_addon'), ('wf_xtra_text_output_variable','wired_addon'),
    ('wf_xtra_unseen','wired_addon'), ('wf_xtra_var_fx_boss','wired_addon'), ('wf_xtra_var_fx_health','wired_addon'),
    ('wf_xtra_var_fx_level','wired_addon'), ('wf_xtra_var_fx_number','wired_addon'),
    ('wf_xtra_var_fx_progress','wired_addon'), ('wf_xtra_var_fx_status','wired_addon'),
    ('wf_xtra_var_lvlup_system','wired_addon'), ('wf_xtra_var_text_connector','wired_addon'),
    ('wf_xtra_var_time_util','wired_addon');

UPDATE furniture f JOIN wired_canonical_79 w ON w.item_name = f.item_name
SET f.interaction_type = w.interaction
WHERE f.type = 's' AND f.interaction_type NOT IN (w.interaction, w.item_name);

DROP TEMPORARY TABLE wired_canonical_79;

-- 2. Boxes named after another client asset store the canonical name (Polaris ItemManager pairs them the same way).
UPDATE furniture SET interaction_type = 'wf_xtra_var_fx_boss'
WHERE item_name = 'wf_xtra_varfx_boss' AND type = 's' AND sprite_id = 19195 AND interaction_type <> 'wf_xtra_var_fx_boss';
UPDATE furniture SET interaction_type = 'wf_xtra_var_fx_health'
WHERE item_name = 'wf_xtra_varfx_hp' AND type = 's' AND sprite_id = 19196 AND interaction_type <> 'wf_xtra_var_fx_health';
UPDATE furniture SET interaction_type = 'wf_xtra_var_fx_level'
WHERE item_name = 'wf_xtra_varfx_levelling' AND type = 's' AND sprite_id = 19197 AND interaction_type <> 'wf_xtra_var_fx_level';
UPDATE furniture SET interaction_type = 'wf_xtra_var_fx_number'
WHERE item_name = 'wf_xtra_varfx_number' AND type = 's' AND sprite_id = 19198 AND interaction_type <> 'wf_xtra_var_fx_number';
UPDATE furniture SET interaction_type = 'wf_xtra_var_fx_progress'
WHERE item_name = 'wf_xtra_varfx_prog' AND type = 's' AND sprite_id = 19199 AND interaction_type <> 'wf_xtra_var_fx_progress';
UPDATE furniture SET interaction_type = 'wf_xtra_var_fx_status'
WHERE item_name = 'wf_xtra_varfx_status' AND type = 's' AND sprite_id = 19200 AND interaction_type <> 'wf_xtra_var_fx_status';
UPDATE furniture SET interaction_type = 'wf_trg_at_given_time'
WHERE item_name = 'wf_proto_trg_at_given_time' AND type = 's' AND sprite_id = 17678 AND interaction_type <> 'wf_trg_at_given_time';
UPDATE furniture SET interaction_type = 'wf_cnd_trggrer_on_frn'
WHERE item_name = 'wf_proto_cnd_trggrer_on_frn' AND type = 's' AND sprite_id = 17679 AND interaction_type <> 'wf_cnd_trggrer_on_frn';
UPDATE furniture SET interaction_type = 'wf_act_toggle_state'
WHERE item_name = 'wf_ltdproto_act_toggle_state' AND type = 's' AND sprite_id = 17676 AND interaction_type <> 'wf_act_toggle_state';

-- 3. Chests and contracts (WiredChestFurniture keys on these interaction types), counters, antennas, switches, click tile.
UPDATE furniture SET interaction_type = 'wired_chest_furni'
WHERE item_name IN ('wf_storage_furni1', 'wf_storage_furni2', 'wf_storage_furni_starter') AND type = 's' AND interaction_type <> 'wired_chest_furni';
UPDATE furniture SET interaction_type = 'wired_chest_coins'
WHERE item_name IN ('wf_storage_coins1', 'wf_storage_coins2') AND type = 's' AND interaction_type <> 'wired_chest_coins';
UPDATE furniture SET interaction_type = 'wired_contract_payment'
WHERE item_name = 'wf_contract_payment' AND type = 's' AND interaction_type <> 'wired_contract_payment';
UPDATE furniture SET interaction_type = 'wired_contract_reward'
WHERE item_name = 'wf_contract_reward' AND type = 's' AND interaction_type <> 'wired_contract_reward';
UPDATE furniture SET interaction_type = 'wired_contract_trade'
WHERE item_name = 'wf_contract_trade' AND type = 's' AND interaction_type <> 'wired_contract_trade';
-- WiredCounterController recognizes a Wired counter by its interaction name; 'counter' would make it a countdown timer.
UPDATE furniture SET interaction_type = item_name
WHERE item_name IN ('wf_upcounter1', 'wf_upcounter2', 'wf_game_upcounter1', 'wf_game_upcounter2', 'wf_floor_switch1', 'wf_floor_switch2')
  AND type = 's' AND interaction_type <> item_name;
UPDATE furniture SET interaction_type = 'antenna'
WHERE item_name IN ('wf_antenna1', 'wf_antenna2') AND type = 's' AND interaction_type <> 'antenna';
-- wf_trg_click_tile only accepts picks whose interaction is the invisible click tile (wiredfurni.error.require_click_tiles).
UPDATE furniture SET interaction_type = 'room_invisible_click_tile'
WHERE item_name = 'room_invisible_click_tile' AND type = 's' AND sprite_id = 14561 AND interaction_type <> 'room_invisible_click_tile';

-- 4. Furnidata names the catalogue shows for boxes that become public below (official furnidata only has placeholders).
UPDATE furniture SET name = 'WIRED Trigger: At Set Time (Long)', public_name = 'WIRED Trigger: At Set Time (Long)'
WHERE item_name = 'wf_trg_at_time_long' AND name = 'wf_trg_at_time_long name';
UPDATE furniture SET name = 'WIRED Negative Condition: User Is NOT Wearing Badge', public_name = 'WIRED Negative Condition: User Is NOT Wearing Badge'
WHERE item_name = 'wf_cnd_not_wearing_b' AND name = 'wf_cnd_not_wearing_b name';
UPDATE furniture SET name = 'WIRED Negative Condition: User Is NOT Wearing Effect', public_name = 'WIRED Negative Condition: User Is NOT Wearing Effect'
WHERE item_name = 'wf_cnd_not_wearing_fx' AND name = 'wf_cnd_not_wearing_fx name';

-- 5. Implemented boxes that were only on staff pages join the public Wired pages, priced like their page.
-- wf_act_give_reward stays staff-only: saving its settings requires the moderation tool permission.
CREATE TEMPORARY TABLE wired_offer_plan_79 (
    item_name VARCHAR(70) NOT NULL PRIMARY KEY,
    sprite_id INT NOT NULL,
    page_link VARCHAR(128) NOT NULL,
    cost_credits INT UNSIGNED NOT NULL,
    bulk_purchase TINYINT(1) NOT NULL,
    sort_order INT NOT NULL,
    furniture_id INT UNSIGNED NULL,
    page_id INT NULL,
    offer_id INT NULL
) ENGINE=MEMORY;
INSERT INTO wired_offer_plan_79 (item_name, sprite_id, page_link, cost_credits, bulk_purchase, sort_order) VALUES
    ('wf_trg_at_time_long', 5442, 'wired_triggers', 6, 1, 1),
    ('wf_act_move_furni_to', 5450, 'wired_effects', 7, 1, 1),
    ('wf_act_mute_triggerer', 5854, 'wired_effects', 7, 1, 2),
    ('wf_cnd_wearing_badge', 5094, 'wired_conditions', 5, 1, 1),
    ('wf_cnd_not_wearing_b', 5446, 'wired_conditions', 5, 1, 2),
    ('wf_cnd_wearing_effect', 5095, 'wired_conditions', 5, 1, 3),
    ('wf_cnd_not_wearing_fx', 5444, 'wired_conditions', 5, 1, 4),
    ('wf_cnd_date_rng_active', 5861, 'wired_conditions', 5, 1, 5),
    ('wf_var_quest', 16877, 'wired_variables', 7, 1, 1),
    ('wf_var_quest_chain', 16879, 'wired_variables', 7, 1, 2),
    ('wf_storage_furni_starter', 18490, 'wired_chests', 0, 0, 1);

-- The client's furnidata entry (has_furnidata) with the official sprite, and the page by its link.
UPDATE wired_offer_plan_79 p
JOIN furniture f ON f.furnidata_classname = p.item_name AND f.type = 's' AND f.sprite_id = p.sprite_id
SET p.furniture_id = f.id;
UPDATE wired_offer_plan_79 p JOIN catalog_pages c ON c.link = p.page_link SET p.page_id = c.id;

-- Already sold on that page (any offer whose product is this furniture): nothing to add.
DELETE p FROM wired_offer_plan_79 p
WHERE p.furniture_id IS NULL OR p.page_id IS NULL OR EXISTS (
    SELECT 1 FROM catalog_page_offers po
    JOIN catalog_offer_products op ON op.offer_id = po.offer_id AND op.product_type = 'furni'
    WHERE po.page_id = p.page_id AND op.furniture_id = p.furniture_id);

SET @wired_offer_floor_79 = (SELECT COALESCE(MAX(id), 0) FROM catalog_offers);
INSERT INTO catalog_offers (localization_key, cost_credits, cost_points, points_type, club_level, bulk_purchase, enabled, preview_image)
SELECT p.item_name, p.cost_credits, 0, 0, 0, p.bulk_purchase, 1, ''
FROM wired_offer_plan_79 p ORDER BY p.page_link, p.sort_order;

-- New offers are the ones above the floor; localization keys are the item names inserted just now.
UPDATE wired_offer_plan_79 p
JOIN catalog_offers o ON o.id > @wired_offer_floor_79 AND o.localization_key = p.item_name
SET p.offer_id = o.id;

INSERT INTO catalog_offer_products (offer_id, position, product_type, furniture_id, amount, extra_param)
SELECT p.offer_id, 0, 'furni', p.furniture_id, 1, '' FROM wired_offer_plan_79 p WHERE p.offer_id IS NOT NULL;

-- Appended after the page's current offers, in plan order.
INSERT INTO catalog_page_offers (page_id, offer_id, position)
SELECT p.page_id, p.offer_id,
       (SELECT COALESCE(MAX(po.position), -1) FROM catalog_page_offers po WHERE po.page_id = p.page_id) + p.sort_order
FROM wired_offer_plan_79 p WHERE p.offer_id IS NOT NULL;

DROP TEMPORARY TABLE wired_offer_plan_79;

COMMIT;
