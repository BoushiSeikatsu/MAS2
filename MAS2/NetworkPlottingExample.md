# Network Plotting Usage Guide

## Overview

The `NetworkPlotting` class provides methods to visualize networks with:
- **Consistent node positions** across different network visualizations
- **Community-based coloring** for nodes and edges
- Support for both force-directed and circular layouts

## Key Features

### 1. Consistent Node Positions
Node positions are computed once and can be saved/loaded from a CSV file. This ensures that node with ID=1 always appears in the same location across all visualizations.

### 2. Community-Based Coloring
- Nodes are colored based on their community membership
- Edges connecting nodes in the same community are colored with that community's color
- Edges connecting nodes in different communities are shown in gray

## Usage in Cviko7

The plotting code has been integrated into `Cviko7.cs` and will automatically generate the following visualizations:

### Generated Images

1. **network_selected_layers.png** - Flattened network from selected layers
2. **network_all_layers.png** - Flattened network from all layers
3. **network_circular.png** - Circular layout of selected layers
4. **network_top2_shared.png** - Top 2 layers with most shared edges

### Generated Position File

- **network_positions.csv** - Node positions (used for consistency across plots)

## Manual Usage Examples

### Example 1: Plot with Automatic Layout
```csharp
// Adjacency matrix and community labels
DokSparseMatrix<int> adjacency = /* your network */;
int[] communityLabels = /* your community detection results */;

// Plot with automatic force-directed layout
NetworkPlotting.PlotNetworkAuto(
    adjacency,
    communityLabels,
    "output.png",
    positionsPath: "positions.csv",  // Save positions for reuse
    title: "My Network",
    seed: 42  // Fixed seed ensures reproducibility
);
```

### Example 2: Plot with Circular Layout
```csharp
NetworkPlotting.PlotNetworkCircular(
    adjacency,
    communityLabels,
    "circular_output.png",
    title: "Network - Circular Layout"
);
```

### Example 3: Compute Layout Separately
```csharp
// Compute positions once
var positions = NetworkPlotting.ComputeForceDirectedLayout(
    adjacency,
    iterations: 500,
    seed: 42
);

// Save positions for later use
NetworkPlotting.SavePositions(positions, "my_positions.csv");

// Plot using these positions
NetworkPlotting.PlotNetwork(
    adjacency,
    positions,
    communityLabels,
    "output.png",
    title: "My Network"
);
```

### Example 4: Reuse Saved Positions
```csharp
// Load previously computed positions
var positions = NetworkPlotting.LoadPositions("my_positions.csv");

// Plot different networks using the same positions
NetworkPlotting.PlotNetwork(
    adjacency1,
    positions,
    communityLabels1,
    "network1.png",
    title: "Network 1"
);

NetworkPlotting.PlotNetwork(
    adjacency2,
    positions,
    communityLabels2,
    "network2.png",
    title: "Network 2"
);
```

### Example 5: Custom Layout Parameters
```csharp
var positions = NetworkPlotting.ComputeForceDirectedLayout(
    adjacency,
    iterations: 1000,        // More iterations for better layout
    seed: 42,                // Fixed seed for reproducibility
    width: 2000f,            // Larger canvas
    height: 2000f,
    repulsion: 8000f,        // Stronger repulsion between nodes
    attraction: 0.005f,      // Weaker attraction along edges
    damping: 0.9f            // Higher damping for smoother convergence
);
```

## Layout Algorithms

### Force-Directed Layout
- Uses repulsive forces between all node pairs
- Uses attractive forces along edges
- Edge weights affect attraction strength
- Deterministic (same seed = same layout)
- Best for showing network structure

### Circular Layout
- Arranges nodes in a circle
- Node ID determines position on circle
- Very consistent and predictable
- Good for small networks or regular structures

## Customization Options

### PlotNetwork Parameters
```csharp
NetworkPlotting.PlotNetwork(
    adjacency,
    positions,
    communityLabels,
    outputPath: "network.png",
    imageWidth: 1200,         // Image size in pixels
    imageHeight: 1200,
    nodeSize: 15f,            // Radius of node circles
    edgeWidth: 1.5f,          // Base edge width (scaled by weight)
    drawEdgeWeights: false,   // Show weight numbers on edges
    title: "My Network"       // Title at top of image
);
```

## Tips for Best Results

1. **Use the same seed** for force-directed layout to get reproducible results
2. **Save positions** to a file when comparing multiple networks
3. **Adjust repulsion/attraction** if nodes are too clustered or too spread out
4. **Use circular layout** for quick visualization of small networks
5. **Increase iterations** (to 1000+) for complex networks with many nodes

## Color Palette

The default community colors are:
- Community 0: Red
- Community 1: Blue
- Community 2: Green
- Community 3: Orange
- Community 4: Purple
- Community 5: Turquoise
- ... (cycles through 15 distinct colors)

## Output Files from Cviko7

When you run `Cviko7`, it will generate:
- Network visualizations (PNG images)
- Community assignments (CSV)
- Modularity scores (TXT)
- Node positions (CSV)
- Edge lists (CSV)

All files are saved in the working directory (bin/Debug/net8.0/).
