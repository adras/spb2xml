

using System.Collections;
using System.Reflection.PortableExecutable;
using System.Text;
using spb2xml;
using static System.Net.Mime.MediaTypeNames;

internal class Program
{
    private static void Main(string[] args)
    {
        //Analyze(@"d:\\FlightSimulator24\\_DEV\\spb2xml-master\\BinText\\bin\\Debug\\net8.0\\Scoring_Override.spb");
        //return;
        System.Diagnostics.Debugger.Launch();
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
                Console.WriteLine(encoded);
                return;
            case "-d":
                string decoded = Decode(param);
                Console.WriteLine(decoded);
                return;
            case "-a":
                Analyze(param);
                break;
        }
    }

    private static void Analyze(string param)
    {
        BinaryReader reader = new BinaryReader(File.OpenRead(param));
        byte[] content = reader.ReadBytes((int)new FileInfo(param).Length);

        for (int i = 0; i < content.Length; i++)
        {
            if (i + 4 > content.Length)
                return;

            UInt32 length = BitConverter.ToUInt32(content[new Range(i, i + 4)]);
            if (length == 0)
                continue;
            // Length is longer than the remaining bytes, we use this as indicator for valid values
            if (length > (content.Length - i))
            {
                // invalid, continue
                continue;
            }
            i += 4;
            byte[] data = content[new Range(i, (int)(i + length))];
            try
            {

                string test = TextDecode.Decode(data);
                string dataString = BitConverter.ToString(content[new Range(i - 4, (int)(i + length + 4))]).Replace("-", " ");
                string offset = i.ToString("X");
                Console.WriteLine($"Offset: {offset} - {test} - {dataString}");
                i += data.Length;
            }
            catch (Exception ex)
            {
                i -= 4;
                continue;
            }

        }
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

        string length = BitConverter.ToString(BitConverter.GetBytes(bytes.Length)).Replace("-", " ");

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