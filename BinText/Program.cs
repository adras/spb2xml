

using System.Collections;
using System.Reflection.PortableExecutable;
using System.Text;
using spb2xml;
using static System.Net.Mime.MediaTypeNames;

internal class Program
{
    private static void Main(string[] args)
    {
        string a = Encode("Hello world");
        string b = Decode(a);

        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        string action = args[0].ToLower();
        if (args.Length <= 1)
        {
            Console.WriteLine("Missing parameter");
            PrintUsage();
            return;
        }

        string param = args[1];

        switch (action)
        {
            case "-e":
                string encoded = Encode(param);
                break;
            case "-d":
                string decoded = Decode(param);
                break;
            case "-a":
                Analyze(param);
                break;
        }
    }

    private static void Analyze(string param)
    {
    }

    private static string Decode(string param)
    {
        byte[] byteArray = param
            .Split(' ') // Split by space
            .Select(hex => Convert.ToByte(hex, 16)) // Convert each hex value to a byte
            .ToArray(); // Convert the result into a byte array



        ;
        int stringLen = BitConverter.ToInt32(byteArray[0..4]);
        string s;
        if (stringLen <= 0)
        {
            s = "";
        }
        else
        {
            s = TextDecode.Decode(byteArray[4..]);
        }

        return s;
    }

    private static string Encode(string param)
    {
        byte[] bytes = TextDecode.Encode(param);

        string result = BitConverter.ToString(bytes).Replace("-", " ");

        string length = BitConverter.ToString(BitConverter.GetBytes(param.Length)).Replace("-", " ");

        result = $"{length} {result}";

        string test = TextDecode.Decode(bytes);
        return result;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("-e -> encodes the given string into hex");
        Console.WriteLine("-d -> decodes the given hex into a string");
        Console.WriteLine("-a -> analyzes the given file");

        Console.WriteLine();
        Console.WriteLine("Example: tool.exe -e \"0D 0A FF 13 37\"");
    }
}