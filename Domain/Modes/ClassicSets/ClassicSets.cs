using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

namespace Domain.Modes.ClassicSets;

public class ClassicSets(ClassicSetsSettings settings) : IGameMode
{
    private readonly ClassicSetsSettings _settings
        = settings ?? throw new ArgumentNullException(nameof(settings));
    public int DartsPerTurn => _settings.DartsPerTurn;

    public PlayerScore CreateInitialScore(Guid playerId)
        => new ClassicSetsScore 
        {
            PlayerId = playerId,
            RemainingInLeg = _settings.ScorePerLeg,
            LegsWonInSet = 0,
            SetsWonInMatch = 0
        };

    public void ValidatePlayers(IReadOnlyCollection<Player> players)
    {
        if (players.Count != 2)
            throw new InvalidOperationException(
                "Classic Sets mode requires exactly two players.");
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

        var playerScore = allPlayerScores[playerId].AsClassicSetsScore("current player's entry");
        var opponentId = allPlayerScores.Single(kv => kv.Key != playerId).Key;
        var opponentScore = allPlayerScores[opponentId].AsClassicSetsScore("opponent's entry");

        // Opponent's score data.
        var opponentRemaining = opponentScore.RemainingInLeg;
        var opponentLegsWon = opponentScore.LegsWonInSet;
        var opponentSetsWon = opponentScore.SetsWonInMatch;

        // Current player's score data.
        var currentRemaining = playerScore.RemainingInLeg;
        var currentLegsWon = playerScore.LegsWonInSet;
        var currentSetsWon = playerScore.SetsWonInMatch;

        // FLags to evaluate required Game state changes.
        var legWon = false;
        var setWon = false;
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

            // Special case of sudden death.
            if (IsDecider(currentSetsWon, opponentSetsWon))
            {
                if (IsDeciderWon(currentLegsWon, opponentLegsWon))
                {
                    setWon = true;
                    currentLegsWon = 0;
                    currentSetsWon++;
                    gameWon = true;
                }
            }
            // Normal course of the game.
            else if (IsSetWon(currentLegsWon))
            {
                    setWon = true;
                    currentLegsWon = 0;
                    currentSetsWon++;

                    if (IsGameWon(currentSetsWon))
                        gameWon = true;
            }
        }

        // New state of a current player.
        var updatedScore = playerScore with 
        {
            RemainingInLeg = currentRemaining, 
            LegsWonInSet = currentLegsWon, 
            SetsWonInMatch = currentSetsWon
        };

        // Other players score handling.
        if (!legWon)
            return ThrowEvaluationResult.Continue(updatedScore);

        // If leg won, there's need to change opponent's state.
        // It's a dictionary, because Game agregate requires a Dictionary in general.
        var opponentUpdatedScore = new Dictionary<Guid, PlayerScore>();

        var updatedOpponent = opponentScore with
        {
            RemainingInLeg = gameWon ? opponentRemaining : _settings.ScorePerLeg,
            LegsWonInSet = gameWon ? opponentLegsWon : setWon ? 0 : opponentLegsWon,
            SetsWonInMatch = opponentSetsWon
        };

        opponentUpdatedScore[opponentId] = updatedOpponent;

        return gameWon ? ThrowEvaluationResult.Win(updatedScore, opponentUpdatedScore)
            : setWon ? ThrowEvaluationResult.Continue(updatedScore, opponentUpdatedScore, ProggressInfo.SetWon)
            : legWon ? ThrowEvaluationResult.Continue(updatedScore, opponentUpdatedScore, ProggressInfo.LegWon)
            : ThrowEvaluationResult.Continue(updatedScore, opponentUpdatedScore);
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

    /// <summary>
    /// Checks for decider (both player are one set from winning a game).
    /// In decider player must win by 2 legs or reach specified number of legs (sudden death).
    /// </summary>
    private bool IsDecider(int currentSetsWon, int opponentSetsWon)
    {
        return _settings.AdvantagesEnabled
               && currentSetsWon == _settings.SetsToWinMatch - 1
               && opponentSetsWon == _settings.SetsToWinMatch - 1;
    }

    private bool IsDeciderWon(int currentLegsWon, int opponentLegsWon)
    {
        return (currentLegsWon >= _settings.LegsToWinSet && currentLegsWon >= opponentLegsWon + 2)
               || currentLegsWon >= _settings.SuddenDeathWinningLeg;
    }

    private bool IsSetWon(int currentLegsWon)
    {
        return currentLegsWon >= _settings.LegsToWinSet;
    }

    private bool IsGameWon(int currentSetsWon)
    {
        return currentSetsWon >= _settings.SetsToWinMatch;
    }
}