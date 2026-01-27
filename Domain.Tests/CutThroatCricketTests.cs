using Domain.Modes;
using Domain.ValueObjects;
using Domain.Entities;
using Domain.Modes.CutThroatCricket;

namespace Domain.Tests;

public class CutThroatCricketTests
{
    private static (Game game, CutThroatCricket mode, List<Player> players, Dictionary<Guid, PlayerScore> allScores)
        Setup(
            int dartsPerTurn = 3,
            int playerCount = 2,
            int hitsToCloseSector = 3,
            bool countMultipliers = true)
    {
        var settings = new CutThroatCricketSettings(
            dartsPerTurn: dartsPerTurn,
            hitsToCloseSector: hitsToCloseSector,
            countMultipliers: countMultipliers
        );

        var mode = new CutThroatCricket(settings);

        var players = new List<Player>();
        var allScores = new Dictionary<Guid, PlayerScore>();

        for (var i = 1; i <= playerCount; i++)
        {
            var player = new Player($"P_{i + 1}");
            players.Add(player);
            allScores[player.Id] = mode.CreateInitialScore(player.Id);
        }

        var game = new Game(mode, players);

        return (game, mode, players, allScores);
    }

    private static void AssertCannotThrow(Game game, Guid playerId)
    {
        Assert.Throws<InvalidOperationException>(() => 
            game.RegisterThrow(playerId, new ThrowData(20, 1)));
    }

    [Fact]
    public void Throw_on_non_scoring_sector_continues_without_changes()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player = players[0];

