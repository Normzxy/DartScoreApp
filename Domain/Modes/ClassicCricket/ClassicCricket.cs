using Domain.Entities;
using Domain.Interfaces;
using Domain.Modes.CutThroatCricket;
using Domain.ValueObjects;

namespace Domain.Modes.ClassicCricket;

public class ClassicCricket(ClassicCricketSettings settings) : IGameMode
{
    private readonly ClassicCricketSettings _settings
        = settings ?? throw new ArgumentNullException(nameof(settings));
    public int DartsPerTurn => _settings.DartsPerTurn;

    public PlayerScore CreateInitialScore(Guid playerId)
        => new CricketScore
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
        if (players.Count != 2)
            throw new InvalidOperationException(
                "Classic Cricket mode requires exactly two players.");
    }

    public ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        ThrowData throwData,
        IReadOnlyDictionary<Guid, PlayerScore> allPlayerScores)
    {
        ArgumentNullException.ThrowIfNull(allPlayerScores);

        var playerScore = allPlayerScores[playerId].AsCricketScore("current player's entry");
        var opponentId = allPlayerScores.Single(kv => kv.Key != playerId).Key;
        var opponentScore = allPlayerScores[opponentId].AsCricketScore("opponent's entry");

        if (!_settings.ScoringSectors.Contains(throwData.Value))
            return ThrowEvaluationResult.Continue(playerScore);

        var sector = throwData.Value;
        var multiplier = throwData.Multiplier;

        var newHits = _settings.CountMultipliers ? multiplier : 1;
        int additionalHits;
        // Could not be changed at all.
        var updatedScore = playerScore;

        // Update player's hits for the sector
        if (!IsSectorClosed(playerScore, sector))
        {
            var sectorHits = GetHitsForSector(playerScore, sector);
            var updatedSectorHits = Math.Min(_settings.HitsToCloseSector, sectorHits + newHits);
            additionalHits = Math.Max(0, sectorHits + newHits - _settings.HitsToCloseSector);
            updatedScore = SetHitsForSector(playerScore, sector, updatedSectorHits);
        }
        else
            additionalHits = newHits;

        // Apply points if player closed sector and opponent didn't
        if (additionalHits > 0 && !IsSectorClosed(opponentScore, sector))
        {
            var pointsToAdd = additionalHits * sector;

            updatedScore = updatedScore with
            {
                Score = updatedScore.Score + pointsToAdd
            };
        }

        // Check for win condition
        if (!AreAllSectorsClosed(updatedScore))
            return ThrowEvaluationResult.Continue(updatedScore);

        return IsGameWon(updatedScore, opponentScore) ? ThrowEvaluationResult.Win(updatedScore)
            : ThrowEvaluationResult.Continue(updatedScore);
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

    private bool IsGameWon(CricketScore playerScore, CricketScore opponentScore)
    {
        return AreAllSectorsClosed(playerScore) && playerScore.Score >= opponentScore.Score;
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
}