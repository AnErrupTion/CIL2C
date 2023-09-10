namespace TestProject;

public static class Program
{
    private static readonly byte StartChar = 64;

    private const byte EndChar = 68;

    public static void Main()
    {
        unsafe
        {
            var buffer = (byte*)0xB8000;
            for (var i = StartChar; i <= EndChar; i++)
            {
                *buffer++ = (byte)(Add(i, 2) - 1);
                *buffer++ = 15;
            }
        }

        while (true)
        { }
    }

    private static byte Add(byte a, byte b) => (byte)(a + b);
}