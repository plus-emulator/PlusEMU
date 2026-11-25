using Plus.Communication.Packets.Outgoing.Rooms.AI.Pets;
using System.Drawing;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.Utilities;
using Plus.HabboHotel.Rooms.Chat.Pets.Commands;
using Plus.HabboHotel.Rooms.Chat.Pets.Locale;

namespace Plus.HabboHotel.Rooms.AI.Types;

public class PetBot : BotAi
{
    private int _actionTimer;
    // Rest refills a pet's maximum energy in one hour, one point at a time.
    private static readonly TimeSpan RefillTime = TimeSpan.FromHours(1);
    private TimeSpan _rest;
    private uint _visitId;
    private Point _targetPosition;
    private Point _visitSpot;
    private int _speechTimer;
    private readonly IPetLocale _locale;
    private readonly IPetCommandManager _commands;
    private readonly Random _random;

    public PetBot(int virtualId, IPetLocale locale, IPetCommandManager commands, Random? random = null)
    {
        _locale = locale;
        _commands = commands;
        _random = random ?? Random.Shared;
        _speechTimer = Random.Shared.Next(10, 60);
        _actionTimer = Random.Shared.Next(10, 30 + virtualId);
    }

    private void RemovePetStatus()
    {
        var botUser = GetRoomUser();

        if (botUser == null) {
            return;
        }

        var pet = botUser;

        if (pet != null) {
            foreach (var kvp in pet.Statusses.ToList()) {
                if (pet.RidingHorse && kvp.Key is "mv" or "jmp") {
                    continue;
                }

                if (pet.Statusses.ContainsKey(kvp.Key)) {
                    pet.Statusses.Remove(kvp.Key);
                }
            }
        }
    }

    public override void OnSelfEnterRoom()
    {
        var botRoom = GetRoom();

        if (botRoom == null) {
            return;
        }

        var botUser = GetRoomUser();

        if (botUser == null) {
            return;
        }

        if (botUser != null && botRoom.GetGameMap().TryGetRandomWalkableSquare(out var nextCoord)) {
            botUser.MoveTo(nextCoord.X, nextCoord.Y);
        }
    }

    public override void OnSelfLeaveRoom(bool kicked) { }


    public override void OnUserEnterRoom(RoomUser user)
    {
        var botUser = GetRoomUser();

        if (botUser == null) {
            return;
        }

        var speakerSession = user?.GetClient();

        if (speakerSession == null) {
            return;
        }

        if (speakerSession != null && speakerSession.GetHabbo() != null) {
            var pet = botUser;

            if (pet?.PetData is { } petData) {
                if (speakerSession.GetHabbo().Username == petData.OwnerName) {
                    Greet();
                }
            }
        }
    }

    /// <summary>Says one welcome line, as a pet does when its owner places it.</summary>
    public void Greet()
    {
        if (GetRoomUser() is { PetData: { } petData } pet) {
            var speech = _locale.GetValue($"welcome.speech.pet{petData.Type}");
            pet.Chat(speech[Random.Shared.Next(0, speech.Length)].Replace("%owner%", petData.OwnerName, StringComparison.Ordinal));
        }
    }

    public override void OnUserLeaveRoom(GameClient client) { }

    public override void OnUserShout(RoomUser user, string message) { }

