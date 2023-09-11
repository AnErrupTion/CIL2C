using System.Runtime.InteropServices;

namespace TestProject;

public static class Program
{
    private static readonly byte StartChar = 64;

    private const byte EndChar = 68;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct VgaChar
    {
        public byte Char;
        public byte Color;
    }

    public static void Main()
    {
        unsafe
        {
            var buffer = (VgaChar*)0xB8000;
            for (var i = StartChar; i <= EndChar; i++)
            {
                *buffer++ = new VgaChar
                {
                    Char = (byte)(Add(i) - 1),
                    Color = 15
                };
            }

            *buffer = new VgaChar
            {
                Char = *(byte*)0xB8000,
                Color = 15
            };
        }

        while (true)
        { }
    }

    private static byte Add(byte a, byte b = 0)
    {
        b = 2;
        return (byte)(a + b);
    }
}