namespace Domain.Modes.ClassicCricket;

public sealed class ClassicCricketSettings
{
    public int DartsPerTurn { get; }
    public int HitsToCloseSector { get; }
    public bool CountMultipliers { get; }
    public IReadOnlyList<int> ScoringSectors { get; } = [ 15, 16, 17, 18, 19, 20, 25 ];

    public ClassicCricketSettings(
        int dartsPerTurn = 3,
        int hitsToCloseSector = 3,
        bool countMultipliers = true)
    {
        if (dartsPerTurn is not 3)
            throw new ArgumentOutOfRangeException(nameof(dartsPerTurn));

        if (hitsToCloseSector is < 1 or > 5)
            throw new ArgumentOutOfRangeException(
                nameof(hitsToCloseSector),
                hitsToCloseSector,
                "Hits required to close a sector must be between 1 and 5.");

        DartsPerTurn = dartsPerTurn;
        HitsToCloseSector = hitsToCloseSector;
        CountMultipliers = countMultipliers;
    }
}