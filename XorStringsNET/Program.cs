using dnlib.DotNet;
using System;
using System.IO;

namespace XorStringsNET
{
    internal class Program
    {
        private static ModuleDefMD Module = null;
        private static string filePath = String.Empty;
        static void Main(string[] args)
        {
            filePath = Utils.Remove(args[0], "\"");
            while (!File.Exists(filePath)){
                Console.WriteLine("File Path: ");
                filePath = Utils.Remove(Console.ReadLine(), "\"");
                Console.Clear();
            }

            Module = ModuleDefMD.Load(filePath);
            
            var stringEncryption = new StringEncryption(Module);
            
            Module = stringEncryption.Run();

            string outputPath = args[0].Insert(args[0].Length - 4, "_packed");
            Module.Write(outputPath);

            Console.WriteLine($"Strings have been encrypted: \nOutput: {outputPath}");
            Console.ReadKey();
        }
    }
}