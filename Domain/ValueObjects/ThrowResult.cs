namespace Domain.ValueObjects;
using Modes;

public enum ThrowOutcome
{
    Bust,
    Continue,
    Win
}

/// <summary>
/// Returns information about the result of the throw, based on the current game state.
/// </summary>
public sealed record ThrowEvaluationResult
{
    public ThrowOutcome Outcome { get; private init; }
    public PlayerScore UpdatedScore { get; private init; } = null!;
    // Optional, because those are edited only in specific situations.
    public IReadOnlyDictionary<Guid, PlayerScore>? OtherUpdatedStates { get; init; }

    // Game uses internal snapshot to restore player's score,
    // so there's no need to return UpdatedScore.
    public static ThrowEvaluationResult Bust()
        => new()
    { 
        Outcome = ThrowOutcome.Bust,
    };

    public static ThrowEvaluationResult Continue(
        PlayerScore updatedScore,
        IReadOnlyDictionary<Guid, PlayerScore>? othersScore = null) => new()
    {
        Outcome = ThrowOutcome.Continue, 
        UpdatedScore = updatedScore, 
        OtherUpdatedStates = othersScore
    };

    public static ThrowEvaluationResult Win(
        PlayerScore finalState) => new()
    { 
        Outcome = ThrowOutcome.Win,
        UpdatedScore = finalState
    };
}