using Domain.Entities;
using Domain.Modes;
using Domain.ValueObjects;

namespace Domain.Interfaces;

public interface IGameMode
{
    int DartsPerTurn { get; }

    void ValidatePlayers(IReadOnlyCollection<Player> players);

    /// <summary>
    /// Creates a specific initial score state according to the specific game mode.
    /// </summary>
    PlayerScore CreateInitialScore(Guid playerId);

    /// <summary>
    /// Evaluates the result of a throw according to the specific game mode.
    /// </summary>
    ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores);
}