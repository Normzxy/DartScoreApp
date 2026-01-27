using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

namespace Domain.Modes.FreeForAll;

public class FreeForAll(FreeForAllSettings settings) : IGameMode
{
    private readonly FreeForAllSettings _settings
        = settings ?? throw new ArgumentNullException(nameof(settings));
    public int DartsPerTurn => _settings.DartsPerTurn;

    public PlayerScore CreateInitialScore(Guid playerId)
        => new ClassicLegsScore
        {
            PlayerId = playerId,
            RemainingInLeg = _settings.ScorePerLeg,
            LegsWonInMatch = 0
        };

    public void ValidatePlayers(IReadOnlyCollection<Player> players)
    {
        if (players.Count is < 2 or > 4)
            throw new ArgumentException(
                "Free For All mode requires 2 - 4 players.");
    }

    public ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores)
    {
        ArgumentNullException.ThrowIfNull(allPlayerScores);

        var playerScore = allPlayerScores[playerId].AsClassicLegsScore("current player's entry");

        // Current player's score data.
        var currentRemaining = playerScore.RemainingInLeg;
        var currentLegsWon = playerScore.LegsWonInMatch;

        // FLags to evaluate required Game state changes.
        var legWon = false;
        var gameWon = false;

        var afterThrow = currentRemaining - throwData.Score;

        if (afterThrow != 0 && IsBust(afterThrow))
            return ThrowEvaluationResult.Bust();

        if (afterThrow is not 0)
            currentRemaining = afterThrow;
        else
        {
            if (!IsLegWon(throwData))
                return ThrowEvaluationResult.Bust();

            legWon = true;
            currentRemaining = _settings.ScorePerLeg;
            currentLegsWon++;

            gameWon = IsGameWon(currentLegsWon);
        }

        var updatedScore = playerScore with 
        {
            RemainingInLeg = currentRemaining, 
            LegsWonInMatch = currentLegsWon
        };

        // Other players score handling.
        if (!legWon)
            return ThrowEvaluationResult.Continue(updatedScore);

        // If leg won, there's need to change opponent's state.
        var othersUpdatedScore = new Dictionary<Guid, PlayerScore>();

        foreach (var (id, score) in allPlayerScores)
        {
            if (id == playerId)
                continue;

            var otherScore = allPlayerScores[id].AsClassicLegsScore("opponent update");

            var updatedOther = otherScore with
            {
                RemainingInLeg = gameWon ? otherScore.RemainingInLeg : _settings.ScorePerLeg,
                LegsWonInMatch = otherScore.LegsWonInMatch
            };

            othersUpdatedScore[id] = updatedOther;
        }

        return gameWon ? ThrowEvaluationResult.Win(updatedScore, othersUpdatedScore)
            : legWon ? ThrowEvaluationResult.Continue(updatedScore, othersUpdatedScore, ProggressInfo.LegWon)
            : ThrowEvaluationResult.Continue(updatedScore, othersUpdatedScore);
    }

    /// <summary>
    /// Checks for instant bust when remaining score is other than 0.
    /// </summary>
    private bool IsBust(int afterThrow)
    {
        if (!_settings.DoubleOutEnabled)
            return afterThrow < 0;

        return afterThrow is < 0 or 1;
    }

    /// <summary>
    /// Checks if leg is finished, when remaining value is 0.
    /// </summary>
    private bool IsLegWon(ThrowData throwData)
    {
        return !_settings.DoubleOutEnabled || throwData.Multiplier is 2;
    }

    private bool IsGameWon(int currentLegsWon)
    {
        return currentLegsWon >= _settings.LegsToWinMatch;
    }
}