    public override void OnTimerTick()
    {
        var botRoom = GetRoom();

        if (botRoom == null) {
            return;
        }

        var botUser = GetRoomUser();

        if (botUser == null) {
            return;
        }

        var pet = botUser;

        if (pet?.PetData is not { } petData) {
            return;
        }

        if (_visitId != 0 && Visit(botRoom, pet, petData)) {
            return;
        }

        if (PetChatter.For(petData.Type) is { } chatter && _random.NextDouble() < chatter.ChancePerTick) {
            pet.Chat(chatter.Lines[_random.Next(chatter.Lines.Length)]);
        }

        if (_speechTimer <= 0) {
            if (petData.DbState != PetDatabaseUpdateState.NeedsInsert) {
                petData.DbState = PetDatabaseUpdateState.NeedsUpdate;
            }

            if (pet != null) {
                RemovePetStatus();
                // The three letter entries of the speech list are gestures.
                var gestures = _locale.GetValue($"speech.pet{petData.Type}").Where(entry => entry.Length == 3).ToArray();

                if (gestures.Length > 0) {
                    pet.Statusses.Add(gestures[Random.Shared.Next(0, gestures.Length)], TextHandling.GetString(pet.Z));
                }
            }

            _speechTimer = Random.Shared.Next(20, 120 + 1);
        }
        else {
            _speechTimer--;
        }

        if (_actionTimer <= 0) {
            try {
                RemovePetStatus();
                _actionTimer = Random.Shared.Next(15, 40 + petData.VirtualId + 1);

                if (!botUser.RidingHorse) {
                    // Remove Status
                    RemovePetStatus();

                    if (petData.Energy < petData.MaxEnergy && FindFood(botRoom, botUser, petData) is { } food) {
                        GoTo(botRoom, botUser, food);
                    }
                    else if (Random.Shared.Next(DrinkChance) == 0 && FindWater(botRoom, botUser) is { } water) {
                        GoTo(botRoom, botUser, water);
                    }
                    else if (Random.Shared.Next(ToyChance) == 0 && FindToy(botRoom, botUser, petData.Type) is { } toy) {
                        GoTo(botRoom, botUser, toy);
                    }
                    else if (Random.Shared.Next(NestChance) == 0 && FindNearest(botRoom, botUser, item => PetNest.Is(item.Definition) && item.Definition.Walkable) is { } nest) {
                        GoTo(botRoom, botUser, nest);
                    }
                    else if (botUser.CanWalk && botRoom.GetGameMap().TryGetRandomWalkableSquare(out var nextCoord)) {
                        botUser.MoveTo(nextCoord.X, nextCoord.Y);
                    }
                }
            }
            catch (Exception e) {
                ExceptionLogger.LogException(e);
            }
        }
        else {
            _actionTimer--;
        }

        _rest += RoomCycle.Interval * petData.MaxEnergy;

        if (_rest >= RefillTime) {
            _rest -= RefillTime;
            petData.PetEnergy(true);
        }
    }

