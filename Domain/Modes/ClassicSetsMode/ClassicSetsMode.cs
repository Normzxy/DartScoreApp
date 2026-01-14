using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Modes.ClassicSetsMode;

public class ClassicSetsMode(ClassicSetsSettings settings) : IGameMode
{
    private readonly ClassicSetsSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public PlayerScore CreateInitialScore(Guid playerId) => 
        new ClassicSetsPlayerScore
        {
            PlayerId = playerId,
            RemainingInLeg = _settings.StartingScorePerLeg,
            LegsWonInSet = 0,
            SetsWonInMatch = 0
        };

    public void ValidatePlayers(IReadOnlyCollection<Player> players)
    {
        if (players.Count != 2)
            throw new InvalidOperationException(
                "Classic sets mode requires exactly two players.");
    }

    /// <summary>
    /// Takes snapshots of current score and all player scores.
    /// Eavluates and returns the outcome of every throw.
    /// </summary>
    public ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores)
    {
        ArgumentNullException.ThrowIfNull(allPlayerScores);

        // Throws InvalidOperationException if more than one oponent is present.
        var playerEntry = allPlayerScores.Single(kv => kv.Key == playerId);
        var opponentEntry = allPlayerScores.Single(kv => kv.Key != playerId);

        if (!allPlayerScores.ContainsKey(playerId))
        {
            throw new InvalidOperationException("Player state not provided in allPlayerScores.");
        }

        if (opponentEntry.Value is not ClassicSetsPlayerScore opponentScore)
        {
            throw new InvalidOperationException("No score data for opponent player.");
        }
        
        if (playerEntry.Value is not ClassicSetsPlayerScore playerScore)
        {
            throw new InvalidOperationException("No score data for opponent player.");
        }

        // Data needed to apply "sudden death" mode when needed.
        var opponentId = opponentScore.PlayerId;
        var opponentRemaining = opponentScore.RemainingInLeg;
        var opponentLegsWon = opponentScore.LegsWonInSet;
        var opponentSetsWon = opponentScore.SetsWonInMatch;

        // Current player data.
        var currentRemaining = playerScore.RemainingInLeg;
        var currentLegsWon = playerScore.LegsWonInSet;
        var currentSetsWon = playerScore.SetsWonInMatch;

        // FLags to evaluate required Game state changes.
        var legWon = false;
        var setWon = false;
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
            currentRemaining = _settings.StartingScorePerLeg;
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
            else
            {
                if (IsSetWon(currentLegsWon))
                {
                    setWon = true;
                    currentLegsWon = 0;
                    currentSetsWon++;

                    if (IsGameWon(currentSetsWon))
                    {
                        gameWon = true;
                    }
                }
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
        {
            return ThrowEvaluationResult.Continue(updatedScore);
        }

        // If leg won, there's need to change opponent's state.
        var opponentUpdatedScore = new Dictionary<Guid, PlayerScore>();
        
        var updatedOther = opponentScore with
        {
            RemainingInLeg = gameWon ? opponentRemaining : _settings.StartingScorePerLeg,
            LegsWonInSet = gameWon ? opponentLegsWon : setWon ? 0 : opponentLegsWon,
            SetsWonInMatch = opponentSetsWon
        };

        opponentUpdatedScore[opponentId] = updatedOther;

        if (opponentUpdatedScore.Count == 0)
        {
            opponentUpdatedScore = null;
        }

        return gameWon ? ThrowEvaluationResult.Win(updatedScore, opponentUpdatedScore) : ThrowEvaluationResult.Continue(updatedScore, opponentUpdatedScore);
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
    
    /// <summary>
    /// Checks if current set is a decider.
    /// In decider player must win by 2 legs.
    /// Games stops definately on the 6th leg won.
    /// </summary>
    private bool IsDecider(int currentSetsWon, int opponentSetsWon)
    {
        return _settings.SuddenDeathEnabled 
               && currentSetsWon == _settings.SetsToWinMatch - 1
               && opponentSetsWon == _settings.SetsToWinMatch - 1;
    }

    /// <summary>
    /// Checking the game state changes.
    /// </summary>
    private bool IsSetWon(int currentLegsWon)
    {
        return currentLegsWon >= _settings.LegsToWinSet;
    }

    private bool IsGameWon(int currentSetsWon)
    {
        return currentSetsWon >= _settings.SetsToWinMatch;
    }

    private bool IsDeciderWon(int currentLegsWon, int opponentLegsWon)
    {
        return (currentLegsWon >= _settings.LegsToWinSet && currentLegsWon >= opponentLegsWon + 2)
               || currentLegsWon == _settings.SuddenDeathWinningLeg;
    }
}