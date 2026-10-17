-- Habbo's 69 recolorable Builders Club furni (*_recolorable): the building blocks and the alphabet blocks whose colour is
-- picked per furni. Furnidata as on hhus (AIR WIN63-202610131318-860371703): sprite id, name, size, height, stack/stand flags,
-- default direction, revision, line, Builders Club offer id; Habbo sells them only in Builders Club (offer id -1).
-- They use the 'recolorable' interaction: a multi-state furni whose state goes out as MapData with its colour (r, g, b).
-- interaction_modes_count is the number of states each furni's visualization animates (furniture_recolorable assets of
-- the same revision).
-- This hotel sells the Builders Club blocks on the ordinary Furni pages 'Building Blocks' and 'Alphabet Blocks' for one
-- credit each, so the recolorable ones get two sibling pages right after them, in hhus' Builders Club page order
-- (bc_blocks_recolorable, bc_alphabet_recolorable), with the official offer ids where they are free.
-- Idempotent: reruns end in the same state.

START TRANSACTION;

CREATE TEMPORARY TABLE recolorable_furni_87 (
    item_name VARCHAR(70) NOT NULL PRIMARY KEY,
    public_name VARCHAR(56) NOT NULL,
    sprite_id INT NOT NULL,
    width INT NOT NULL,
    length INT NOT NULL,
    height DOUBLE NOT NULL,
    can_stack TINYINT NOT NULL,
    can_stand_on TINYINT NOT NULL,
    can_sit_on TINYINT NOT NULL,
    can_lay_on TINYINT NOT NULL,
    default_dir INT NOT NULL,
    revision INT NOT NULL,
    special_type INT NOT NULL,
    recyclable TINYINT NOT NULL,
    category VARCHAR(64) NOT NULL,
    furni_line VARCHAR(64) NOT NULL,
    tradeable TINYINT NOT NULL,
    modes INT NOT NULL,
    official_offer_id INT NOT NULL,
    furniture_id INT UNSIGNED NULL,
    page_link VARCHAR(128) NOT NULL,
    position INT NOT NULL,
    offer_id INT NULL
) ENGINE=MEMORY;
INSERT INTO recolorable_furni_87 VALUES
    ('bc_block_0_recolorable', 'Small Block', 19348, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35202, NULL, 'bc_blocks_recolorable', 0, NULL),
    ('bc_block_1_recolorable', 'Large Block', 19349, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35203, NULL, 'bc_blocks_recolorable', 1, NULL),
    ('bc_block_small_recolorable', 'Small Block', 19358, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35212, NULL, 'bc_blocks_recolorable', 2, NULL),
    ('bc_panel_recolorable', 'Panel Block', 19370, 2, 1, 2.01, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35224, NULL, 'bc_blocks_recolorable', 3, NULL),
    ('bc_stick_recolorable', 'Stick Block', 19381, 1, 1, 4, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35235, NULL, 'bc_blocks_recolorable', 4, NULL),
    ('bc_stairs_recolorable', 'Stair Block', 19378, 1, 1, 0.5, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35232, NULL, 'bc_blocks_recolorable', 5, NULL),
    ('bc_ramp_recolorable', 'Ramp Block', 19374, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 3, 35228, NULL, 'bc_blocks_recolorable', 6, NULL),
    ('bc_curvedramp_recolorable', 'Curved Ramp', 19362, 1, 1, 1, 0, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35216, NULL, 'bc_blocks_recolorable', 7, NULL),
    ('bc_wedge_recolorable', 'Wedge Block', 19386, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 3, 35240, NULL, 'bc_blocks_recolorable', 8, NULL),
    ('bc_wedgecrnr_recolorable', 'Wedge Corner', 19387, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 3, 35241, NULL, 'bc_blocks_recolorable', 9, NULL),
    ('bc_triangularprism_recolorable', 'Triangular Prism', 19385, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 2, 35239, NULL, 'bc_blocks_recolorable', 10, NULL),
    ('bc_standingtriangularprism_recolorable', 'Standing Triangular Prism', 19380, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35234, NULL, 'bc_blocks_recolorable', 11, NULL),
    ('bc_pyramidtip_recolorable', 'Pyramid Block', 19371, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 3, 35225, NULL, 'bc_blocks_recolorable', 12, NULL),
    ('bc_cone_recolorable', 'Cone Block', 19361, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 4, 35215, NULL, 'bc_blocks_recolorable', 13, NULL),
    ('bc_cylinder_recolorable', 'Cylinder Block', 19363, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35217, NULL, 'bc_blocks_recolorable', 14, NULL),
    ('bc_halfcylinder_recolorable', 'Half Cylinder', 19366, 1, 1, 0.6, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 2, 35220, NULL, 'bc_blocks_recolorable', 15, NULL),
    ('bc_round_recolorable', 'Round Block', 19375, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35229, NULL, 'bc_blocks_recolorable', 16, NULL),
    ('bc_quartercircle_recolorable', 'Quarter Circle Block', 19372, 1, 1, 1, 0, 0, 0, 0, 2, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35226, NULL, 'bc_blocks_recolorable', 17, NULL),
    ('bc_quarterring_recolorable', 'Quarter Ring Block', 19373, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 4, 35227, NULL, 'bc_blocks_recolorable', 18, NULL),
    ('bc_sphere_recolorable', 'Sphere Block', 19376, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35230, NULL, 'bc_blocks_recolorable', 19, NULL),
    ('bc_sphere_small_recolorable', 'Small Sphere', 19377, 1, 1, 1, 0, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35231, NULL, 'bc_blocks_recolorable', 20, NULL),
    ('bc_standinghalfcylinder_recolorable', 'Standing Half Cylinder', 19379, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35233, NULL, 'bc_blocks_recolorable', 21, NULL),
    ('bc_hemisphere_recolorable', 'Hemisphere Block', 19367, 1, 1, 0.6, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 4, 35221, NULL, 'bc_blocks_recolorable', 22, NULL),
    ('bc_block_glass_recolorable', 'Glass Block', 19351, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35205, NULL, 'bc_blocks_recolorable', 23, NULL),
    ('bc_glasspanel_recolorable', 'Glass Panel', 19365, 1, 1, 2, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35219, NULL, 'bc_blocks_recolorable', 24, NULL),
    ('bc_wood_recolorable', 'Simple Wood Block', 19388, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35242, NULL, 'bc_blocks_recolorable', 25, NULL),
    ('bc_block_redbrick_recolorable', 'Brick Block', 19356, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35210, NULL, 'bc_blocks_recolorable', 26, NULL),
    ('bc_block_marble_recolorable', 'Marble Block', 19354, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35208, NULL, 'bc_blocks_recolorable', 27, NULL),
    ('bc_tile_recolorable', 'Tile Block', 19384, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35238, NULL, 'bc_blocks_recolorable', 28, NULL),
    ('bc_block_artdeco1_recolorable', 'Art Deco Block', 19350, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35204, NULL, 'bc_blocks_recolorable', 29, NULL),
    ('bc_block_industrial_recolorable', 'Industrial Block', 19353, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35207, NULL, 'bc_blocks_recolorable', 30, NULL),
    ('bc_block_metalgrip_recolorable', 'Metal Block', 19355, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35209, NULL, 'bc_blocks_recolorable', 31, NULL),
    ('bc_metalcrate_recolorable', 'Metal Crate Block', 19369, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35223, NULL, 'bc_blocks_recolorable', 32, NULL),
    ('bc_block_wool_recolorable', 'Wool Block', 19360, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35214, NULL, 'bc_blocks_recolorable', 33, NULL),
    ('bc_stone_recolorable', 'Stone Block', 19382, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35236, NULL, 'bc_blocks_recolorable', 34, NULL),
    ('bc_terra_recolorable', 'Terra Block', 19383, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 2, 35237, NULL, 'bc_blocks_recolorable', 35, NULL),
    ('bc_block_grass_recolorable', 'Grass Block', 19352, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35206, NULL, 'bc_blocks_recolorable', 36, NULL),
    ('bc_flowerhedge_recolorable', 'Flower Hedge', 19364, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35218, NULL, 'bc_blocks_recolorable', 37, NULL),
    ('bc_block_sand_recolorable', 'Sand Block', 19357, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35211, NULL, 'bc_blocks_recolorable', 38, NULL),
    ('bc_block_water_recolorable', 'Water Block', 19359, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 2, 35213, NULL, 'bc_blocks_recolorable', 39, NULL),
    ('bc_lavarock_recolorable', 'Lava Block', 19368, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 1, 35222, NULL, 'bc_blocks_recolorable', 40, NULL),
    ('bc_alpha1_num_recolorable', 'Numeric Block', 19335, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 10, 35189, NULL, 'bc_alphabet_recolorable', 0, NULL),
    ('bc_alpha1_alpha_recolorable', 'Alphabet Block', 19321, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 26, 35175, NULL, 'bc_alphabet_recolorable', 1, NULL),
    ('bc_alpha1_a_recolorable', 'A Block', 19320, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35174, NULL, 'bc_alphabet_recolorable', 2, NULL),
    ('bc_alpha1_b_recolorable', 'B Block', 19322, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35176, NULL, 'bc_alphabet_recolorable', 3, NULL),
    ('bc_alpha1_c_recolorable', 'C Block', 19323, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35177, NULL, 'bc_alphabet_recolorable', 4, NULL),
    ('bc_alpha1_d_recolorable', 'D Block', 19324, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35178, NULL, 'bc_alphabet_recolorable', 5, NULL),
    ('bc_alpha1_e_recolorable', 'E Block', 19325, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35179, NULL, 'bc_alphabet_recolorable', 6, NULL),
    ('bc_alpha1_f_recolorable', 'F Block', 19326, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35180, NULL, 'bc_alphabet_recolorable', 7, NULL),
    ('bc_alpha1_g_recolorable', 'G Block', 19327, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35181, NULL, 'bc_alphabet_recolorable', 8, NULL),
    ('bc_alpha1_h_recolorable', 'H Block', 19328, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35182, NULL, 'bc_alphabet_recolorable', 9, NULL),
    ('bc_alpha1_i_recolorable', 'I Block', 19329, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35183, NULL, 'bc_alphabet_recolorable', 10, NULL),
    ('bc_alpha1_j_recolorable', 'J Block', 19330, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35184, NULL, 'bc_alphabet_recolorable', 11, NULL),
    ('bc_alpha1_k_recolorable', 'K Block', 19331, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35185, NULL, 'bc_alphabet_recolorable', 12, NULL),
    ('bc_alpha1_l_recolorable', 'L Block', 19332, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35186, NULL, 'bc_alphabet_recolorable', 13, NULL),
    ('bc_alpha1_m_recolorable', 'M Block', 19333, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35187, NULL, 'bc_alphabet_recolorable', 14, NULL),
    ('bc_alpha1_n_recolorable', 'N Block', 19334, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35188, NULL, 'bc_alphabet_recolorable', 15, NULL),
    ('bc_alpha1_o_recolorable', 'O Block', 19336, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35190, NULL, 'bc_alphabet_recolorable', 16, NULL),
    ('bc_alpha1_p_recolorable', 'P Block', 19337, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35191, NULL, 'bc_alphabet_recolorable', 17, NULL),
    ('bc_alpha1_q_recolorable', 'Q Block', 19338, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35192, NULL, 'bc_alphabet_recolorable', 18, NULL),
    ('bc_alpha1_r_recolorable', 'R Block', 19339, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35193, NULL, 'bc_alphabet_recolorable', 19, NULL),
    ('bc_alpha1_s_recolorable', 'S Block', 19340, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35194, NULL, 'bc_alphabet_recolorable', 20, NULL),
    ('bc_alpha1_t_recolorable', 'T Block', 19341, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35195, NULL, 'bc_alphabet_recolorable', 21, NULL),
    ('bc_alpha1_u_recolorable', 'U Block', 19342, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35196, NULL, 'bc_alphabet_recolorable', 22, NULL),
    ('bc_alpha1_v_recolorable', 'V Block', 19343, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35197, NULL, 'bc_alphabet_recolorable', 23, NULL),
    ('bc_alpha1_w_recolorable', 'W Block', 19344, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35198, NULL, 'bc_alphabet_recolorable', 24, NULL),
    ('bc_alpha1_x_recolorable', 'X Block', 19345, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35199, NULL, 'bc_alphabet_recolorable', 25, NULL),
    ('bc_alpha1_y_recolorable', 'Y Block', 19346, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35200, NULL, 'bc_alphabet_recolorable', 26, NULL),
    ('bc_alpha1_z_recolorable', 'Z Block', 19347, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 1, 35201, NULL, 'bc_alphabet_recolorable', 27, NULL);

