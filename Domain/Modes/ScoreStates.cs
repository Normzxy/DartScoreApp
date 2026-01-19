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