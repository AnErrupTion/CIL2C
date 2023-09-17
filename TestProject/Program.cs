using System.Runtime.InteropServices;

namespace TestProject;

public static class Program
{
    private static readonly byte StartChar = 64;

    private const byte EndChar = 68;

    private const ushort Com1 = 0x3F8;

    private const byte ComData = 0x00;
    private const byte ComInterrupt = 0x01;
    private const byte ComLineControl = 0x02;
    private const byte ComModemControl = 0x03;
    private const byte ComLineStatus = 0x04;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SerialChar
    {
        public byte Char1;
        public byte Char2;
    }

    [CFunctionImport(IncludeFile = "utils.h", FunctionName = "outb")]
    private static extern void Out8(ushort port, byte value);

    public static void Main()
    {
        Out8(Com1 + ComInterrupt, 0x00);
        Out8(Com1 + ComModemControl, 0x80);
        Out8(Com1 + ComData, 0x01);
        Out8(Com1 + ComInterrupt, 0x00);
        Out8(Com1 + ComModemControl, 0x03);
        Out8(Com1 + ComLineControl, 0xC7);
        Out8(Com1 + ComLineStatus, 0x0B);
        Out8(Com1 + ComInterrupt, 0x0F);

        for (var i = StartChar; i <= EndChar; i++)
        {
            var chrL = new SerialChar
            {
                Char1 = (byte)(Add(i) - 1),
                Char2 = 67
            };
                
            Out8(Com1, chrL.Char1);
            Out8(Com1, chrL.Char2);
        }

        var chrE = new SerialChar
        {
            Char1 = 65,
            Char2 = 67
        };

        Out8(Com1, chrE.Char1);
        Out8(Com1, chrE.Char2);

        while (true)
        { }
    }

    private static byte Add(byte a, byte b = 0)
    {
        b = 2;
        return (byte)(a + b);
    }
}