-- 1. Furniture definitions, matched by furnidata class name or sprite id like the client does.
INSERT INTO furniture (item_name, public_name, type, width, length, stack_height, can_stack, can_sit, is_walkable, sprite_id,
    allow_recycle, allow_trade, allow_marketplace_sell, allow_gift, allow_inventory_stack, interaction_type, interaction_modes_count,
    has_furnidata, revision, category, default_dir, xdim, ydim, name, description, custom_params, special_type,
    can_stand_on, can_sit_on, can_lay_on, can_put_stuff_on, height, furni_line, rare, tradeable, recyclable)
SELECT r.item_name, r.public_name, 's', r.width, r.length, r.height, r.can_stack, r.can_sit_on, r.can_stand_on, r.sprite_id,
    r.recyclable, r.tradeable, r.tradeable, r.tradeable, 1, 'recolorable', r.modes,
    1, r.revision, r.category, r.default_dir, r.width, r.length, r.public_name, CONCAT(r.item_name, ' desc'), NULL, r.special_type,
    r.can_stand_on, r.can_sit_on, r.can_lay_on, r.can_stack, r.height, r.furni_line, 0, r.tradeable, r.recyclable
FROM recolorable_furni_87 r
WHERE NOT EXISTS (SELECT 1 FROM furniture f WHERE f.type = 's' AND f.furnidata_classname = r.item_name)
  AND NOT EXISTS (SELECT 1 FROM furniture f WHERE f.type = 's' AND f.furnidata_sprite_id = r.sprite_id);

