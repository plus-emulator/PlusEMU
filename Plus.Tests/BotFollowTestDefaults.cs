using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items.Wired.Modern.Actions;

namespace Plus.Tests;

internal static class BotFollowTestDefaults
{
    // Tests that do not look at the start chance get a following bot that always starts. A test that does sets its own roll.
    [ModuleInitializer]
    internal static void Configure() => WiredBotTargets.DefaultStartRoll = () => 0;
}
