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
        PlayerScore playerScore,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores)
    {
        if (playerScore is not ClassicSetsPlayerScore scoreData)
        {
            throw new InvalidOperationException("Expected ClassicSetsPlayerScore for ClassicSetsMode.");
        }

        ArgumentNullException.ThrowIfNull(allPlayerScores);

        // Throws InvalidOperationException if more than one oponent is present.
        var opponentEntry = allPlayerScores.Single(kv => kv.Key != playerId);

        if (!allPlayerScores.ContainsKey(playerId))
        {
            throw new InvalidOperationException("Player state not provided in allPlayerScores.");
        }

        if (opponentEntry.Value is not ClassicSetsPlayerScore otherScore)
        {
            throw new InvalidOperationException("No score data for other player.");
        }

        // Data needed to apply "sudden death" mode when needed.
        var otherLegsWon = otherScore.LegsWonInSet;
        var otherSetsWon = otherScore.SetsWonInMatch;

        // Current player data.
        var currentRemaining = scoreData.RemainingInLeg;
        var currentLegsWon = scoreData.LegsWonInSet;
        var currentSetsWon = scoreData.SetsWonInMatch;

        // FLags to evaluate required Game state changes.
        var legWon = false;
        var setWon = false;
        var gameWon = false;

        var afterThrow = scoreData.RemainingInLeg - throwData.Score;

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
            if (IsDecider(currentSetsWon, otherSetsWon))
            {
                if (IsDeciderWon(currentLegsWon, otherLegsWon))
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
        var updatedScore = scoreData with 
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

        // If leg won, there's need to change other player's states.
        var otherUpdatedScores = new Dictionary<Guid, PlayerScore>();

        foreach (var (currentId, currentScore) in allPlayerScores)
        {
            if (currentId == playerId)
            {
                continue;
            }

            var other = (ClassicSetsPlayerScore)currentScore;

            var updatedOther = other with
            {
                RemainingInLeg = _settings.StartingScorePerLeg,
                LegsWonInSet = setWon ? 0 : other.LegsWonInSet,
                SetsWonInMatch = other.SetsWonInMatch
            };

            if (!other.Equals(updatedOther))
            {
                otherUpdatedScores[currentId] = updatedOther;
            }
        }

        if (otherUpdatedScores.Count == 0)
        {
            otherUpdatedScores = null;
        }

        return gameWon ? ThrowEvaluationResult.Win(updatedScore) : ThrowEvaluationResult.Continue(updatedScore, otherUpdatedScores);
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
    private bool IsDecider(int currentSetsWon, int otherSetsWon)
    {
        return _settings.SuddenDeathEnabled 
               && currentSetsWon == _settings.SetsToWinMatch - 1
               && otherSetsWon == _settings.SetsToWinMatch - 1;
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

    private bool IsDeciderWon(int currentLegsWon, int otherLegsWon)
    {
        return (currentLegsWon >= _settings.LegsToWinSet && currentLegsWon >= otherLegsWon + 2)
               || currentLegsWon == _settings.SuddenDeathWinningLeg;
    }
}