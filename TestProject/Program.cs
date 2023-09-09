namespace TestProject;

public static class Program
{
    public static void Main()
    {
        unsafe
        {
            var buffer = (byte*)0xB8000;
            *buffer++ = GetAChar();
            *buffer = 15;
        }
    }

    private static byte GetAChar() => 65;
}