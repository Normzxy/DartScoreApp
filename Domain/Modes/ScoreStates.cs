namespace Domain.Modes;

/// <summary>
/// Stores score structure according to the specific game mode.
/// </summary>
public abstract record PlayerScore
{
    public Guid PlayerId { get; init; }
}

public sealed record ClassicSetsScore : PlayerScore
{
    public int RemainingInLeg { get; init; }
    public int LegsWonInSet { get; init; }
    public int SetsWonInMatch { get; init; }
}

public sealed record ClassicLegsScore : PlayerScore
{
    public int RemainingInLeg { get; init; }
    public int LegsWonInMatch { get; init; }
}

public sealed record CricketScore : PlayerScore
{
    public int Score { get; init; }
    public int HitsOn15 { get; init; }
    public int HitsOn16 { get; init; }
    public int HitsOn17 { get; init; }
    public int HitsOn18 { get; init; }
    public int HitsOn19 { get; init; }
    public int HitsOn20 { get; init; }
    public int HitsOnBull { get; init; }
}