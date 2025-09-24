using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MAS2
{
    public class Clique
    {
        public List<int> NodeIds { get; }
        public int Year { get; }

        public Clique(List<int> nodeIds, int year)
        {
            NodeIds = nodeIds;
            Year = year;
        }
    }

    public class TemporalNetworkLoader
    {
        public Dictionary<int, List<Clique>> YearToCliques { get; private set; }

        public TemporalNetworkLoader(string nvertsPath, string simplicesPath, string timesPath)
        {
            YearToCliques = LoadTemporalCliques(nvertsPath, simplicesPath, timesPath);
        }

        private Dictionary<int, List<Clique>> LoadTemporalCliques(string nvertsPath, string simplicesPath, string timesPath)
        {
            var nverts = File.ReadAllLines(nvertsPath).Select(s => int.Parse(s.Trim())).ToList();
            var simplices = File.ReadAllLines(simplicesPath).Select(s => int.Parse(s.Trim())).ToList();
            var times = File.ReadAllLines(timesPath).Select(s => int.Parse(s.Trim())).ToList();

            if (nverts.Count != times.Count)
                throw new Exception($"nverts ({nverts.Count}) and times ({times.Count}) must have the same number of lines.");

            var yearToCliques = new Dictionary<int, List<Clique>>();
            int simplexIdx = 0;
            for (int i = 0; i < nverts.Count; i++)
            {
                int year = times[i];
                int cliqueSize = nverts[i];
                var nodeIds = simplices.Skip(simplexIdx).Take(cliqueSize).ToList();
                simplexIdx += cliqueSize;

                var clique = new Clique(nodeIds, year);
                if (!yearToCliques.ContainsKey(year))
                    yearToCliques[year] = new List<Clique>();
                yearToCliques[year].Add(clique);
            }
            return yearToCliques;
        }
    }
}
