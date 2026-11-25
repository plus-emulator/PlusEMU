namespace Plus.HabboHotel.Rooms.AI;

/// <summary>A moment in which a pet answers in chat.</summary>
public enum PetVocal
{
    Refusal,
    UnknownCommand,
    Scratched,
    Eating,
    Drinking,
    Sleeping,
    Playing
}

/// <summary>
/// Habbo's pet answers. Every pet type can refuse with the shared refusal lines and adds its own lines; all pets share the playing lines.
/// A pet type without lines for a moment stays silent.
/// </summary>
public static class PetVocals
{
    private static readonly string[] SharedRefusals =
        ["Maybe I'll do it later", "I'd rather not do it...", "Yes, yes, I'll do it later", "Well, I'm not going to do it..."];

    private static readonly string[] SharedPlaying =
        ["I'm having a blast!", "I can't stop playing!", "See! I knew this would be fun!", "Wooo! Yay!", "Whee!"];

    private static readonly Dictionary<(int Type, PetVocal Vocal), string[]> Lines = new()
    {
        [(0, PetVocal.Refusal)] = ["*so not going to do that*", "*could not care less about your command*"],
        [(0, PetVocal.UnknownCommand)] = ["*does not get the drift*", "*does not understand*"],
        [(0, PetVocal.Scratched)] = ["*grinning*"],
        [(1, PetVocal.Refusal)] = ["*Why are you treating me like this?*", "*I hear you, but I'm not listening*", "*I don't think so*"],
        [(1, PetVocal.UnknownCommand)] = ["*I don't understand*", "*Not getting that*", "*Excuse me?*"],
        [(8, PetVocal.Refusal)] = ["*hiss*", "Never!"],
        [(8, PetVocal.UnknownCommand)] = ["You confound me with your tongue!", "I do not understand you, master.", "What are you saying to me?"],
        [(11, PetVocal.Refusal)] = ["Not doing that, I'd rather croak!"],
        [(11, PetVocal.UnknownCommand)] = ["I don't understand, please repeat", "Huh? I don't understand.", "Oh would you just kiss me already?!"],
        [(15, PetVocal.Refusal)] = ["Talk to the hoof!", "Hey, manure happens.", "I wish I could do as you ask, but I cannot."],
        [(15, PetVocal.UnknownCommand)] =
        [
            "Billy McCoy says...Gunter glieben glauchen globen", "Billy McCoy says...I'm a shadow with the win, runnin' in the dust",
            "Billy McCoy says...And the dream stands before thequeen of them all", "Billy McCoy says... hypnotised got me, cool eyes cool woman",
            "Billy McCoy says...Drop till you rock! I say Rock!"
        ],
        [(15, PetVocal.Scratched)] = ["I'm happier than a turkey after Thanksgiving.", "*Happy*"],
        [(17, PetVocal.Refusal)] = ["Sorry, but no.", "I'm sure there's something else we can do :)"],
        [(17, PetVocal.UnknownCommand)] = ["I don't understand what you're asking.", "Speak up! My ears only hear kind words :)", "Say what?"],
        [(20, PetVocal.Refusal)] = ["Oh, that's no fun!", "Normally, for you I would do anything, but right now, I'm busy day dreaming."],
        [(20, PetVocal.UnknownCommand)] = ["Huh? What do you mean?", "I have no idea what you mean, but I like the sound of it!", "Mmmhmm."],
        [(21, PetVocal.Refusal)] = ["I'm sorry, but while I'd do anything for love, I won't do that."],
        [(21, PetVocal.UnknownCommand)] =
            ["Your words confuse and confound!", "Your words fall upon my ears like raindrops on an iceberg.", "My ears are closed to your intentions."],
        [(22, PetVocal.UnknownCommand)] = ["Say what?", "I have no idea what you're saying.", "Try again.", "How dare you speak to me like that?", "Eh?"],
        [(28, PetVocal.UnknownCommand)] = ["No comprende, amigo.", "Don't you speak kitten?"],
        [(29, PetVocal.UnknownCommand)] = ["Stop hounding me, I don't understand!", "Let´s paws for a second."],
        [(30, PetVocal.UnknownCommand)] = ["Hog wash", "You're bacon me crazy!"],
        [(1, PetVocal.Eating)] = ["*eating*"],
        [(8, PetVocal.Eating)] = ["The fresher the flesh, the greater the taste!"],
        [(15, PetVocal.Eating)] = ["What a delicacy, yum, yum", "Yum, yum, this is delicious", "This is delicious!"],
        [(20, PetVocal.Eating)] = ["What a delicacy, yum, yum"],
        [(29, PetVocal.Eating)] = ["Mmm, I'm wolfing this down!"],
        [(35, PetVocal.Eating)] = ["Veganism isn't just for Christmas. Veganism is for life.", "*Crunch* I just ADORE meal times. *Crunch*", "Mmmm. Healthy. *Crunch*"],
        [(0, PetVocal.Drinking)] = ["*drinking*"],
        [(1, PetVocal.Drinking)] = ["*Splish splash*"],
        [(8, PetVocal.Drinking)] = ["Yes... Yes... This unholy water gives me strength!"],
        [(15, PetVocal.Drinking)] = ["Ah, that's the good stuff."],
        [(20, PetVocal.Drinking)] = ["This is literally the greatest drink I have ever had!"],
        [(21, PetVocal.Drinking)] = ["Yes... not too much, but not too little either."],
        [(29, PetVocal.Drinking)] = ["I love to drink! Love it, love it, love it!", "H-2-Whoa!", "Oops, did I spill some?", "*slurp slurp slurp*"],
        [(35, PetVocal.Drinking)] = ["Drinking water after an entire day eating grass is simply heavenly.", "*Gulp* ..if it wasn't for water.. *Gulp* ..life wouldn't exist on this planet.."],
        [(1, PetVocal.Playing)] = ["*playing*"],
        [(8, PetVocal.Playing)] = ["Joy is mine!"],
        [(11, PetVocal.Playing)] = ["Woohoo!", "Anyone up for a game of croak-ay?", "Fun fun fun..."],
        [(28, PetVocal.Playing)] = ["This is pawesome!"],
        [(15, PetVocal.Sleeping)] = ["Zzzz.... Marston!", "Zzzz... Old Dan Tucker....", "Zzzz... I have a heart that can never be tamed..."],
        [(21, PetVocal.Sleeping)] =
        [
            "Zzzz... shira uo ya, kuroki me o aku, nori no ami...", "Zzz... otoroi ya, ha ni kuiateshim haru no ame...",
            "Zzzz... suisen ya, shiroki shoji no, tomo utsuri..."
        ],
        [(22, PetVocal.Sleeping)] = ["Zzz..."],
        [(29, PetVocal.Sleeping)] = ["Zzz... Say goodbye, cat."],
        [(35, PetVocal.Sleeping)] = ["*Dreams of electric sheep*", "*zzZZzz..zz..zzZZzz*", "*Rolls over and has a mouthful of grass mid-sleep*"]
    };

    public static IReadOnlyList<string> For(int type, PetVocal vocal)
    {
        var own = Lines.GetValueOrDefault((type, vocal), []);

        return vocal switch
        {
            PetVocal.Refusal => [.. SharedRefusals, .. own],
            PetVocal.Playing => [.. SharedPlaying, .. own],
            _ => own
        };
    }

    /// <summary>The pet says one of its lines for the moment. A muted room hears nothing.</summary>
    public static void Say(RoomUser pet, PetVocal vocal)
    {
        if (pet.PetData is not { } data) {
            return;
        }

        var lines = For(data.Type, vocal);

        if (lines.Count > 0) {
            pet.Chat(lines[Random.Shared.Next(lines.Count)]);
        }
    }
}
