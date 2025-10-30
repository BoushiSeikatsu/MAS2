using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MAS2;
namespace MAS2
{
    /*public class Cviko4
    {
        public static void Main(string[] args)
        {
            int nodeCount = 1500;
            double q = 0.05;
            var linkSelectionMatrix = NetworkGenerator.GenerateLinkSelectionModel(nodeCount);
            NetworkGenerator.SaveMatrixAsEdgeList(linkSelectionMatrix, "link_selection_1500.csv");
            var linkSelectionMatrixIL = NetworkGenerator.GenerateLinkSelectionModelWithInternalLinks(nodeCount, q);
            NetworkGenerator.SaveMatrixAsEdgeList(linkSelectionMatrixIL, "link_selection_1500_with_internal_links.csv");

            double p = 0.5;
            var copyingModelMatrix = NetworkGenerator.GenerateCopyingModel(nodeCount, p);
            NetworkGenerator.SaveMatrixAsEdgeList(copyingModelMatrix, "copying_model_1500.csv");
            var copyingModelMatrixIL = NetworkGenerator.GenerateCopyingModelWithInternalLinks(nodeCount, p, q);
            NetworkGenerator.SaveMatrixAsEdgeList(copyingModelMatrixIL, "copying_model_1500_with_internal_links.csv");

            
            int m = 3; 
            var barabasiMatrix = NetworkGenerator.GenerateBarabasiAlbert(nodeCount, m);
            NetworkGenerator.SaveMatrixAsEdgeList(barabasiMatrix, $"barabasi_albert_{nodeCount}_m{m}.csv");
            Console.WriteLine("Networks generated and saved.");
        }
    }*/
}
