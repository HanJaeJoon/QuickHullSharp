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
  (`HalfEdgeMesh<TVertex>`). Both accept `Vector3` points or double-precision (x, y, z)
  coordinates, which return `Vector3D` vertices.
