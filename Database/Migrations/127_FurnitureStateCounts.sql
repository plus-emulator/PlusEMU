-- Gives each furniture class the state count Habbo uses, so a double-click and Toggle State cycle through the same states.
-- A placed item whose stored state is above the new last state moves to that last state. Idempotent: a rerun changes nothing.

UPDATE furniture SET interaction_modes_count = 2 WHERE has_furnidata = 1 AND interaction_modes_count <> 2 AND item_name IN ('SF_lamp', 'bazaar_c17_clothroof2', 'bazaar_c17_curtain', 'bubblejuice_c21_window', 'catcafe_c23_table', 'celestial_c19_tree', 'classic2_floor', 'classic5_wall', 'classic6_lantern', 'classic7_crn', 'classic7_gate', 'classic9_wall', 'coralking_c18_gate', 'coralking_c18_neoncoral1', 'coralking_c18_neoncoral3', 'cpunk_c15_door', 'cpunk_c15_searchlight', 'cpunk_c15_traffic', 'dino_c15_floor', 'disco_c26_blueglitterbomb', 'disco_c26_goldglitterbomb', 'disco_c26_greenglitterbomb', 'disco_c26_pinkglitterbomb', 'disco_c26_purpleglitterbomb', 'disco_c26_redglitterbomb', 'disco_c26_silverglitterbomb', 'exe_gate', 'gift_mypod', 'gift_thing', 'gift_vase', 'hween_c20_secretbkshelf', 'hween_c21_shojislidingdoor', 'hween_c21_shojislidingdoor2', 'hween_c22_gothicstairs', 'hween_c22_gothicstatue', 'hween_c23_sphinx', 'hygge_c25_sponges', 'ktchn_c15_table_b', 'ktchn_c15_table_g', 'ktchn_c15_table_p', 'lt_c26_bobbapoof', 'lt_c26_stonegate', 'lt_c26_stonestairs', 'mafia_c26_coffeetable_dark', 'mystics_bspike1', 'nyc_c23_roaditem2', 'olympus_c25_floor1', 'paris_c15_table', 'sfx_duck', 'sfx_funkhorn', 'sfx_glass', 'sfx_highhatintro', 'sfx_lazer', 'sfx_onetwo', 'sfx_pad1', 'sfx_pad2', 'sfx_pad3', 'sfx_sqrpad_dbldotted', 'sfx_strange_echoes_1', 'sfx_strange_echoes_2', 'sfx_strange_echoes_3', 'sfx_whistle', 'sfx_xylo', 'sfx_xylo2', 'sfx_xylo_high', 'sfx_xylopattern', 'skorea_c22_signbig2', 'st_hween14_jack', 'sunsetcafe_c20_swordfish', 'tokyo_c18_gamesign', 'tokyo_c18_lanterns', 'tokyo_c18_trellis', 'tv_flat', 'uni_c15_lamp', 'uni_rubik', 'usva2_shelf2', 'usva3_shelf2', 'usva_shelf2', 'val14_door', 'val14_gate', 'wf_antenna1', 'wf_antenna2', 'wf_arrowplate', 'wf_arrowplate_8dir', 'wf_fx_firegate', 'wf_pressureplate', 'wf_pyramid', 'wf_ringplate', 'wildwest_cactus1', 'wildwest_crate');

UPDATE furniture SET interaction_modes_count = 3 WHERE has_furnidata = 1 AND interaction_modes_count <> 3 AND item_name IN ('SF_panel1', 'army_c15_food', 'fantasy_c22_hangingflags', 'fest_c19_pole', 'hween_c15_road', 'nyc_c23_roaditem1');

UPDATE furniture SET interaction_modes_count = 4 WHERE has_furnidata = 1 AND interaction_modes_count <> 4 AND item_name IN ('fantasy_c22_adventureboard', 'fantasy_c22_gnoll', 'hween_c20_brokenfloor', 'hygge_c25_kitchensink', 'hygge_c25_soap', 'lt_c26_banana', 'lt_c26_coin', 'mexico_c24_wall3', 'paris_c15_parasol', 'ranch_c26_dustpath', 'skorea_c22_signsmall');

UPDATE furniture SET interaction_modes_count = 5 WHERE has_furnidata = 1 AND interaction_modes_count <> 5 AND item_name IN ('greek_c15_floor');

