namespace Domain.Modes.FreeForAll;

public class FreeForAllSettings
{
    public int ScorePerLeg { get; }
    public int LegsToWinMatch { get; }
    public bool DoubleOutEnabled { get; }

    private static readonly int[] AllowedStartingScores = [ 201, 301, 401, 501, 601, 701, 801, 901 ];

    public FreeForAllSettings(
        int scorePerLeg = 501,
        int legsToWinMatch = 3,
        bool doubleOutEnabled = false)
    {
        if (!AllowedStartingScores.Contains(scorePerLeg))
            throw new ArgumentOutOfRangeException(
                nameof(scorePerLeg),
                scorePerLeg,
                $"Score per leg must be one of: {string.Join(", ", AllowedStartingScores)}.");

        if (legsToWinMatch is < 1 or > 18)
            throw new ArgumentOutOfRangeException(
                nameof(legsToWinMatch),
                legsToWinMatch,
                "Number of legs required to win a match must be between 1 and 18 (inclusive).");

        ScorePerLeg = scorePerLeg;
        LegsToWinMatch = legsToWinMatch;
        DoubleOutEnabled = doubleOutEnabled;
    }
}