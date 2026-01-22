using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Modes.CutThroatCricket;

public class CutThroatCricket(CutThroatCricketSettings settings) : IGameMode
{
    private readonly CutThroatCricketSettings _settings
        = settings ?? throw new ArgumentNullException(nameof(settings));

    public PlayerScore CreateInitialScore(Guid playerId)
        => new CricketScore()
        {
            PlayerId = playerId,
            Score = 0,
            HitsOn15 = 0,
            HitsOn16 = 0,
            HitsOn17 = 0,
            HitsOn18 = 0,
            HitsOn19 = 0,
            HitsOn20 = 0,
            HitsOnBull = 0
        };

    public void ValidatePlayers(IReadOnlyCollection<Player> players)
    {
        if (players.Count is < 2 or > 4)
        {
            throw new ArgumentException(
                "Cut-Throat Cricket mode requires 2 - 4 players.");
        }
    }

    public ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores)
    {
        ArgumentNullException.ThrowIfNull(allPlayerScores);

        if (allPlayerScores.Count is 0)
        {
            throw new InvalidOperationException("There's no player scores.");
        }

        var playerScore = allPlayerScores[playerId].AsCricketScore("current player's entry");

        if (!_settings.ScoringSectors.Contains(throwData.Value))
        {
            return ThrowEvaluationResult.Continue(playerScore);
            // There is no bust in this mode.
        }

        var sector = throwData.Value;
        var multiplier = throwData.Multiplier;

        var newHits = _settings.CountMultipliers ? multiplier : 1;
        int additionalHits;
        // Could not be changed at all.
        var updatedScore = playerScore;

        // Player's state needs to be changed.
        if (!IsSectorClosed(playerScore, sector))
        {
            var sectorHits = GetHitsForSector(playerScore, sector);
            var updatedSectorHits = Math.Min(_settings.HitsToCloseSector, sectorHits + newHits);
            additionalHits = Math.Max(0, sectorHits + newHits - _settings.HitsToCloseSector);
            // Only player's hits (not the score) are updated during player's turn.
            updatedScore = SetHitsForSector(playerScore, sector, updatedSectorHits);
        }
        else
        {
            additionalHits = newHits;
        }

        if (additionalHits is 0 && !AreAllSectorsClosed(updatedScore))
        {
            return ThrowEvaluationResult.Continue(updatedScore);
        }

        // If additionalHits is 0 and ALL SECTORS ARE CLOSED:
        // penaltyScore is 0 effectively (so no penalty) and
        // otherUpdatedScores is needed anyway (with unchanged state)
        // to evaluate if player's score is sufficient to win or tie the game,
        // based on the other players.

        // If penalty to apply, there's need to change opponent's state.
        var penaltyScore = additionalHits * sector;

        var otherUpdatedScores
            = ApplyPenaltiesToOpponents(
                playerId, sector, penaltyScore, allPlayerScores);

        if (!AreAllSectorsClosed(updatedScore))
        {
            return ThrowEvaluationResult.Continue(updatedScore, otherUpdatedScores);
        }

        var bestClosures = GetLeadersWithAllClosedSectors(playerId, updatedScore, otherUpdatedScores);

        return IsGameWon(playerId, bestClosures) ? ThrowEvaluationResult.Win(updatedScore, otherUpdatedScores)
            : IsGameTied(playerId, bestClosures) ? ThrowEvaluationResult.Tie(updatedScore, otherUpdatedScores)
            : ThrowEvaluationResult.Continue(updatedScore, otherUpdatedScores);
    }

    private bool IsSectorClosed(CricketScore score, int sector)
    {
        return GetHitsForSector(score, sector) >= _settings.HitsToCloseSector;
    }

    private bool AreAllSectorsClosed(CricketScore score)
    {
        return _settings.ScoringSectors
            .All(sector => GetHitsForSector(score, sector) >= _settings.HitsToCloseSector);
    }

    private Dictionary<Guid, PlayerScore> ApplyPenaltiesToOpponents(
        Guid currentPlayerId,
        int sector,
        int penaltyScore,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores)
    {
        var updatedScores = new Dictionary<Guid, PlayerScore>();

        foreach (var (id, score) in allPlayerScores)
        {
            if (id == currentPlayerId)
            {
                continue;
            }

            var cricketScore = score.AsCricketScore("opponent update");
            var shouldApplyPenalty = !IsSectorClosed(cricketScore, sector);

            updatedScores[id] = cricketScore with
            {
                Score = shouldApplyPenalty 
                    ? cricketScore.Score + penaltyScore 
                    : cricketScore.Score
            };
        }

        return updatedScores;
    }

    /// <summary>
    /// Return dictionary of players who have the lowest penalty score with all sectors closed.
    /// </summary>
    private Dictionary<Guid, CricketScore> GetLeadersWithAllClosedSectors(
        Guid playerId,
        CricketScore playerScore,
        IReadOnlyDictionary<Guid, PlayerScore> otherScores)
    {
        var cricketEntries = otherScores.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.AsCricketScore("getting leaders"));

        cricketEntries[playerId] = playerScore;

        var minPenalty = cricketEntries.Min(kv => kv.Value.Score);

        // Leaders with closed sectors.
        return cricketEntries
            .Where(kv => kv.Value.Score == minPenalty
                         && AreAllSectorsClosed(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static int GetHitsForSector(CricketScore score, int sector)
        => sector switch
        {
            15 => score.HitsOn15,
            16 => score.HitsOn16,
            17 => score.HitsOn17,
            18 => score.HitsOn18,
            19 => score.HitsOn19,
            20 => score.HitsOn20,
            25 => score.HitsOnBull,
            _ => throw new InvalidOperationException("Unsupported sector: " + sector)
        };

    private static CricketScore SetHitsForSector(CricketScore score, int sector, int hits)
        => sector switch
        {
            15 => score with { HitsOn15 = hits },
            16 => score with { HitsOn16 = hits },
            17 => score with { HitsOn17 = hits },
            18 => score with { HitsOn18 = hits },
            19 => score with { HitsOn19 = hits },
            20 => score with { HitsOn20 = hits },
            25 => score with { HitsOnBull = hits },
            _ => throw new InvalidOperationException("Unsupported sector: " + sector)
        };

    private static bool IsGameWon(
        Guid playerId,
        Dictionary<Guid, CricketScore> bestClosures)
    {
        return bestClosures.ContainsKey(playerId)
               && bestClosures.Count == 1;
    }

    private static bool IsGameTied(
        Guid playerId,
        Dictionary<Guid, CricketScore> bestClosures)
    {
        return bestClosures.ContainsKey(playerId)
               && bestClosures.Count > 1;
    }
}