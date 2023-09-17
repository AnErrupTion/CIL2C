namespace CIL2C;

public class Logger
{
    public readonly bool Enabled;

    public Logger(bool enabled) => Enabled = enabled;

    public void Info(string text)
    {
        if (!Enabled) return;

        Console.WriteLine($"[INFO] {text}");
    }
}