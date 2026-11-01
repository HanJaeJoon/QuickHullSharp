## QuickHullSharp

[![Build](https://github.com/HanJaeJoon/QuickHullSharp/actions/workflows/nuget-publish.yml/badge.svg)](https://github.com/HanJaeJoon/QuickHullSharp/actions/workflows/nuget-publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

QuickHullSharp computes the 3D convex hull of a point cloud with the QuickHull algorithm.
It is a pure C# port of [akuukka/quickhull](https://github.com/akuukka/quickhull) by Antti Kuukka,
which is public domain. The port is released under the MIT license.

Targets `netstandard2.0`, `netstandard2.1`, `net8.0`, and `net10.0`, so it can be used from .NET Framework 4.6.2+, .NET Core 2.0+, .NET 5+, Unity 2021.2+, and Mono.

## Installation

``` bash
dotnet add package QuickHullSharp
```

## Usage

``` c#
using System.Numerics;
using QuickHullSharp;

Vector3[] points = /* your point cloud */;

ConvexHull<Vector3> hull = QuickHull.ComputeHull(points);
IReadOnlyList<Vector3> vertices = hull.Vertices; // only the hull vertices
IReadOnlyList<int> indices = hull.Indices;       // three indices per triangle
```

- `ccw` (default `true`): whether the triangles are counter-clockwise seen from outside the hull in a
  left-handed coordinate system, as upstream defines it. Pass `false` to get triangles whose
  right-hand-rule normal `(b - a) x (c - a)` points out of the hull.
- `useOriginalIndices` (default `false`): when `true`, the indices refer to the input points and
  `Vertices` is a copy of them; otherwise `Vertices` holds only the points on the hull.
- `epsilon`: minimum distance to a face plane for a point to count as outside it, relative to the
  scale of the point cloud (default `1e-4` for `Vector3` input, `1e-7` for double input).

Degenerate input (fewer than four points, or coincident, collinear or coplanar points) still
yields a closed triangle mesh, which is then flat.

### Double-precision input

Coordinates can also be given as consecutive (x, y, z) triples of `double`. The vertices are then
returned as `Vector3D`. The hull is always computed in double precision.

``` c#
double[] coordinates = [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1];
ConvexHull<Vector3D> hull = QuickHull.ComputeHull(coordinates);
```

### Half edge mesh

`ComputeHullAsMesh` returns the hull as a half edge mesh, which gives quick access to adjacent faces:

``` c#
HalfEdgeMesh<Vector3> mesh = QuickHull.ComputeHullAsMesh(points);
foreach (Face face in mesh.Faces)
{
    HalfEdge edge = mesh.HalfEdges[face.HalfEdgeIndex];
    int neighborFace = mesh.HalfEdges[edge.Opp].Face;
}
```

## Credits

Algorithm and original implementation: [akuukka/quickhull](https://github.com/akuukka/quickhull)
(public domain).
