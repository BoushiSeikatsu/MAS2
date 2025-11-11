using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using MAS2;
//Potřeba je kouknout na ty metriky!
namespace MAS2
{
    public class Cviko8
    {
        public static void Main(string[] args)
        {
            var defaultFiles = new List<string>
            {
                Path.Combine("bin", "Debug", "net8.0", "Kaktovi.edges"),
                Path.Combine("bin", "Debug", "net8.0", "Venetie.edges"),
                Path.Combine("bin", "Debug", "net8.0", "Wainwright.edges")
            };

            var layerFiles = args != null && args.Length > 0 ? args.ToList() : defaultFiles;

            // Filter only existing files
            var existing = layerFiles.Where(f => File.Exists(f)).ToList();
            if (existing.Count == 0)
            {
                Console.WriteLine("No layer files found. Provide file paths as arguments or place layer1.csv, layer2.csv, layer3.csv in the working directory.");
                return;
            }

            Console.WriteLine($"Loading {existing.Count} layer files (multilayer edge format)...");
            var ml = MultilayerNetwork.LoadFromMultilayerFiles(existing, ' ', true);
            Console.WriteLine($"Loaded multilayer network with {ml.Layers.Count} layers and {ml.NodeCount} nodes.");
        }
    }
}