using System;
using System.Collections.Generic;
using Xunit;
using Domain.Modes.ClassicSetsMode;
using Domain.Modes;
using Domain.ValueObjects;
using Domain.Entities;

namespace Domain.Tests
{
    public class ClassicSetsModeTests
    {
        private static (ClassicSetsMode mode, Player p1, Player p2, ClassicSetsPlayerScore s1, ClassicSetsPlayerScore s2, Dictionary<Guid, PlayerScore> allScores)
            Setup
            (int startingScore = 201,
            bool doubleOutEnabled = false,
            bool suddenDeath = false,
            int setsToWin = 3,
            int suddenDeathWinningLeg = 6)
        {
            var settings = new ClassicSetsSettings(
                startingScorePerLeg: startingScore,
                doubleOutEnabled: doubleOutEnabled,
                suddenDeathEnabled: suddenDeath,
                setsToWinMatch: setsToWin,
                suddenDeathWinningLeg: suddenDeathWinningLeg
            );

            var mode = new ClassicSetsMode(settings);

            var p1 = new Player("P_1");
            var p2 = new Player("P_2");
            var s1 = (ClassicSetsPlayerScore)mode.CreateInitialScore(p1.Id);
            var s2 = (ClassicSetsPlayerScore)mode.CreateInitialScore(p2.Id);
            var allScores = new Dictionary<Guid, PlayerScore>
            {
                [p1.Id] = s1,
                [p2.Id] = s2
            };

            return (mode, p1, p2, s1, s2, allScores);
        }

        [Fact]
        public void Bust_when_negative_score()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: false);
            s1 = s1 with { RemainingInLeg = 50, LegsWonInSet = 0, SetsWonInMatch = 0};
            allScores[p1.Id] = s1;

            var dart = new ThrowData(17, 3);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Bust_when_leaving_one_in_doubleout_mode()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: true);
            s1 = s1 with { RemainingInLeg = 52, LegsWonInSet = 0, SetsWonInMatch = 0};
            allScores[p1.Id] = s1;

            var dart = new ThrowData(17, 3);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Bust_when_zero_but_not_double_in_doubleout_mode()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: true);
            s1 = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};
            allScores[p1.Id] = s1;

            var dart = new ThrowData(20, 1);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        }

        [Fact]
        public void Normal_subtraction_decreases_remaining()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: false);
            var dart = new ThrowData(20, 3);

            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            Assert.Equal(201 - 60, updated.RemainingInLeg);
        }

        [Fact]
        public void Win_leg_single_when_double_out_disabled()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: false);
            s1 = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};
            allScores[p1.Id] = s1;

            var dart = new ThrowData(20, 1);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(1, updated.LegsWonInSet);
            Assert.Equal(0, updated.SetsWonInMatch);
        }

        [Fact]
        public void Win_leg_double_when_double_out_enabled()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: true);
            s1 = s1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0};
            allScores[p1.Id] = s1;

            var dart = new ThrowData(10, 2);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(1, updated.LegsWonInSet);
            Assert.Equal(0, updated.SetsWonInMatch);
        }

        [Fact]
        public void Win_set_resets_other_player_leg_counters_and_increments_sets()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: false);
            s1 = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 0 };
            s2 = s2 with { RemainingInLeg = 10, LegsWonInSet = 1, SetsWonInMatch = 0 };
            allScores[p1.Id] = s1;
            allScores[p2.Id] = s2;

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            // Set won, match not yet -> Continue with otherUpdatedStates present
            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player.
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(1, updated.SetsWonInMatch);
            // Second player.
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(201, otherUpdated.RemainingInLeg);
            Assert.Equal(0, otherUpdated.LegsWonInSet);
            Assert.Equal(0, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Win_match_normal_flow()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: false, setsToWin: 3);
            // player close to match: sets = 2 and legsWonInSet = 2 -> win leg leads to set and match
            s1 = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 2 };
            s2 = s2 with { RemainingInLeg = 10, LegsWonInSet = 1, SetsWonInMatch = 0 };
            allScores[p1.Id] = s1;
            allScores[p2.Id] = s2;

            var dart = new ThrowData(20, 1); // finishes leg -> set -> match
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(10, otherUpdated.RemainingInLeg);
            Assert.Equal(1, otherUpdated.LegsWonInSet);
            Assert.Equal(0, otherUpdated.SetsWonInMatch);
        }
        
        // Minimum 2 legs ahead required to win decider.
        [Fact]
        public void Decider_continues_despite_legs_to_win_match_reached()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: false, suddenDeath: true, setsToWin: 3, suddenDeathWinningLeg: 6);
            s1 = s1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 2 };
            s2 = s2 with { RemainingInLeg = 30, LegsWonInSet = 2, SetsWonInMatch = 2 };
            allScores[p1.Id] = s1;
            allScores[p2.Id] = s2;

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Continue, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(3, updated.LegsWonInSet);
            Assert.Equal(2, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(201, otherUpdated.RemainingInLeg);
            Assert.Equal(2, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Decider_win_by_two_legs_in_sudden_death()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: false, suddenDeath: true, setsToWin: 3, suddenDeathWinningLeg: 6);
            s1 = s1 with { RemainingInLeg = 20, LegsWonInSet = 3, SetsWonInMatch = 2 };
            s2 = s2 with { RemainingInLeg = 30, LegsWonInSet = 2, SetsWonInMatch = 2 };
            allScores[p1.Id] = s1;
            allScores[p2.Id] = s2;

            var dart = new ThrowData(20, 1); // Finishes a leg.
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(30, otherUpdated.RemainingInLeg);
            Assert.Equal(2, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }

        [Fact]
        public void Decider_win_by_sudden_death_last_leg()
        {
            var (mode, p1, p2, s1, s2, allScores)
                = Setup(doubleOutEnabled: false, suddenDeath: true, setsToWin: 3, suddenDeathWinningLeg: 6);
            // in decider: both at sets = 2
            s1 = s1 with { RemainingInLeg = 6, LegsWonInSet = 5, SetsWonInMatch = 2 };
            s2 = s2 with { RemainingInLeg = 20, LegsWonInSet = 5, SetsWonInMatch = 2 };
            allScores[p1.Id] = s1;
            allScores[p2.Id] = s2;

            var dart = new ThrowData(3, 2);
            var result = mode.EvaluateThrow(p1.Id, dart, allScores);

            Assert.Equal(ThrowOutcome.Win, result.Outcome);
            var updated = Assert.IsType<ClassicSetsPlayerScore>(result.UpdatedScore);
            //First player (state changes).
            Assert.Equal(201, updated.RemainingInLeg);
            Assert.Equal(0, updated.LegsWonInSet);
            Assert.Equal(3, updated.SetsWonInMatch);
            // Second player (state does not change).
            Assert.NotNull(result.OtherUpdatedScores);
            var otherUpdated = (ClassicSetsPlayerScore)result.OtherUpdatedScores[p2.Id];
            Assert.Equal(20, otherUpdated.RemainingInLeg);
            Assert.Equal(5, otherUpdated.LegsWonInSet);
            Assert.Equal(2, otherUpdated.SetsWonInMatch);
        }
    }
}