UPDATE furniture SET interaction_modes_count = 6 WHERE has_furnidata = 1 AND interaction_modes_count <> 6 AND item_name IN ('hween11_pumpkin', 'hween_c19_claddedwall', 'hween_c19_stonewall', 'uni_c15_shelvescrn');

UPDATE furniture SET interaction_modes_count = 8 WHERE has_furnidata = 1 AND interaction_modes_count <> 8 AND item_name IN ('rainyday_c20_retrogames');

UPDATE furniture SET interaction_modes_count = 9 WHERE has_furnidata = 1 AND interaction_modes_count <> 9 AND item_name IN ('wildwest_floor', 'wildwest_floor2');

UPDATE furniture SET interaction_modes_count = 10 WHERE has_furnidata = 1 AND interaction_modes_count <> 10 AND item_name IN ('wf_teamcolortile');

UPDATE items i JOIN furniture f ON f.id = i.base_item SET i.extra_data = '1' WHERE f.has_furnidata = 1 AND f.item_name IN ('SF_lamp', 'cpunk_c15_door', 'cpunk_c15_traffic', 'disco_c26_blueglitterbomb', 'disco_c26_goldglitterbomb', 'disco_c26_greenglitterbomb', 'disco_c26_pinkglitterbomb', 'disco_c26_purpleglitterbomb', 'disco_c26_redglitterbomb', 'disco_c26_silverglitterbomb', 'exe_gate', 'hween_c20_secretbkshelf', 'hween_c21_shojislidingdoor', 'hween_c21_shojislidingdoor2', 'lt_c26_bobbapoof', 'lt_c26_stonegate', 'sfx_duck', 'sfx_funkhorn', 'sfx_glass', 'sfx_highhatintro', 'sfx_lazer', 'sfx_onetwo', 'sfx_pad1', 'sfx_pad2', 'sfx_pad3', 'sfx_sqrpad_dbldotted', 'sfx_strange_echoes_1', 'sfx_strange_echoes_2', 'sfx_strange_echoes_3', 'sfx_whistle', 'sfx_xylo', 'sfx_xylo2', 'sfx_xylo_high', 'sfx_xylopattern', 'st_hween14_jack', 'val14_door', 'val14_gate', 'wf_antenna1', 'wf_antenna2', 'wf_arrowplate', 'wf_arrowplate_8dir', 'wf_fx_firegate', 'wf_pressureplate', 'wf_pyramid', 'wf_ringplate') AND i.extra_data REGEXP '^[0-9]+$' AND CAST(i.extra_data AS UNSIGNED) >= 2;

UPDATE items i JOIN furniture f ON f.id = i.base_item SET i.extra_data = '2' WHERE f.has_furnidata = 1 AND f.item_name IN ('SF_panel1') AND i.extra_data REGEXP '^[0-9]+$' AND CAST(i.extra_data AS UNSIGNED) >= 3;

UPDATE items i JOIN furniture f ON f.id = i.base_item SET i.extra_data = '3' WHERE f.has_furnidata = 1 AND f.item_name IN ('hygge_c25_kitchensink', 'lt_c26_banana', 'lt_c26_coin', 'ranch_c26_dustpath') AND i.extra_data REGEXP '^[0-9]+$' AND CAST(i.extra_data AS UNSIGNED) >= 4;

UPDATE items i JOIN furniture f ON f.id = i.base_item SET i.extra_data = '5' WHERE f.has_furnidata = 1 AND f.item_name IN ('hween11_pumpkin') AND i.extra_data REGEXP '^[0-9]+$' AND CAST(i.extra_data AS UNSIGNED) >= 6;

UPDATE items i JOIN furniture f ON f.id = i.base_item SET i.extra_data = '7' WHERE f.has_furnidata = 1 AND f.item_name IN ('rainyday_c20_retrogames') AND i.extra_data REGEXP '^[0-9]+$' AND CAST(i.extra_data AS UNSIGNED) >= 8;

UPDATE items i JOIN furniture f ON f.id = i.base_item SET i.extra_data = '9' WHERE f.has_furnidata = 1 AND f.item_name IN ('wf_teamcolortile') AND i.extra_data REGEXP '^[0-9]+$' AND CAST(i.extra_data AS UNSIGNED) >= 10;
