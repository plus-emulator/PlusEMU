-- State counts: more classes get the count Habbo cycles through. A placed item above the new last state moves to that last state.
UPDATE furniture SET interaction_modes_count = 2 WHERE has_furnidata = 1 AND interaction_modes_count <> 2 AND item_name IN ('school_urinal');

UPDATE furniture SET interaction_modes_count = 3 WHERE has_furnidata = 1 AND interaction_modes_count <> 3 AND item_name IN ('thai_c21_tropicalbg', 'uni_moustache');

UPDATE furniture SET interaction_modes_count = 8 WHERE has_furnidata = 1 AND interaction_modes_count <> 8 AND item_name IN ('hween_c18_staffoftheyr', 'uni_hipdeer');

UPDATE items i JOIN furniture f ON f.id = i.base_item SET i.extra_data = '1' WHERE f.has_furnidata = 1 AND f.item_name IN ('school_urinal') AND i.extra_data REGEXP '^[0-9]+$' AND CAST(i.extra_data AS UNSIGNED) >= 2;

UPDATE items i JOIN furniture f ON f.id = i.base_item SET i.extra_data = '7' WHERE f.has_furnidata = 1 AND f.item_name IN ('hween_c18_staffoftheyr') AND i.extra_data REGEXP '^[0-9]+$' AND CAST(i.extra_data AS UNSIGNED) >= 8;

-- Hand items given by vending style classes.
UPDATE furniture SET vending_ids = '148' WHERE has_furnidata = 1 AND vending_ids = '0' AND item_name = 'antique_c21_magnifyinglass';
UPDATE furniture SET vending_ids = '103' WHERE has_furnidata = 1 AND vending_ids = '0' AND item_name = 'olympics_c16_nanarack';
UPDATE furniture SET vending_ids = '99' WHERE has_furnidata = 1 AND vending_ids = '0' AND item_name = 'paris_c15_vegstall2';
UPDATE furniture SET vending_ids = '98' WHERE has_furnidata = 1 AND vending_ids = '0' AND item_name = 'paris_c15_vegstall3';
UPDATE furniture SET vending_ids = '122' WHERE has_furnidata = 1 AND vending_ids = '0' AND item_name = 'tokyo_c18_snackdisplay';
