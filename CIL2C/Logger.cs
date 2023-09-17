namespace CIL2C;

public static class Logger
{
    private static bool _verbose;

    public static void Initialize(bool verbose) => _verbose = verbose;

    public static void VerboseInfo(string text)
    {
        if (!_verbose) return;

        Console.WriteLine($"[INFO] {text}");
    }

    public static void Info(string text)
    {
        Console.WriteLine($"[INFO] {text}");
    }
}