    public override void OnUserSay(RoomUser user, string message)
    {
        if (GetRoom() is not { } botRoom || GetRoomUser() is not { PetData: { } petData } pet || user?.GetClient()?.GetHabbo() is not { } speaker) {
            return;
        }

        if (message.Equals(petData.Name, StringComparison.OrdinalIgnoreCase)) {
            pet.SetRot(Rotation.Calculate(pet.X, pet.Y, user.X, user.Y), false);

            return;
        }

        if (speaker.Id != petData.OwnerId || !message.StartsWith(petData.Name + " ", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        var commandId = _commands.TryInvoke(message[(petData.Name.Length + 1)..]);
        var commands = PetCommandSet.For(petData.Type);

        if (commandId < 0 || !commands.Has(commandId)) {
            return;
        }

        if (petData.Type == HorseProducts.HorseType) {
            var navigation = botRoom.GetGameMap().Navigation;
            navigation.RunOwner(pet, (_, _) =>
            {
                if (!ReferenceEquals(botRoom.GetRoomUserManager().GetRoomUserByVirtualId(user.VirtualId), user)) {
                    return;
                }

                if (pet.RidingHorse) {
                    var rider = botRoom.GetRoomUserManager().GetRoomUserByVirtualId(pet.HorseId);

                    if (commandId == PetCommandSet.Free || rider == null || !rider.RidingHorse || rider.HorseId != pet.VirtualId) {
                        return;
                    }

                    navigation.Mounts.Ride(rider, pet, false, _locale);

                    if (pet.RidingHorse) {
                        return;
                    }
                }

                Respond(botRoom, pet, petData, user, commandId);
            });

            return;
        }

        Respond(botRoom, pet, petData, user, commandId);
    }

    private void Respond(Room botRoom, RoomUser pet, Pet petData, RoomUser user, int commandId)
    {
        var commands = PetCommandSet.For(petData.Type);
        RemovePetStatus();
        pet.UpdateNeeded = true;

        if (!commands.HasLearned(commandId, petData.Level)) {
            pet.Statusses["gst"] = "puz";
            PetVocals.Say(pet, PetVocal.UnknownCommand);

            return;
        }

        if (petData.IsTired) {
            pet.Statusses["lay"] = TextHandling.GetString(pet.Z);
            _speechTimer = 50;
            _actionTimer = 45;

            return;
        }

        if (commandId == PetCommandSet.Breed) {
            GoToBreedingBox(botRoom, pet, petData.Type, user.GetClient()!);

            return;
        }

        var food = commandId == PetCommandSet.Eat ? FindFood(botRoom, pet, petData) : null;
        var water = commandId == PetCommandSet.Drink ? FindWater(botRoom, pet) : null;

        // Without a bowl or food in the room a pet refuses to drink or eat.
        if (commandId == PetCommandSet.Drink && water == null || commandId == PetCommandSet.Eat && food == null || !petData.ObeysCommand(commandId)) {
            petData.SpendEnergy(2);
            PetVocals.Say(pet, PetVocal.Refusal);

            return;
        }

        if (food != null) {
            GoTo(botRoom, pet, food);
            petData.Addexperience(0);

            return;
        }

        Obey(botRoom, pet, user, commandId);

        if (water != null) {
            GoTo(botRoom, pet, water);
        }

        if (commandId != PetCommandSet.Free) {
            petData.Addexperience(PetCommandSet.Experience(petData.Type, commandId));
            petData.SpendEnergy(5);
        }
    }

    // The pet walks to the nearest box for its type it has a route to; otherwise Habbo tells the owner why not.
    private void GoToBreedingBox(Room room, RoomUser pet, int petType, GameClient owner)
    {
        var navigation = room.GetGameMap().Navigation;
        navigation.RunOwner(pet, (actor, _) =>
        {
            var boxes = room.GetRoomItemHandler().GetFloor.Where(item => item.Definition.BreedingPetType != null).ToList();
            var suitable = boxes.Where(item => item.Definition.BreedingPetType == petType).ToList();
            var reachable = suitable
                .OrderBy(item => Math.Max(Math.Abs(item.GetX - actor.X), Math.Abs(item.GetY - actor.Y)))
                .FirstOrDefault(item => navigation.HasRoute(actor, item.GetX, item.GetY));

            if (reachable != null) {
                _actionTimer = 30;
                actor.MoveTo(reachable.GetX, reachable.GetY);

                return;
            }

            owner.Send(new GoToBreedingNestFailureComposer(boxes.Count == 0 ? BreedingNestFailure.NoNest
                : suitable.Count == 0 ? BreedingNestFailure.NoSuitableNest : BreedingNestFailure.NoPath));
        });
    }

    private static Item? FindFood(Room room, RoomUser pet, Pet petData) => room.GetRoomItemHandler()
        .FindNearestFloor(pet.Coordinate, item => !item.IsTemporary && PetFood.Of(item.Definition)?.Feeds(petData.Type) == true);

    private static Item? FindWater(Room room, RoomUser pet) =>
        FindNearest(room, pet, item => PetWater.Suits(item.Definition, pet.PetData?.Type ?? -1));

    private static Item? FindToy(Room room, RoomUser pet, int petType) =>
        FindNearest(room, pet, item => PetToy.Suits(item.Definition, petType) && item.Definition.Walkable);

    private static Item? FindNearest(Room room, RoomUser pet, Func<Item, bool> wanted) => room.GetRoomItemHandler().GetFloor
        .Where(wanted)
        .MinBy(item => Math.Abs(item.GetX - pet.X) + Math.Abs(item.GetY - pet.Y));

    // A pet stands on food and bowls; it stands next to furni it cannot walk on, such as the trough.
    private void GoTo(Room room, RoomUser pet, Item item)
    {
        var map = room.GetGameMap();
        var spot = item.Definition.Walkable
            ? item.Coordinate
            : Around(item.Coordinate).Where(tile => map.CanWalk(tile.X, tile.Y, false))
                .OrderBy(tile => Math.Abs(tile.X - pet.X) + Math.Abs(tile.Y - pet.Y)).FirstOrDefault(item.SquareInFront);
        _targetPosition = item.Coordinate;
        _visitId = item.Id;
        _visitSpot = spot;
        pet.MoveTo(spot);
    }

    private static IEnumerable<Point> Around(Point tile)
    {
        for (var dx = -1; dx <= 1; dx++) {
            for (var dy = -1; dy <= 1; dy++) {
                if (dx != 0 || dy != 0) {
                    yield return new(tile.X + dx, tile.Y + dy);
                }
            }
        }
    }

    // Returns false when the pet is not at the furni it went to, so it goes on with its day.
    private bool Visit(Room room, RoomUser pet, Pet petData)
    {
        var item = room.GetRoomItemHandler().GetItem(_visitId);

        if (item == null) {
            _visitId = 0;

            return false;
        }

        if (pet.IsWalking) {
            return true;
        }

        _visitId = 0;

        if (pet.Coordinate != _visitSpot || item.Coordinate != _targetPosition) {
            return false;
        }

        if (PetWater.Suits(item.Definition, petData.Type)) {
            Drink(item, pet);

            return true;
        }

        if (PetNest.Is(item.Definition)) {
            Sleep(pet);

            return true;
        }

        if (PetToy.Suits(item.Definition, petData.Type)) {
            Play(item, pet, petData);

            return true;
        }

        return PetFood.Of(item.Definition) is { } values && values.Feeds(petData.Type) && Eat(room, item, values, pet, petData);
    }

    private void Drink(Item water, RoomUser pet)
    {
        RemovePetStatus();
        pet.Statusses["eat"] = TextHandling.GetString(pet.Z);
        pet.UpdateNeeded = true;
        _actionTimer = 10;
        PetVocals.Say(pet, PetVocal.Drinking);

        if (PetWater.Is(water.Definition)) {
            PetWater.Drink(water, DateTimeOffset.UtcNow);
            water.UpdateState(true, true);
        }
    }

    // A horse kicks the ball, a frog dips in the pond and other pets play. The toy moves for three and a half seconds.
    private void Play(Item toy, RoomUser pet, Pet petData)
    {
        RemovePetStatus();
        pet.Statusses[PetToy.IsPond(toy.Definition) ? "dip" : petData.Type == 15 ? "kck" : "pla"] = TextHandling.GetString(pet.Z);
        pet.Statusses["gst"] = "sml";
        pet.UpdateNeeded = true;
        _actionTimer = 10;
        PetVocals.Say(pet, PetVocal.Playing);
        toy.GetRoom()!.GetRoomItemHandler().PlayPetBall(toy);
    }

    // A pet sleeps in a nest for about a minute.
    private void Sleep(RoomUser pet)
    {
        RemovePetStatus();
        pet.Statusses["lay"] = TextHandling.GetString(pet.Z);
        pet.UpdateNeeded = true;
        _actionTimer = Random.Shared.Next(80, 190 + 1);
        _speechTimer = _actionTimer;
        PetVocals.Say(pet, PetVocal.Sleeping);
    }

    // On the food the pet takes one bite, and more while it is below its maximum energy.
    private bool Eat(Room room, Item food, PetFood values, RoomUser pet, Pet petData)
    {
        var bites = int.TryParse(food.LegacyDataString, out var taken) ? Math.Clamp(taken, 0, PetFood.Bites - 1) : 0;
        var eaten = 0;

        do {
            bites++;
            eaten++;
        } while (bites < PetFood.Bites && petData.Energy + eaten * values.BiteEnergy < petData.MaxEnergy);

        if (bites == PetFood.Bites && !room.GetRoomItemHandler().DeleteFurniture(food)) {
            return false;
        }

        petData.Feed(eaten * values.BiteEnergy);
        RemovePetStatus();
        pet.Statusses["eat"] = TextHandling.GetString(pet.Z);
        pet.UpdateNeeded = true;
        _actionTimer = 10;
        PetVocals.Say(pet, PetVocal.Eating);

        if (bites < PetFood.Bites) {
            if (food.ExtraData is not LegacyDataFormat) {
                food.ExtraData = new LegacyDataFormat();
            }

            food.LegacyDataString = bites.ToString();
            food.UpdateUsedState();
        }

        petData.Addexperience(eaten * values.BiteExperience);

        return true;
    }

    private void Obey(Room room, RoomUser pet, RoomUser user, int commandId)
    {
        switch (commandId) {
            case PetCommandSet.Free:
                if (room.GetGameMap().TryGetRandomWalkableSquare(out var free)) {
                    pet.MoveTo(free.X, free.Y);
                }

                break;
            case 3:
                _actionTimer = 30;
                var front = user.SquareInFront;
                pet.MoveTo(front.X, front.Y);
                break;
            default:
                if (Postures.TryGetValue(commandId, out var posture)) {
                    pet.Statusses[posture] = TextHandling.GetString(pet.Z);
                    _actionTimer = 30;
                }

                break;
        }
    }

    // About one action in six a pet goes to drink when there is water in the room.
    private const int DrinkChance = 6;

    // About one action in six a pet goes to play with a toy.
    private const int ToyChance = 6;

    // About one action in twenty a pet goes to sleep in a nest.
    private const int NestChance = 20;

    private static readonly Dictionary<int, string> Postures = new()
    {
        [1] = "sit",
        [2] = "lay",
        [5] = "ded",
        [9] = "jmp",
        [44] = "wag"
    };
}