UPDATE furniture f JOIN recolorable_furni_87 r ON f.type = 's' AND f.item_name = r.item_name
SET f.interaction_type = 'recolorable', f.interaction_modes_count = r.modes
WHERE f.interaction_type IN ('', 'default', 'recolorable');

UPDATE recolorable_furni_87 r JOIN furniture f ON f.type = 's' AND f.item_name = r.item_name SET r.furniture_id = f.id;

-- 2. Pages next to the existing Builders Club block pages under Furni.
INSERT INTO catalog_pages (parent_id, caption, icon, position, link, layout, visible, enabled, required_club_level)
SELECT p.id, 'Recolorable Blocks', 28, 16, 'bc_blocks_recolorable', 'default_3x3', 1, 1, 0 FROM catalog_pages p
WHERE p.link = 'furni' AND NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'bc_blocks_recolorable');
INSERT INTO catalog_pages (parent_id, caption, icon, position, link, layout, visible, enabled, required_club_level)
SELECT p.id, 'Recolorable Alphabet Blocks', 101, 17, 'bc_alphabet_recolorable', 'default_3x3', 1, 1, 0 FROM catalog_pages p
WHERE p.link = 'furni' AND NOT EXISTS (SELECT 1 FROM catalog_pages c WHERE c.link = 'bc_alphabet_recolorable');

