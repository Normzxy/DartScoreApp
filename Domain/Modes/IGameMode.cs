namespace Domain.Modes;
using ValueObjects;
using Entities;

public interface IGameMode
{
    /// <summary>
    /// Creates a specific initial score state according to the specific game mode.
    /// </summary>
    PlayerScore CreateInitialScore(Guid playerId);
    void ValidatePlayers(IReadOnlyCollection<Player> players);
    
    /// <summary>
    /// Evaluates the result of a throw according to the specific game mode.
    /// </summary>
    ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores);
}