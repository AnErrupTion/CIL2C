namespace TestProject;

public static class Program
{
    public static void Main()
    {
        unsafe
        {
            var buffer = (byte*)0xB8000;
            *buffer++ = Add(64, 1);
            *buffer = 15;
        }

        while (true)
        { }
    }

    private static byte Add(byte a, byte b) => (byte)(a + b);
}