INSERT IGNORE INTO catalog_page_images (page_id, slot, image)
SELECT c.id, s.slot, CASE
        WHEN c.link = 'bc_blocks_recolorable' AND s.slot = 1 THEN 'catalog_teaser_bcblocks'
        WHEN c.link = 'bc_alphabet_recolorable' AND s.slot = 0 THEN 'catalog_header_alpha1'
        WHEN c.link = 'bc_alphabet_recolorable' AND s.slot = 1 THEN 'catalog_teaser_alpha1'
        ELSE '' END
FROM catalog_pages c JOIN (SELECT 0 AS slot UNION ALL SELECT 1 UNION ALL SELECT 2) s
WHERE c.link IN ('bc_blocks_recolorable', 'bc_alphabet_recolorable');
INSERT IGNORE INTO catalog_page_texts (page_id, slot, text)
SELECT c.id, s.slot, '' FROM catalog_pages c JOIN (SELECT 0 AS slot UNION ALL SELECT 1 UNION ALL SELECT 2) s
WHERE c.link IN ('bc_blocks_recolorable', 'bc_alphabet_recolorable');

-- 3. Offers: one credit like the other Builders Club blocks here. An offer already selling the furni under its name is kept;
-- otherwise the official offer id is used when it is free.
UPDATE recolorable_furni_87 r
JOIN catalog_offer_products op ON op.furniture_id = r.furniture_id AND op.product_type = 'furni' AND op.position = 0
JOIN catalog_offers o ON o.id = op.offer_id AND o.localization_key = r.item_name
SET r.offer_id = o.id
WHERE NOT EXISTS (SELECT 1 FROM catalog_offer_products other WHERE other.offer_id = op.offer_id AND other.position > 0);

