using System.Collections.Immutable;

namespace Plus.HabboHotel.Users.Collectibles;

/// <summary>A product a collectible can be.</summary>
public sealed record CollectibleProduct(string ProductCode, short ProductType, string ItemTypeId, int Score, string Rarity, string PetFigure,
    ImmutableArray<int> FigureSetIds);

public enum SetPrize
{
    Bonus,
    Reward
}

/// <summary>An enabled set of the Collections tab, with its products in display order.</summary>
public sealed record CollectibleSet(string Id, string Name, int BoostScore, ImmutableArray<CollectibleProduct> Products, CollectibleProduct? Bonus,
    CollectibleProduct? Reward, DateTimeOffset? ReleasedAt, DateTimeOffset? SnapshotAt)
{
    public int TotalScore => Products.Sum(product => product.Score);

    public CollectibleProduct? Prize(SetPrize prize) => prize == SetPrize.Bonus ? Bonus : Reward;

    public string ClaimId(SetPrize prize) => Id + (prize == SetPrize.Bonus ? ".bonus" : ".reward");
}

public sealed record CollectorLevel(int Level, int MinScore);

/// <summary>
/// What one wallet owns and the collector rules built on it: score, level, completed sets and which set prizes it may claim.
/// </summary>
public sealed class CollectorBook(ImmutableArray<CollectibleSet> sets, IReadOnlyDictionary<string, CollectibleProduct> products,
    ImmutableArray<CollectorLevel> levels, IReadOnlyDictionary<string, int> owned, IReadOnlySet<(string SetId, SetPrize Prize)> claimed)
{
    public ImmutableArray<CollectibleSet> Sets => sets;

    /// <summary>Each product the wallet owns counts once, and every completed set adds its boost.</summary>
    public int Score => owned.Where(pair => pair.Value > 0).Sum(pair => products[pair.Key].Score) + sets.Where(IsComplete).Sum(set => set.BoostScore);

    public int Level
    {
        get
        {
            var score = Score;

            return levels.Where(level => level.MinScore <= score).Select(level => level.Level).DefaultIfEmpty(0).Max();
        }
    }

    public int Owned(CollectibleProduct product) => owned.GetValueOrDefault(product.ProductCode);

    public bool IsComplete(CollectibleSet set) => set.Products.Length > 0 && set.Products.All(product => Owned(product) > 0);

    public int SetScore(CollectibleSet set) => set.Products.Where(product => Owned(product) > 0).Sum(product => product.Score);

    public bool HasClaimed(CollectibleSet set, SetPrize prize) => claimed.Contains((set.Id, prize));

    /// <summary>The bonus is only open between the set's release and its snapshot; a missing time leaves that side open.</summary>
    public bool CanClaim(CollectibleSet set, SetPrize prize, DateTimeOffset now) =>
        set.Prize(prize) != null && IsComplete(set) && !HasClaimed(set, prize)
        && (prize == SetPrize.Reward || (set.ReleasedAt is not { } released || released <= now) && (set.SnapshotAt is not { } snapshot || now < snapshot));
}
