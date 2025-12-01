using System;
using System.IO;
using System.Linq;
using System.Drawing;
using ScottPlot;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

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

    // Saves a bar chart with algorithm names on X axis (rotated) and F1 scores on Y axis
    public static void SaveBarChart(string path, string[] labels, double[] values, string title, int width = 1200, int height = 800)
    {
        if (labels == null) throw new ArgumentNullException(nameof(labels));
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (labels.Length != values.Length) throw new ArgumentException("labels and values must have the same length");

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        using (var bmp = new Bitmap(width, height))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.White);

            int marginLeft = 80;
            int marginRight = 40;
            int marginTop = 60;
            int marginBottom = 120; // leave room for rotated labels

            int plotWidth = width - marginLeft - marginRight;
            int plotHeight = height - marginTop - marginBottom;

            // Draw title
            using (var titleFont = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold))
            using (var axisFont = new System.Drawing.Font("Segoe UI", 10))
            using (var labelFont = new System.Drawing.Font("Segoe UI", 9))
            {
                g.DrawString(title, titleFont, Brushes.Black, new RectangleF(marginLeft, 8, plotWidth, 30));

                // Calculate bar positions
                int n = values.Length;
                if (n == 0)
                {
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    return;
                }

                // Force Y axis range to [0,1]
                double minVal = 0.0;
                double maxVal = 1.0;
                double valRange = 1.0;

                int barGap = Math.Max(4, plotWidth / Math.Max(30, n * 5));
                int barWidth = Math.Max(6, (plotWidth - (n + 1) * barGap) / Math.Max(1, n));

                // Draw Y axis ticks and grid
                int ticks = 5;
                for (int t = 0; t <= ticks; t++)
                {
                    float y = marginTop + (float)(plotHeight - (plotHeight * t / (double)ticks));
                    double val = minVal + (valRange * t / ticks);
                    g.DrawLine(Pens.LightGray, marginLeft, y, marginLeft + plotWidth, y);
                    g.DrawString(val.ToString("F2"), axisFont, Brushes.Black, new PointF(8, y - 8));
                }

                // Draw bars
                for (int i = 0; i < n; i++)
                {
                    // Clamp values to [0,1] so chart stays within fixed Y range
                    double raw = values[i];
                    double v = Math.Max(0.0, Math.Min(1.0, raw));
                    float x = marginLeft + barGap + i * (barWidth + barGap);
                    float h = (float)((v - minVal) / valRange * plotHeight);
                    var rect = new RectangleF(x, marginTop + plotHeight - h, barWidth, h);
                    using (var brush = new SolidBrush(System.Drawing.Color.FromArgb(100, 149, 237))) // CornflowerBlue-like
                        g.FillRectangle(brush, rect);
                    g.DrawRectangle(Pens.DimGray, Rectangle.Round(rect));
                }

                // Draw X labels rotated 45 degrees
                for (int i = 0; i < n; i++)
                {
                    float x = marginLeft + barGap + i * (barWidth + barGap) + barWidth / 2f;
                    float y = marginTop + plotHeight + 6;
                    var label = labels[i];
                    // Save state
                    var state = g.Save();
                    // Translate to label position
                    g.TranslateTransform(x, y);
                    g.RotateTransform(45);
                    var size = g.MeasureString(label, labelFont);
                    g.DrawString(label, labelFont, Brushes.Black, -size.Width / 2f, 0);
                    // Restore
                    g.Restore(state);
                }

                // Y axis label
                var yLabel = "F1 score";
                var yState = g.Save();
                g.TranslateTransform(16, marginTop + plotHeight / 2f);
                g.RotateTransform(-90);
                var ySize = g.MeasureString(yLabel, axisFont);
                g.DrawString(yLabel, axisFont, Brushes.Black, -ySize.Width / 2f, 0);
                g.Restore(yState);
            }

            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    // Saves a multi-series line chart where each series has its own X (thresholds) and Y (F1) arrays
    public static void SaveLineChart(string path, string[] seriesLabels, double[][] xsPerSeries, double[][] ysPerSeries, string title, int width = 1200, int height = 800)
    {
        if (seriesLabels == null) throw new ArgumentNullException(nameof(seriesLabels));
        if (xsPerSeries == null) throw new ArgumentNullException(nameof(xsPerSeries));
        if (ysPerSeries == null) throw new ArgumentNullException(nameof(ysPerSeries));
        if (seriesLabels.Length != xsPerSeries.Length || seriesLabels.Length != ysPerSeries.Length)
            throw new ArgumentException("seriesLabels, xsPerSeries and ysPerSeries must have the same length");

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        var plt = new ScottPlot.Plot();
        for (int i = 0; i < seriesLabels.Length; i++)
        {
            var xs = xsPerSeries[i] ?? new double[0];
            var ys = ysPerSeries[i] ?? new double[0];
            var scatter = plt.Add.Scatter(xs, ys);
            scatter.LegendText = seriesLabels[i];
            scatter.MarkerSize = 4;
            scatter.LineWidth = 2;
        }

        plt.Title(title);
        plt.XLabel("threshold");
        plt.YLabel("F1 score");
        // Enable legend and position it Top-Right
        try
        {
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = ScottPlot.Alignment.UpperRight;
        }
        catch { /* some ScottPlot versions expose different API; ignore if unavailable */ }

        // Save using the same API we used earlier
        plt.SavePng(path, width, height);
    }

    // Saves a heatmap to PNG
    public static void SaveHeatmap(string path, double[,] data, string title, string xLabel, string yLabel, int width = 1200, int height = 800)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        var plt = new ScottPlot.Plot();
        
        // Add Heatmap
        var hm = plt.Add.Heatmap(data);
        
        // Customize appearance
        // ScottPlot 5 Heatmap automatically maps values to colors (Viridis by default usually)
        // We can add a colorbar
        plt.Add.ColorBar(hm);

        plt.Title(title);
        plt.XLabel(xLabel);
        plt.YLabel(yLabel);

        // Flip Y axis so Rank 0 is at the top if desired? 
        // Standard matrix: [0,0] is usually bottom-left or top-left depending on library.
        // ScottPlot 5 Heatmap: [0,0] is bottom-left by default.
        // If we want Rank 0 (Top User) at the TOP, we need to invert the Y axis or reverse the data rows.
        // Let's invert the Y axis view.
        // Actually, let's just let it be standard: Rank 0 at bottom (0) to Rank 99 at top (99).
        // Or if we want Rank 0 at top, we can flip the data or the axis.
        // Let's keep it simple: Rank 0 is row 0. In ScottPlot Heatmap, row 0 is at the bottom.
        // So Rank 0 (Top User) will be at the bottom. That's fine.

        plt.SavePng(path, width, height);
    }

    // Saves a simple line chart with custom labels
    public static void SaveSimpleLineChart(string path, double[] xs, double[] ys, string title, string xLabel, string yLabel, int width = 1200, int height = 800)
    {
        if (xs == null) throw new ArgumentNullException(nameof(xs));
        if (ys == null) throw new ArgumentNullException(nameof(ys));
        if (xs.Length != ys.Length) throw new ArgumentException("xs and ys must have same length");

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        var plt = new ScottPlot.Plot();
        var scatter = plt.Add.Scatter(xs, ys);
        scatter.LineWidth = 2;
        scatter.MarkerSize = 5;

        plt.Title(title);
        plt.XLabel(xLabel);
        plt.YLabel(yLabel);

        plt.SavePng(path, width, height);
    }
}
