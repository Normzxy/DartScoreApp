using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Modes.FreeForAllMode;

public class FreeForAllMode(FreeForAllModeSettings modeSettings) : IGameMode
{
    private readonly FreeForAllModeSettings _modeSettings
        = modeSettings ?? throw new ArgumentNullException(nameof(modeSettings));

    public PlayerScore CreateInitialScore(Guid playerId)
        => new ClassicLegsScore
        {
            PlayerId = playerId,
            RemainingInLeg = _modeSettings.ScorePerLeg,
            LegsWonInMatch = 0
        };

    public void ValidatePlayers(IReadOnlyCollection<Player> players)
    {
        if (players.Count is < 2 or > 4)
        {
            throw new ArgumentException(
                "Free For All mode requires 2 - 4 players.");
        }
    }

    public ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores)
    {
        ArgumentNullException.ThrowIfNull(allPlayerScores);

        var playerEntry = allPlayerScores.Single(kv => kv.Key == playerId);

        if (playerEntry.Value is not ClassicLegsScore playerScore)
        {
            throw new InvalidOperationException("Unexpected current player's score data.");
        }
        
        // Current player's score data.
        var currentRemaining = playerScore.RemainingInLeg;
        var currentLegsWon = playerScore.LegsWonInMatch;
        
        // FLags to evaluate required Game state changes.
        var legWon = false;
        var gameWon = false;
        
        var afterThrow = currentRemaining - throwData.Score;
        
        if (afterThrow != 0 && IsBust(afterThrow))
        {
            return ThrowEvaluationResult.Bust();
        }

        if (afterThrow is not 0)
        {
            currentRemaining = afterThrow;
        }
        else
        {
            if (!IsLegWon(throwData))
            {
                return ThrowEvaluationResult.Bust();
            }

            legWon = true;
            currentRemaining = _modeSettings.ScorePerLeg;
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
        {
            return ThrowEvaluationResult.Continue(updatedScore);
        }

        // If leg won, there's need to change opponent's state.
        var othersUpdatedScore = new Dictionary<Guid, PlayerScore>();

        foreach (var (id, score) in allPlayerScores)
        {
            if (id == playerId)
            {
                continue;
            }

            if (score is not ClassicLegsScore otherScore)
            {
                throw new InvalidOperationException("Unexpected player's score data.");
            }

            var updatedOther = otherScore with
            {
                RemainingInLeg = gameWon ? otherScore.RemainingInLeg : _modeSettings.ScorePerLeg,
                LegsWonInMatch = otherScore.LegsWonInMatch
            };

            othersUpdatedScore[id] = updatedOther;
        }

        if (othersUpdatedScore.Count == 0)
        {
            othersUpdatedScore = null;
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
        if (!_modeSettings.DoubleOutEnabled)
        {
            return afterThrow < 0;
        }

        return afterThrow is < 0 or 1;
    }

    /// <summary>
    /// Checks if leg is finished, when remaining value is 0.
    /// </summary>
    private bool IsLegWon(ThrowData throwData)
    {
        return !_modeSettings.DoubleOutEnabled || throwData.Multiplier is 2;
    }

    private bool IsGameWon(int currentLegsWon)
    {
        return currentLegsWon >= _modeSettings.LegsToWinMatch;
    }
}