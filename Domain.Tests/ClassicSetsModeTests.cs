using Domain.Modes.ClassicSetsMode;
using Domain.Modes;
using Domain.ValueObjects;
using Domain.Entities;

namespace Domain.Tests
{
    public class ClassicSetsModeTests
    {
        private static (Game game, ClassicSetsMode mode, Player p1, Player p2, ClassicSetsScore s1, ClassicSetsScore s2, Dictionary<Guid, PlayerScore> allScores)
            Setup
            (int scorePerLeg = 201,
            bool doubleOutEnabled = false,
            bool advantagesEnabled = false,
            int setsToWinMatch = 3,
            int suddenDeathWinningLeg = 6)
        {
            var settings = new ClassicSetsModeSettings(
                scorePerLeg: scorePerLeg,
                doubleOutEnabled: doubleOutEnabled,
                advantagesEnabled: advantagesEnabled,
                setsToWinMatch: setsToWinMatch,
                suddenDeathWinningLeg: suddenDeathWinningLeg
            );

            var mode = new ClassicSetsMode(settings);

            var p1 = new Player("P_1");
            var p2 = new Player("P_2");
            var players = new List<Player> { p1, p2 };

            var s1 = (ClassicSetsScore)mode.CreateInitialScore(p1.Id);
            var s2 = (ClassicSetsScore)mode.CreateInitialScore(p2.Id);
            var allScores = new Dictionary<Guid, PlayerScore>
            {
                [p1.Id] = s1,
                [p2.Id] = s2
            };
            
            var game = new Game(mode, players);

            return (game, mode, p1, p2, s1, s2, allScores);
        }

