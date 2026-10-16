namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // The rest of room 14's floor furni as stored. The Web API add-on (1009) is left out: it needs the web API services to load.
    private static (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[] Room14OtherRows() =>
    [
        (68, 9, 10, 0.0, 0, "wf_act_log", null),
        (74, 10, 9, 0.0, 0, "CF_10_coin_gold", null),
        (75, 9, 8, 0.0, 0, "CF_10_coin_gold", null),
        (78, 5, 7, 0.0, 0, "wf_colortile", null),
        (79, 4, 7, 0.0, 0, "wf_colortile", null),
        (80, 4, 8, 0.0, 0, "wf_colortile", null),
        (81, 5, 8, 0.0, 0, "wf_colortile", null),
        (82, 6, 8, 0.0, 0, "wf_colortile", null),
        (83, 6, 6, 0.0, 0, "wf_colortile", null),
        (84, 5, 6, 0.0, 0, "wf_colortile", null),
        (85, 6, 7, 0.0, 0, "wf_colortile", null),
        (87, 4, 6, 0.0, 0, "wf_colortile", null),
        (286, 8, 7, 0.0, 0, "sofachair_polyfon", null),
        (287, 8, 6, 0.0, 0, "sofachair_polyfon", null),
        (288, 8, 4, 0.0, 2, "sofachair_polyfon", null),
        (289, 8, 5, 0.0, 2, "sofachair_polyfon", null),
        (307, 16, 17, 0.0, 0, "tile_stackmagic2", null),
        (308, 10, 1, 3.6, 0, "hween_c26_wornfloorboards", null),
        (322, 11, 4, 0.0, 0, "wf_storage_furni1", null),
        (323, 11, 5, 0.0, 0, "wf_storage_coins2", null),
        (324, 11, 3, 0.0, 0, "wf_storage_furni2", null),
        (325, 11, 6, 0.0, 0, "wf_storage_furni1", null),
        (977, 8, 10, 0.0, 2, "wf_act_change_var_val", null),
        (983, 3, 15, 0.0, 0, "wf_xtra_text_output_variable", null),
    ];
}
