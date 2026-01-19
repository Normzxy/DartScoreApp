namespace Domain.ValueObjects;
using Modes;

public enum ThrowOutcome
{
    Bust,
    Continue,
    Win
}

public enum ProggressInfo
{
    None,
    LegWon,
    SetWon
}

/// <summary>
/// Returns information about the result of the throw, based on the current game state.
/// </summary>
public sealed record ThrowEvaluationResult
{
    public ThrowOutcome Outcome { get; private init; }
    public ProggressInfo Proggress { get; init; } = ProggressInfo.None;
    public PlayerScore? UpdatedScore { get; private init; }
    // Optional, because those are edited only in specific situations.
    public IReadOnlyDictionary<Guid, PlayerScore>? OtherUpdatedScores { get; private init; }

    // Game uses internal snapshot to restore player's score,
    // so there's no need to return UpdatedScore.
    public static ThrowEvaluationResult Bust()
        => new()
    {
        Outcome = ThrowOutcome.Bust,
    };

    public static ThrowEvaluationResult Continue(
        PlayerScore updatedScore,
        IReadOnlyDictionary<Guid, PlayerScore>? othersScore = null,
        ProggressInfo proggress = default)
        => new()
    {
        Outcome = ThrowOutcome.Continue,
        UpdatedScore = updatedScore,
        OtherUpdatedScores = othersScore,
        Proggress = proggress
    };

    public static ThrowEvaluationResult Win(
        PlayerScore finalState,
        IReadOnlyDictionary<Guid, PlayerScore>? othersScore = null)
        => new()
    { 
        Outcome = ThrowOutcome.Win,
        UpdatedScore = finalState,
        OtherUpdatedScores = othersScore
    };
}