        [Fact]
        public void Bust_when_negative_score()
        {
            var (_, mode, p1, _, s1, _, allScores)
                = Setup();
            allScores[p1.Id] = s1 with { RemainingInLeg = 50, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(17, 3);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Bust_when_leaving_one_in_doubleout_mode()
        {
            var (_, mode, p1, _, s1, _, allScores)
                = Setup(doubleOutEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 52, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(17, 3);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Bust_when_zero_but_not_double_in_doubleout_mode()
        {
            var (_, mode, p1, _, s1, _, allScores)
                = Setup(doubleOutEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(20, 1);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Normal_subtraction_decreases_remaining()
        {
            var (_, mode, p1, _, _, _, allScores)
                = Setup();
            var dart = new ThrowData(20, 3);

            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
            Assert.Equal(201 - 60, updated.RemainingInLeg);
        }

        [Fact]
        public void Win_leg_single_when_double_out_disabled()
        {
            var (_, mode, p1, _, s1, _, allScores)
                = Setup();
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(20, 1);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(1, updated.LegsWonInSet);
            Assert.Equal(0, updated.SetsWonInMatch);
        }

        [Fact]
        public void Win_leg_double_when_double_out_enabled()
        {
            var (_, mode, p1, _, s1, _, allScores)
                = Setup(doubleOutEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};

            var dart = new ThrowData(10, 2);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(1, updated.LegsWonInSet);
            Assert.Equal(0, updated.SetsWonInMatch);
        }

        [Fact]
        public void Win_set_resets_other_player_leg_counters_and_increments_sets()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup();
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 0 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 10, LegsWonInSet = 1, SetsWonInMatch = 0 };

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            // Set won, match not yet -> Continue with otherUpdatedStates present
            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
            //First player.
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(1, updated.SetsWonInMatch);
            // Second player.
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(201, otherUpdated.RemainingInLeg);
            Assert.Equal(0, otherUpdated.LegsWonInSet);
            Assert.Equal(0, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Win_match_normal_flow()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup();
            // player close to match: sets = 2 and legsWonInSet = 2 -> win leg leads to set and match
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 2 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 10, LegsWonInSet = 1, SetsWonInMatch = 0 };

            var dart = new ThrowData(20, 1); // finishes leg -> set -> match
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(10, otherUpdated.RemainingInLeg);
            Assert.Equal(1, otherUpdated.LegsWonInSet);
            Assert.Equal(0, otherUpdated.SetsWonInMatch);
        }
        
        // Minimum 2 legs ahead required to win decider.
        [Fact]
        public void Decider_continues_despite_legs_to_win_match_reached()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(advantagesEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 2 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 30, LegsWonInSet = 2, SetsWonInMatch = 2 };

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(3, updated.LegsWonInSet);
            Assert.Equal(2, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(201, otherUpdated.RemainingInLeg);
            Assert.Equal(2, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Decider_win_by_two_legs_in_sudden_death()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(advantagesEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 20, LegsWonInSet = 3, SetsWonInMatch = 2 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 30, LegsWonInSet = 2, SetsWonInMatch = 2 };

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(30, otherUpdated.RemainingInLeg);
            Assert.Equal(2, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Decider_win_by_sudden_death_last_leg()
        {
            var (_, mode, p1, p2, s1, s2, allScores)
                = Setup(advantagesEnabled: true);
            allScores[p1.Id] = s1 with { RemainingInLeg = 6, LegsWonInSet = 5, SetsWonInMatch = 2 };
            allScores[p2.Id] = s2 with { RemainingInLeg = 20, LegsWonInSet = 5, SetsWonInMatch = 2 };

            var dart = new ThrowData(3, 2);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(20, otherUpdated.RemainingInLeg);
            Assert.Equal(5, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Workflow_tests()
        {
            var (game, _, p1, p2, _, _, _)
                = Setup(doubleOutEnabled:true);

            // P1 turn (starts the set)
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:0:21
            // P1 Should not be able to throw
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            // P2 Should be able to throw
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:0:21
            // P1 turn
            game.RegisterThrow(p1.Id, new ThrowData(7, 3)); // (P1) 0:0:21 -> bust
            // P2 turn
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            game.RegisterThrow(p2.Id, new ThrowData(1, 1));
            var r1 = game.RegisterThrow(p2.Id, new ThrowData(10, 2)); // (P2) 0:1:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r1.Proggress);
            // P2 turn (starts a leg).
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:1:21
            // P1 turn
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:0:21
            // P2 turn
            game.RegisterThrow(p2.Id, new ThrowData(1, 1));
            var r2 = game.RegisterThrow(p2.Id, new ThrowData(10, 2)); // (P2) 0:2:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r2.Proggress);
            // P1 turn (starts the leg)
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p2.Id, new ThrowData(1,1)));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:0:21
            // P2 turn
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:2:21
            // P1 turn
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p2.Id, new ThrowData(1,1)));
            game.RegisterThrow(p1.Id, new ThrowData(7, 1));
            var r3 = game.RegisterThrow(p1.Id, new ThrowData(7, 2)); // (P1) 0:1:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r3.Proggress);
            // P2 turn (starts the leg)
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:2:21
            // P1 turn
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:1:21
            // P2 turn
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            game.RegisterThrow(p2.Id, new ThrowData(20, 1)); // (P2) 0:2:21 -> bust
            // P1 turn
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p2.Id, new ThrowData(1,1)));
            game.RegisterThrow(p1.Id, new ThrowData(1, 1));
            var r4 = game.RegisterThrow(p1.Id, new ThrowData(10, 2)); // (P1) 0:2:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r4.Proggress);

            // Score states check
            var p1Score = Assert.IsType<ClassicSetsScore>(game.ScoreStates[p1.Id]);
            var p2Score = Assert.IsType<ClassicSetsScore>(game.ScoreStates[p2.Id]);
            // P1
            Assert.Equal(201, p1Score.RemainingInLeg);
            Assert.Equal(2, p1Score.LegsWonInSet);
            Assert.Equal(0, p1Score.SetsWonInMatch);
            // P2
            Assert.Equal(201, p2Score.RemainingInLeg);
            Assert.Equal(2, p2Score.LegsWonInSet);
            Assert.Equal(0, p2Score.SetsWonInMatch);

            // P1 turn (starts the leg)
            game.RegisterThrow(p1.Id, new ThrowData(19, 3));
            game.RegisterThrow(p1.Id, new ThrowData(19, 3));
            game.RegisterThrow(p1.Id, new ThrowData(19, 3)); // (P1) 0:2:30
            // P2 turn (starts the leg)
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 0:2:21
            // P1 turn (starts the leg)
            game.RegisterThrow(p1.Id, new ThrowData(10, 2));
            game.RegisterThrow(p1.Id, new ThrowData(5, 1));
            game.RegisterThrow(p1.Id, new ThrowData(20, 1)); // (P1) 0:2:30 -> bust
            // P2 turn (starts the leg)
            game.RegisterThrow(p2.Id, new ThrowData(5, 1));
            var r5 = game.RegisterThrow(p2.Id, new ThrowData(8, 2)); // (P2) 1:0:201 -> set won
            Assert.Equal(ProggressInfo.SetWon, r5.Proggress);
            // P2 turn (starts the set)
            Assert.Throws<InvalidOperationException>(() => game.RegisterThrow(p1.Id, new ThrowData(1,1)));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3));
            game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // (P2) 1:0:21
            // P1 turn (starts the leg)
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3));
            game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // (P1) 0:0:21
            // P2 turn
            game.RegisterThrow(p2.Id, new ThrowData(1, 1));
            game.RegisterThrow(p2.Id, new ThrowData(10, 1));
            game.RegisterThrow(p2.Id, new ThrowData(0, 1)); // (P2) 1:0:21
            // P1 turn (starts the leg)
            game.RegisterThrow(p1.Id, new ThrowData(1, 1));
            var r6 = game.RegisterThrow(p1.Id, new ThrowData(10, 2)); // (P1) 0:1:201 -> leg won
            Assert.Equal(ProggressInfo.LegWon, r6.Proggress);
            
            // Score states check
            p1Score = Assert.IsType<ClassicSetsScore>(game.ScoreStates[p1.Id]);
            p2Score = Assert.IsType<ClassicSetsScore>(game.ScoreStates[p2.Id]);
            // P1
            Assert.Equal(201, p1Score.RemainingInLeg);
            Assert.Equal(1, p1Score.LegsWonInSet);
            Assert.Equal(0, p1Score.SetsWonInMatch);
            // P2
            Assert.Equal(201, p2Score.RemainingInLeg);
            Assert.Equal(0, p2Score.LegsWonInSet);
            Assert.Equal(1, p2Score.SetsWonInMatch);
        }
    }
}