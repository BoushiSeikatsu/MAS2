using System;
using System.IO;
using ScottPlot;

public static class ChartGenerator
{
    // Saves an X/Y scatter plot to PNG. If logX/logY is true, axes are displayed in log scale
    public static void SaveDistributionPng(string path, double[] xs, double[] ys, string title, bool logX = true, bool logY = true, int width = 1200, int height = 800)
    {
        if (xs == null) throw new ArgumentNullException(nameof(xs));
        if (ys == null) throw new ArgumentNullException(nameof(ys));
        if (xs.Length != ys.Length) throw new ArgumentException("xs and ys must have same length");
        // Work on copies so caller arrays stay intact
        double[] tx = new double[xs.Length];
        double[] ty = new double[ys.Length];
        Array.Copy(xs, tx, xs.Length);
        Array.Copy(ys, ty, ys.Length);

        if (logX)
        {
            for (int i = 0; i < tx.Length; i++) tx[i] = tx[i] > 0 ? Math.Log10(tx[i]) : double.NaN;
        }
        if (logY)
        {
            for (int i = 0; i < ty.Length; i++) ty[i] = ty[i] > 0 ? Math.Log10(ty[i]) : double.NaN;
        }

    var plt = new ScottPlot.Plot();
    var scatter = plt.Add.Scatter(tx, ty);
    scatter.MarkerSize = 6;
        plt.Title(title);
        plt.XLabel(logX ? "log10(x)" : "x");
        plt.YLabel(logY ? "log10(y)" : "y");
    // grid appearance will use default settings

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        // Save with requested pixel size
        plt.SavePng(path, width, height);
    }
}
