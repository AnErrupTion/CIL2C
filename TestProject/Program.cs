namespace TestProject;

public static class Program
{
    public static void Main()
    {
        unsafe
        {
            var buffer = (byte*)0xB8000;
            *buffer++ = 65;
            *buffer = 15;
        }
    }
}