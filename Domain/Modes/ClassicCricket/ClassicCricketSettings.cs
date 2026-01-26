namespace Domain.Modes.ClassicCricket;

public sealed class ClassicCricketSettings
{
    public int HitsToCloseSector { get; }
    public bool CountMultipliers { get; }
    public IReadOnlyList<int> ScoringSectors { get; } = [ 15, 16, 17, 18, 19, 20, 25 ];

    public ClassicCricketSettings(
        int hitsToCloseSector = 3,
        bool countMultipliers = true)
    {
        if (hitsToCloseSector is < 1 or > 5)
            throw new ArgumentOutOfRangeException(
                nameof(hitsToCloseSector),
                hitsToCloseSector,
                "Hits required to close a sector must be between 1 and 5.");

        HitsToCloseSector = hitsToCloseSector;
        CountMultipliers = countMultipliers;
    }
}