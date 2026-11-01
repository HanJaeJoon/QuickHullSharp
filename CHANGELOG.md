# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial C# port of [akuukka/quickhull](https://github.com/akuukka/quickhull):
  `QuickHull.ComputeHull` returns the 3D convex hull of a point cloud as a triangle mesh
  (`ConvexHull<TVertex>` with `Vertices` and `Indices`), with the `ccw`, `useOriginalIndices`
  and `epsilon` options, and `QuickHull.ComputeHullAsMesh` returns it as a half edge mesh
  (`HalfEdgeMesh<TVertex>`). Both take `Vector3` points; `QuickHull.ComputeHullFromCoordinates`
  and `QuickHull.ComputeHullAsMeshFromCoordinates` take double-precision (x, y, z) coordinates
  and return `Vector3D` vertices. The double overloads have their own names so that collection
  expressions such as `QuickHull.ComputeHull([new(0, 0, 0), ...])` and `QuickHull.ComputeHull([])`
  are not ambiguous.
- `epsilon` must be finite and non-negative: other values throw `ArgumentOutOfRangeException`
  instead of failing with `IndexOutOfRangeException` (NaN) or returning a hull that does not
  contain the points (negative or infinite).
