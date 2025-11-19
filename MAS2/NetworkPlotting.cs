using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace MAS2
{
    /// <summary>
    /// Network plotting utilities for visualizing graphs with consistent node positions
    /// and community-based coloring.
    /// </summary>
    public class NetworkPlotting
    {
        /// <summary>
        /// Represents a 2D position for a node in the network visualization.
        /// </summary>
        public class NodePosition
        {
            public int NodeId { get; set; }
            public float X { get; set; }
            public float Y { get; set; }

            public NodePosition(int nodeId, float x, float y)
            {
                NodeId = nodeId;
                X = x;
                Y = y;
            }
        }

        /// <summary>
        /// Color palette for communities - expands automatically as needed.
        /// </summary>
        private static readonly SKColor[] CommunityColors = new[]
        {
            SKColor.Parse("#E74C3C"), // Red
            SKColor.Parse("#3498DB"), // Blue
            SKColor.Parse("#2ECC71"), // Green
            SKColor.Parse("#F39C12"), // Orange
            SKColor.Parse("#9B59B6"), // Purple
            SKColor.Parse("#1ABC9C"), // Turquoise
            SKColor.Parse("#E67E22"), // Dark Orange
            SKColor.Parse("#34495E"), // Dark Blue-Gray
            SKColor.Parse("#16A085"), // Dark Turquoise
            SKColor.Parse("#27AE60"), // Dark Green
            SKColor.Parse("#2980B9"), // Strong Blue
            SKColor.Parse("#8E44AD"), // Dark Purple
            SKColor.Parse("#C0392B"), // Dark Red
            SKColor.Parse("#D35400"), // Pumpkin
            SKColor.Parse("#7F8C8D"), // Gray
        };

        /// <summary>
        /// Get a color for a given community ID.
        /// </summary>
        public static SKColor GetCommunityColor(int communityId)
        {
            if (communityId < 0) return SKColors.Gray;
            return CommunityColors[communityId % CommunityColors.Length];
        }

        /// <summary>
        /// Compute node positions using a deterministic force-directed layout algorithm.
        /// Uses a fixed random seed to ensure consistent positions across runs.
        /// </summary>
        public static Dictionary<int, NodePosition> ComputeForceDirectedLayout(
            DokSparseMatrix<int> adjacency,
            int iterations = 500,
            int seed = 42,
            float width = 1000f,
            float height = 1000f,
            float repulsion = 5000f,
            float attraction = 0.01f,
            float damping = 0.85f)
        {
            int n = adjacency.Rows;
            var positions = new Dictionary<int, NodePosition>();
            var velocities = new Dictionary<int, (float vx, float vy)>();
            var rnd = new Random(seed);

            // Initialize positions randomly but deterministically
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rnd.NextDouble() * width);
                float y = (float)(rnd.NextDouble() * height);
                positions[i] = new NodePosition(i, x, y);
                velocities[i] = (0f, 0f);
            }

            // Build edge list for efficiency
            var edges = new List<(int u, int v, int weight)>();
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    int w = adjacency[i, j];
                    if (w != 0)
                    {
                        edges.Add((i, j, w));
                    }
                }
            }

            // Force-directed iterations
            for (int iter = 0; iter < iterations; iter++)
            {
                var forces = new Dictionary<int, (float fx, float fy)>();
                for (int i = 0; i < n; i++)
                {
                    forces[i] = (0f, 0f);
                }

                // Repulsive forces between all pairs
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        float dx = positions[j].X - positions[i].X;
                        float dy = positions[j].Y - positions[i].Y;
                        float distSq = dx * dx + dy * dy + 0.01f; // avoid division by zero
                        float dist = (float)Math.Sqrt(distSq);

                        float force = repulsion / distSq;
                        float fx = force * dx / dist;
                        float fy = force * dy / dist;

                        forces[i] = (forces[i].fx - fx, forces[i].fy - fy);
                        forces[j] = (forces[j].fx + fx, forces[j].fy + fy);
                    }
                }

                // Attractive forces along edges
                foreach (var (u, v, weight) in edges)
                {
                    float dx = positions[v].X - positions[u].X;
                    float dy = positions[v].Y - positions[u].Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy + 0.01f);

                    float force = attraction * dist * weight;
                    float fx = force * dx / dist;
                    float fy = force * dy / dist;

                    forces[u] = (forces[u].fx + fx, forces[u].fy + fy);
                    forces[v] = (forces[v].fx - fx, forces[v].fy - fy);
                }

                // Update positions with damping
                for (int i = 0; i < n; i++)
                {
                    var (vx, vy) = velocities[i];
                    var (fx, fy) = forces[i];

                    vx = (vx + fx) * damping;
                    vy = (vy + fy) * damping;

                    velocities[i] = (vx, vy);

                    positions[i].X += vx;
                    positions[i].Y += vy;

                    // Keep within bounds
                    positions[i].X = Math.Clamp(positions[i].X, 50f, width - 50f);
                    positions[i].Y = Math.Clamp(positions[i].Y, 50f, height - 50f);
                }
            }

            return positions;
        }

        /// <summary>
        /// Compute node positions using a simple circular layout.
        /// Nodes are arranged in a circle, ensuring consistent positions based on node ID.
        /// </summary>
        public static Dictionary<int, NodePosition> ComputeCircularLayout(
            int nodeCount,
            float centerX = 500f,
            float centerY = 500f,
            float radius = 400f)
        {
            var positions = new Dictionary<int, NodePosition>();
            
            for (int i = 0; i < nodeCount; i++)
            {
                double angle = 2 * Math.PI * i / nodeCount;
                float x = centerX + radius * (float)Math.Cos(angle);
                float y = centerY + radius * (float)Math.Sin(angle);
                positions[i] = new NodePosition(i, x, y);
            }

            return positions;
        }

        /// <summary>
        /// Save node positions to a CSV file for reuse.
        /// </summary>
        public static void SavePositions(Dictionary<int, NodePosition> positions, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine("NodeId,X,Y");
            foreach (var pos in positions.Values.OrderBy(p => p.NodeId))
            {
                writer.WriteLine($"{pos.NodeId},{pos.X},{pos.Y}");
            }
        }

        /// <summary>
        /// Load node positions from a CSV file.
        /// </summary>
        public static Dictionary<int, NodePosition> LoadPositions(string filePath)
        {
            var positions = new Dictionary<int, NodePosition>();
            
            using var reader = new StreamReader(filePath);
            string? headerLine = reader.ReadLine(); // Skip header
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    int nodeId = int.Parse(parts[0]);
                    float x = float.Parse(parts[1]);
                    float y = float.Parse(parts[2]);
                    positions[nodeId] = new NodePosition(nodeId, x, y);
                }
            }
            
            return positions;
        }

        /// <summary>
        /// Plot a network graph with community coloring and save as PNG image.
        /// </summary>
        public static void PlotNetwork(
            DokSparseMatrix<int> adjacency,
            Dictionary<int, NodePosition> positions,
            int[] communityLabels,
            string outputPath,
            int imageWidth = 1200,
            int imageHeight = 1200,
            float nodeSize = 15f,
            float edgeWidth = 1.5f,
            bool drawEdgeWeights = false,
            string? title = null)
        {
            int n = adjacency.Rows;

            using var surface = SKSurface.Create(new SKImageInfo(imageWidth, imageHeight));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            // Draw title if provided
            if (!string.IsNullOrEmpty(title))
            {
                using var titlePaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 24,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                };
                canvas.DrawText(title, 20, 30, titlePaint);
            }

            // Draw edges first (so nodes appear on top)
            using var edgePaint = new SKPaint
            {
                IsAntialias = true,
                StrokeWidth = edgeWidth,
                Style = SKPaintStyle.Stroke
            };

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    int weight = adjacency[i, j];
                    if (weight == 0) continue;

                    if (!positions.ContainsKey(i) || !positions.ContainsKey(j)) continue;

                    var pos1 = positions[i];
                    var pos2 = positions[j];

                    // Color edge based on whether nodes share same community
                    if (communityLabels[i] == communityLabels[j])
                    {
                        edgePaint.Color = GetCommunityColor(communityLabels[i]).WithAlpha(150);
                    }
                    else
                    {
                        edgePaint.Color = SKColors.LightGray.WithAlpha(100);
                    }

                    // Make edge thickness proportional to weight if weights vary significantly
                    edgePaint.StrokeWidth = edgeWidth * (float)Math.Log(weight + 1);

                    canvas.DrawLine(pos1.X, pos1.Y, pos2.X, pos2.Y, edgePaint);

                    // Optionally draw edge weights
                    if (drawEdgeWeights && weight > 1)
                    {
                        float midX = (pos1.X + pos2.X) / 2;
                        float midY = (pos1.Y + pos2.Y) / 2;
                        
                        using var textPaint = new SKPaint
                        {
                            Color = SKColors.DarkGray,
                            TextSize = 10,
                            IsAntialias = true
                        };
                        canvas.DrawText(weight.ToString(), midX, midY, textPaint);
                    }
                }
            }

            // Draw nodes
            using var nodePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var nodeBorderPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                Color = SKColors.Black
            };

            using var labelPaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 12,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            for (int i = 0; i < n; i++)
            {
                if (!positions.ContainsKey(i)) continue;

                var pos = positions[i];
                int community = communityLabels[i];
                
                nodePaint.Color = GetCommunityColor(community);
                canvas.DrawCircle(pos.X, pos.Y, nodeSize, nodePaint);
                canvas.DrawCircle(pos.X, pos.Y, nodeSize, nodeBorderPaint);

                // Draw node label
                canvas.DrawText(i.ToString(), pos.X, pos.Y + 4, labelPaint);
            }

            // Draw legend
            DrawCommunityLegend(canvas, communityLabels, imageWidth, imageHeight);

            // Save image
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(outputPath);
            data.SaveTo(stream);
        }

        /// <summary>
        /// Draw a legend showing community colors.
        /// </summary>
        private static void DrawCommunityLegend(SKCanvas canvas, int[] communityLabels, int imageWidth, int imageHeight)
        {
            var communities = communityLabels.Distinct().OrderBy(c => c).ToList();
            if (communities.Count == 0) return;

            float legendX = imageWidth - 150;
            float legendY = 60;
            float lineHeight = 25;

            using var legendPaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 14,
                IsAntialias = true
            };

            using var boxPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            using var borderPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };

            // Draw legend background
            float legendHeight = communities.Count * lineHeight + 20;
            canvas.DrawRect(legendX - 10, legendY - 20, 140, legendHeight, 
                new SKPaint { Color = SKColors.White.WithAlpha(230), Style = SKPaintStyle.Fill });
            canvas.DrawRect(legendX - 10, legendY - 20, 140, legendHeight, borderPaint);

            canvas.DrawText("Communities", legendX + 10, legendY - 5, 
                new SKPaint { Color = SKColors.Black, TextSize = 14, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) });

            for (int i = 0; i < communities.Count; i++)
            {
                int comm = communities[i];
                float y = legendY + i * lineHeight + 15;

                boxPaint.Color = GetCommunityColor(comm);
                canvas.DrawCircle(legendX + 5, y, 8, boxPaint);
                canvas.DrawCircle(legendX + 5, y, 8, borderPaint);

                canvas.DrawText($"Community {comm}", legendX + 20, y + 5, legendPaint);
            }
        }

        /// <summary>
        /// Plot network with automatic layout computation (force-directed).
        /// </summary>
        public static void PlotNetworkAuto(
            DokSparseMatrix<int> adjacency,
            int[] communityLabels,
            string outputPath,
            string? positionsPath = null,
            string? title = null,
            int seed = 42)
        {
            Dictionary<int, NodePosition> positions;

            // Load or compute positions
            if (!string.IsNullOrEmpty(positionsPath) && File.Exists(positionsPath))
            {
                positions = LoadPositions(positionsPath);
            }
            else
            {
                positions = ComputeForceDirectedLayout(adjacency, seed: seed);
                
                if (!string.IsNullOrEmpty(positionsPath))
                {
                    SavePositions(positions, positionsPath);
                }
            }

            PlotNetwork(adjacency, positions, communityLabels, outputPath, title: title);
        }

        /// <summary>
        /// Plot network with circular layout.
        /// </summary>
        public static void PlotNetworkCircular(
            DokSparseMatrix<int> adjacency,
            int[] communityLabels,
            string outputPath,
            string? title = null)
        {
            var positions = ComputeCircularLayout(adjacency.Rows);
            PlotNetwork(adjacency, positions, communityLabels, outputPath, title: title);
        }
    }
}
