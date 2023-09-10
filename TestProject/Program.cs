namespace TestProject;

public static class Program
{
    public static void Main()
    {
        unsafe
        {
            var buffer = (byte*)0xB8000;
            for (byte i = 64; i <= 68; i++)
            {
                *buffer++ = Add(i, 1);
                *buffer++ = 15;
            }
        }

        while (true)
        { }
    }

    private static byte Add(byte a, byte b) => (byte)(a + b);
}