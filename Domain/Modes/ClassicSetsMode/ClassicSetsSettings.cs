namespace Domain.Modes.ClassicSetsMode;

public sealed class ClassicSetsSettings
{
    public int StartingScorePerLeg { get; }
    public int LegsToWinSet { get; }
    public int SetsToWinMatch { get; }
    public bool DoubleOutEnabled { get; }
    public bool SuddenDeathEnabled { get; }
    public int SuddenDeathWinningLeg { get; }
    
    private static readonly int[] AllowedStartingScores = { 201, 301, 401, 501, 601, 701 };

    public ClassicSetsSettings(
        int startingScorePerLeg = 501,
        int setsToWinMatch = 3,
        bool doubleOutEnabled = false,
        bool suddenDeathEnabled = false,
        int suddenDeathWinningLeg = 6)
    {
        if (!AllowedStartingScores.Contains(startingScorePerLeg))
            throw new ArgumentOutOfRangeException(
                nameof(startingScorePerLeg), startingScorePerLeg, $"Score per leg must be one of: {string.Join(", ", AllowedStartingScores)}.");

        if (setsToWinMatch is < 3 or > 7)
            throw new ArgumentOutOfRangeException(
                nameof(setsToWinMatch),
                setsToWinMatch,
                "SetsToWinMatch must be between 3 and 7 (inclusive).");

        StartingScorePerLeg = startingScorePerLeg;
        LegsToWinSet = 3;
        SetsToWinMatch = setsToWinMatch;
        DoubleOutEnabled = doubleOutEnabled;
        SuddenDeathEnabled = suddenDeathEnabled;
        SuddenDeathWinningLeg = suddenDeathWinningLeg;
    }
}