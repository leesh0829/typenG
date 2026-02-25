namespace TypeOverlay.Models;

public sealed class TypingStats
{
    public int TotalKeystrokes { get; set; }
    public int CorrectKeystrokes { get; set; }

    public double Cpm(double elapsedMinutes)
    {
        if (elapsedMinutes <= 0) return 0;
        return TotalKeystrokes / elapsedMinutes;
    }

    public double Wpm(double elapsedMinutes)
    {
        if (elapsedMinutes <= 0) return 0;
        return (TotalKeystrokes / 5.0) / elapsedMinutes;
    }

    public double AccuracyPercent()
    {
        if (TotalKeystrokes == 0) return 100;
        return CorrectKeystrokes * 100.0 / TotalKeystrokes;
    }
}
