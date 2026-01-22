using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Modes.ClassicLegs;

public class ClassicLegs(ClassicLegsSettings settings) : IGameMode
{
    private readonly ClassicLegsSettings _settings
        = settings ?? throw new ArgumentNullException(nameof(settings));

    public PlayerScore CreateInitialScore(Guid playerId)
        => new ClassicLegsScore
        {
            PlayerId = playerId,
            RemainingInLeg = _settings.ScorePerLeg,
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

        var playerScore = allPlayerScores[playerId].AsClassicLegsScore("current player's entry");
        var opponentId = allPlayerScores.Single(kv => kv.Key != playerId).Key;
        var opponentScore = allPlayerScores[opponentId].AsClassicLegsScore("opponent's entry");

        // Opponent's score data.
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
            currentRemaining = _settings.ScorePerLeg;
            currentLegsWon++;

            gameWon = _settings.AdvantagesEnabled ? IsGameWonAdvantage(currentLegsWon, opponentLegsWon)
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
            RemainingInLeg = gameWon ? opponentRemaining : _settings.ScorePerLeg,
            LegsWonInMatch = opponentLegsWon
        };

        opponentUpdatedScore[opponentId] = updatedOpponent;

        return gameWon ? ThrowEvaluationResult.Win(updatedScore, opponentUpdatedScore)
            : legWon ? ThrowEvaluationResult.Continue(updatedScore, opponentUpdatedScore, ProggressInfo.LegWon)
            : ThrowEvaluationResult.Continue(updatedScore, opponentUpdatedScore);
    }

    /// <summary>
    /// Checks for instant bust when remaining score is other than 0.
    /// </summary>
    private bool IsBust(int afterThrow)
    {
        if (!_settings.DoubleOutEnabled)
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
        return !_settings.DoubleOutEnabled || throwData.Multiplier is 2;
    }
    
    private bool IsGameWon(int currentLegsWon)
    {
        return currentLegsWon >= _settings.LegsToWinMatch;
    }

    /// <summary>
    /// Game must be won by two legs of advantage until the limit of legs is reached (sudden dath).
    /// </summary>
    private bool IsGameWonAdvantage(int currentLegsWon, int opponentLegsWon)
    {
        return (currentLegsWon >= _settings.LegsToWinMatch && currentLegsWon >= opponentLegsWon + 2)
               || currentLegsWon >= _settings.SuddenDeathWinningLeg;
    }
}