        var dart = new ThrowData(5, 1);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);
        Assert.Equal(0, updated.Score);
        Assert.Equal(0, updated.HitsOn20);
        Assert.Equal(0, updated.HitsOn19);
        Assert.Equal(0, updated.HitsOn18);
        Assert.Equal(0, updated.HitsOn17);
        Assert.Equal(0, updated.HitsOn16);
        Assert.Equal(0, updated.HitsOn15);
        Assert.Equal(0, updated.HitsOnBull);
    }

    [Fact]
    public void Single_hit_increments_sector_count()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player = players[0];

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);
        Assert.Equal(1, updated.HitsOn20);
    }

    [Fact]
    public void Triple_hit_counts_as_three_when_multipliers_enabled()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player = players[0];

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);
        Assert.Equal(3, updated.HitsOn20);
    }

    [Fact]
    public void Triple_hit_counts_as_one_when_multipliers_disabled()
    {
        var (_, mode, players, allScores)
            = Setup(countMultipliers: false);
        var player = players[0];

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);
        Assert.Equal(1, updated.HitsOn20);
    }

    [Fact]
    public void Hits_cap_at_required_to_close()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player = players[0];
        var playerScore = (CricketScore)allScores[player.Id];
        allScores[player.Id] = playerScore with { HitsOn20 = 2 };

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);
        Assert.Equal(3, updated.HitsOn20);
    }

    [Fact]
    public void Win_when_all_sectors_closed_and_lowest_or_equal_penalty()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];
        var player2 = players[1];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];

        // P1 will close all sectors in the next throw
        allScores[player1.Id] = p1Score with 
        {
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 3,
            HitsOn19 = 3,
            HitsOn20 = 2,
            HitsOnBull = 3,
            Score = 100
        };

        // P2 didn't close all sectors yet
        allScores[player2.Id] = p2Score with 
        { 
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 2,
            HitsOn19 = 0,
            HitsOn20 = 2,
            HitsOnBull = 1,
            Score = 100
        };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Continue_when_all_sectors_closed_but_higher_penalty()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];
        var player2 = players[1];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];

        // P1 will close all sectors in the next throw
        allScores[player1.Id] = p1Score with 
        { 
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 3,
            HitsOn19 = 3,
            HitsOn20 = 2,
            HitsOnBull = 3,
            Score = 200
        };

        // P2 didn't close all sectors yet
        allScores[player2.Id] = p2Score with 
        { 
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 2,
            HitsOn19 = 0,
            HitsOn20 = 2,
            HitsOnBull = 1,
            Score = 100
        };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Appropriate_penalty_to_opponents()
    {
        var (_, mode, players, allScores)
            = Setup(playerCount: 3);
        var player1 = players[0];
        var player2 = players[1];
        var player3 = players[2];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];
        var p3Score = (CricketScore)allScores[player3.Id];
        allScores[player1.Id] = p1Score with { HitsOn20 = 3 }; // Closed (current)
        allScores[player2.Id] = p2Score with { HitsOn20 = 3 }; // Closed
        allScores[player3.Id] = p3Score with { HitsOn20 = 2 }; // Opened

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.NotNull(result.OtherUpdatedScores);
        var p2Updated = (CricketScore)result.OtherUpdatedScores[player2.Id];
        var p3Updated = (CricketScore)result.OtherUpdatedScores[player3.Id];
        Assert.Equal(0, p2Updated.Score);
        Assert.Equal(20, p3Updated.Score);
    }

    [Fact]
    public void Multiple_additional_hits_apply_multiple_penalties()
    {
        var (_, mode, players, allScores)
            = Setup(playerCount: 3);
        var player1 = players[0];
        var player2 = players[1];
        var player3 = players[2];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];
        var p3Score = (CricketScore)allScores[player3.Id];
        allScores[player1.Id] = p1Score with { HitsOn20 = 2 };
        allScores[player2.Id] = p2Score with { HitsOn20 = 2 };
        allScores[player3.Id] = p3Score with { HitsOn20 = 1 };

        var dart = new ThrowData(20, 3); // Triple: 1 to close, 2 additional
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.NotNull(result.OtherUpdatedScores);
        var p2Updated = (CricketScore)result.OtherUpdatedScores[player2.Id];
        var p3Updated = (CricketScore)result.OtherUpdatedScores[player3.Id];
        Assert.Equal(40, p2Updated.Score);
        Assert.Equal(40, p3Updated.Score);
    }
    
    [Fact]
    public void Workflow_three_player_game()
    {
        var (game, _, players, _)
            = Setup(playerCount:3, dartsPerTurn: 1);
        var p1 = players[0];
        var p2 = players[1];
        var p3 = players[2];

        game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // 20 closed
        AssertCannotThrow(game, p1.Id);

        game.RegisterThrow(p2.Id, new ThrowData(19, 3)); // 19 closed
        AssertCannotThrow(game, p2.Id);

        game.RegisterThrow(p3.Id, new ThrowData(18, 3)); // 18 closed
        AssertCannotThrow(game, p3.Id);

        var r1 = game.RegisterThrow(p1.Id, new ThrowData(20, 2)); // 40 penalty to P2 and P3
        Assert.Equal(ThrowOutcome.Continue, r1.Outcome);

        // Penalty to P2 and P3
        var p2Score = Assert.IsType<CricketScore>(game.ScoreStates[p2.Id]);
        var p3Score = Assert.IsType<CricketScore>(game.ScoreStates[p3.Id]);
        Assert.Equal(40, p2Score.Score); // Changed
        Assert.Equal(40, p3Score.Score); // Changed

        // P2 and P3 close 20
        game.RegisterThrow(p2.Id, new ThrowData(20, 3));
        game.RegisterThrow(p3.Id, new ThrowData(20, 3));

        game.RegisterThrow(p1.Id, new ThrowData(20, 1)); // No penalty (all closed)

        // No penalty to P2 and P3
        p2Score = Assert.IsType<CricketScore>(game.ScoreStates[p2.Id]);
        p3Score = Assert.IsType<CricketScore>(game.ScoreStates[p3.Id]);
        Assert.Equal(40, p2Score.Score); // Unchanged
        Assert.Equal(40, p3Score.Score); // Unchanged
    }
}