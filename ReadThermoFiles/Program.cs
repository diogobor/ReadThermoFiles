using ReadThermoFiles.Control;
using System.Text.RegularExpressions;

namespace ReadThermoFiles
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("##################################");
            Console.WriteLine("     Welcome to Thermo Reader  ");
            Console.WriteLine("  Developed by Diogo Borges Lima  ");
            Console.WriteLine("##################################\n");
            Console.WriteLine("Instructions:");
            Console.WriteLine("1- Type (or paste) the full path of the Thermo RAW file");
            Console.WriteLine("2- Type the desired MSn level: 1 or 2");
            Console.WriteLine("=> The output will be a *.ms1 or *.ms2 file generated in the same directory of the input file.\n");
            Console.WriteLine("Raw file:");
            string raw_file = Console.ReadLine();
            while (String.IsNullOrEmpty(raw_file))
            {
                Console.WriteLine("Invalid Raw file.");
                Console.WriteLine("Raw file:");
                raw_file = Console.ReadLine();
            }
            Console.WriteLine("MSn level:");
            string str_msn_level = Console.ReadLine();
            while (!isNumeric(str_msn_level))
            {
                Console.WriteLine("Invalid MSn level.");
                Console.WriteLine("MSn level:");
                str_msn_level = Console.ReadLine();
            }
            int msn_level = Convert.ToInt32(str_msn_level);

            var ms = ParserThermo.Parse(raw_file, (short)msn_level);
            ExportMS.Converter2MSn(ms,raw_file, (short)msn_level);

            Console.WriteLine("File has been exported successfully.");
        }

        private static bool isNumeric(string str)
        {
            if (String.IsNullOrEmpty(str)) return false;

            string pattern = @"^\d+$";
            return Regex.IsMatch(str, pattern);
        }
    }
}