INSERT INTO catalog_offers (id, localization_key, cost_credits, cost_points, points_type, club_level, bulk_purchase, enabled, preview_image)
SELECT r.official_offer_id, r.item_name, 1, 0, 0, 0, 1, 1, ''
FROM recolorable_furni_87 r
WHERE r.offer_id IS NULL AND r.furniture_id IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM catalog_offers o WHERE o.id = r.official_offer_id);
UPDATE recolorable_furni_87 r JOIN catalog_offers o ON o.id = r.official_offer_id AND o.localization_key = r.item_name
SET r.offer_id = o.id WHERE r.offer_id IS NULL;

SET @recolorable_offer_floor_87 = (SELECT COALESCE(MAX(id), 0) FROM catalog_offers);
INSERT INTO catalog_offers (localization_key, cost_credits, cost_points, points_type, club_level, bulk_purchase, enabled, preview_image)
SELECT r.item_name, 1, 0, 0, 0, 1, 1, ''
FROM recolorable_furni_87 r WHERE r.offer_id IS NULL AND r.furniture_id IS NOT NULL ORDER BY r.page_link, r.position;
UPDATE recolorable_furni_87 r JOIN catalog_offers o ON o.id > @recolorable_offer_floor_87 AND o.localization_key = r.item_name
SET r.offer_id = o.id WHERE r.offer_id IS NULL;

INSERT INTO catalog_offer_products (offer_id, position, product_type, furniture_id, amount, extra_param)
SELECT r.offer_id, 0, 'furni', r.furniture_id, 1, ''
FROM recolorable_furni_87 r
WHERE r.offer_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM catalog_offer_products op WHERE op.offer_id = r.offer_id);

-- 4. Page contents in hhus' order.
INSERT INTO catalog_page_offers (page_id, offer_id, position)
SELECT c.id, r.offer_id, r.position
FROM recolorable_furni_87 r JOIN catalog_pages c ON c.link = r.page_link
WHERE r.offer_id IS NOT NULL
ON DUPLICATE KEY UPDATE position = VALUES(position);

DROP TEMPORARY TABLE recolorable_furni_87;

COMMIT;
