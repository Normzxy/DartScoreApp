using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Modes.ClassicLegsMode;

public class ClassicLegsMode(ClassicLegsModeSettings modeSettings) : IGameMode
{
    private readonly ClassicLegsModeSettings _modeSettings
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
        if (players.Count != 2)
        {
            throw new InvalidOperationException(
                "Classic Legs mode requires exactly two players.");
        }
    }

    /// <summary>
    /// Takes snapshots of current player's score and opponent's score.
    /// Eavluates and returns the outcome of every throw.
    /// </summary>
    public ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores)
    {
        ArgumentNullException.ThrowIfNull(allPlayerScores);

        var playerEntry = allPlayerScores.Single(kv => kv.Key == playerId);
        var opponentEntry = allPlayerScores.Single(kv => kv.Key != playerId);

        if (opponentEntry.Value is not ClassicLegsScore opponentScore)
        {
            throw new InvalidOperationException("Unexpected opponent's score data.");
        }

        if (playerEntry.Value is not ClassicLegsScore playerScore)
        {
            throw new InvalidOperationException("Unexpected current player's score data.");
        }

        // Opponent's score data.
        var opponentId = opponentScore.PlayerId;
        var opponentRemaining = opponentScore.RemainingInLeg;
        var opponentLegsWon = opponentScore.LegsWonInMatch;

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

            gameWon = _modeSettings.AdvantagesEnabled ? IsGameWonAdvantage(currentLegsWon, opponentLegsWon)
                : IsGameWon(currentLegsWon);
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
        // It's a dictionary, because Game agregate requires a Dictionary in general.
        var opponentUpdatedScore = new Dictionary<Guid, PlayerScore>();

        var updatedOpponent = opponentScore with
        {
            RemainingInLeg = gameWon ? opponentRemaining : _modeSettings.ScorePerLeg,
            LegsWonInMatch = opponentLegsWon
        };

        opponentUpdatedScore[opponentId] = updatedOpponent;

        if (opponentUpdatedScore.Count == 0)
        {
            opponentUpdatedScore = null;
        }

        return gameWon ? ThrowEvaluationResult.Win(updatedScore, opponentUpdatedScore)
            : legWon ? ThrowEvaluationResult.Continue(updatedScore, opponentUpdatedScore, ProggressInfo.LegWon)
            : ThrowEvaluationResult.Continue(updatedScore, opponentUpdatedScore);
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

    /// <summary>
    /// Game must be won by two legs of advantage until the limit of legs is reached (sudden dath).
    /// </summary>
    private bool IsGameWonAdvantage(int currentLegsWon, int opponentLegsWon)
    {
        return (currentLegsWon >= _modeSettings.LegsToWinMatch && currentLegsWon >= opponentLegsWon + 2)
               || currentLegsWon >= _modeSettings.SuddenDeathWinningLeg;
    }
}