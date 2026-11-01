# QuickHullSharp round 1 dogfooding findings

Target: `QuickHullSharp 0.1.0` from this branch, consumed through a `ProjectReference` by a net8.0
console app (`%TEMP%\dogfood-QuickHullSharp`, `dotnet run -c Release -- 1..9`).
Library source was not opened while writing the scenarios - only the README and the generated XML
docs. Public surface was read through reflection. Source was opened afterwards to fix the bug and
attribute the findings.

Machine: 12 logical cores, .NET SDK 10.0.402, app runs on net8.0, Release build.

## Scenarios

- [x] 1. 3D point cloud hull (ball, sphere, cube surface grid), cross-checked against brute force
- [x] 2. Hull as a render mesh (flat and smooth normals, `ccw`, `useOriginalIndices`, volume)
- [x] 3. Degenerate input (0-3 points, duplicates, collinear, coplanar, lattices, near-planar)
- [x] 4. Extremes (large offsets, tiny/huge scales, non-finite input, `epsilon` range)
- [x] 5. Performance and allocation at 100k and 1M points
- [x] 6. Public surface audit
- [x] 7. Threading and determinism
- [x] 8. Half edge mesh consistency
- [x] 9. (follow-up) `Vector3` vs double path at equal `epsilon`

Every hull is checked by the same validator: indices in range, every directed edge paired with
its reverse exactly once, Euler `V - E + F = 2`, zero-area triangles, orientation against the
centroid, max distance of any input point outside any face (relative to scale), hull vertices
are input points, and signed volume.

---

# Verdict: 0.1.0 can ship. One bug was found and fixed; no P0 remains.

The hull itself is solid: every non-degenerate scenario produced a closed, consistently oriented,
containing hull with zero failed horizon edges, the vertex set matched brute force exactly, and
the two input paths, the two output forms and the parallel runs all agree. What surfaced is
input validation (fixed), two documentation claims that did not match the output (fixed), and
one overload-resolution friction that is cheapest to settle before the first release (P1).

## Bug found (fixed in this round)

### 1. [BUG] `epsilon` was not validated

| `epsilon` | before | after |
|---|---|---|
| `NaN` | `IndexOutOfRangeException` from inside the builder | `ArgumentOutOfRangeException` |
| `-1` | returns a 4-vertex hull, points up to 1.0 (100% of scale) outside it | `ArgumentOutOfRangeException` |
| `+Infinity` | returns a 4-vertex hull, points up to 1.2x scale outside it | `ArgumentOutOfRangeException` |
| `0`, `1e-9`, `1e-2`, `0.5` | valid hull | unchanged |

Points were already checked for finiteness, `epsilon` was not. Upstream has no check either. Fixed
in `QuickHullBuilder`'s constructor, which all four entry points go through; test
`Should_Throw_When_EpsilonIsNegativeOrNotFinite` covers all four.

## Documentation corrected in this round

- **`epsilon` scale.** "Relative to the scale of the point cloud" did not say what the scale is.
  It is the largest absolute coordinate (upstream behavior), not the extent, so translating a
  cloud away from the origin coarsens the hull. Unit ball, 2000 points, default epsilon:

  | offset | `Vector3` hull verts | double hull verts | double volume |
  |---|---|---|---|
  | 0 | 199 | 201 | 3.7780 |
  | 1e3 | 39 | 199 | 3.7780 |
  | 1e5 | 4 | 161 | 3.7732 |
  | 1e6 | 4 | 39 | 3.4013 |
  | 1e8 | 4 | 4 | - |

  The double path holds up until `1e-7 * offset` approaches the cloud radius; the `Vector3` path
  is also limited by float precision itself. Pure scaling (1e-6 to 1e15) does not change the hull:
  volume / scale^3 = 3.7780 throughout. README and XML now say what the scale is and to translate
  the cloud near the origin.
- **Degenerate output is not a clean manifold by index.** README said degenerate input yields
  "a closed triangle mesh, which is then flat". The half edge mesh is closed, but the index buffer
  repeats vertices: 3 points give 4 triangles over 3 vertices (one vertex used twice per
  tetrahedron), and an exactly planar 20x20 grid gives 6 triangles over 4 vertices, 2 of them
  zero-area (upstream replaces its temporary apex point with vertex 0). Harmless for rendering,
  but a consumer validating edge pairing will see it. Docs now say so.

## What was verified as working

**Point cloud hull (1).** Ball 2000 (170 hull verts), sphere 2000 (1998), cube surface grid 9^3
(8 verts, 12 tris): closed, Euler 2, 0 wrongly oriented, max outside 5.85e-5 of scale (below
epsilon 1e-4), 0 failed horizon edges. A 40-point cloud's hull vertex set equals the brute-force
set over all triples (26 = 26). The double path gives the same 26 verts / 48 tris.

**Render mesh (2).** 5000-point ball, radius 3: 0/636 flat normals and 0/320 averaged vertex normals
point inward with `ccw: false`; 0 unused vertex buffer entries. `useOriginalIndices` yields the
identical oriented triangle set, `Vertices` is a 5000-entry copy (not the caller's array). Cube
`[-1,1]^3` with 500 interior points: exactly 8 verts, 12 tris, volume 8.000000; with the default
`ccw: true` the right-hand signed volume is -8, matching the documented left-handed convention.

**Degenerate input (3).** No exception for any case. Duplicated cube corners (x50), cube surface
grids 20 and 60, a 10^3 integer lattice: exactly 8 verts / 12 tris, 0 failed horizon edges.
Identical points, collinear points: 4-vertex flat hull. Near-planar clouds (tilted plane in float,
1e-7 noise): closed, max outside 4.4e-6. A 64x10 cylinder of rings: 152 verts, closed.

**Invalid input (4).** NaN/Infinity point -> `ArgumentException` naming the index; coordinate count
not a multiple of 3 -> `ArgumentException`, for both `ComputeHull` and `ComputeHullAsMesh`.
`float.MaxValue` coordinates do not throw.

**Performance (5)**, best of 3, default epsilon:

| input | n | `Vector3` | double | `AsMesh` | `useOriginalIndices` | hull verts |
|---|---|---|---|---|---|---|
| ball | 100k | 10.0 ms, 7.3 MB | 9.9 ms, 7.4 MB | 9.4 ms, 8.0 MB | 9.4 ms, 8.3 MB | 1383 |
| ball | 1M | 149 ms, 54 MB | 153 ms, 55 MB | 173 ms, 57 MB | 178 ms, 66 MB | 4298 |
| sphere surface | 100k | 168 ms, 80 MB | 406 ms, 183 MB | 169 ms, 102 MB | 151 ms, 76 MB | 39696 |
| sphere surface | 1M | 1009 ms, 234 MB | 5719 ms, 1799 MB | 1468 ms, 260 MB | 1412 ms, 241 MB | 46303 |
| cube surface grid | 135k | 2.9 ms, 5.3 MB | 2.8 ms, 5.3 MB | 2.8 ms, 5.4 MB | 3.9 ms, 7.0 MB | 8 |

A sphere surface 100k hull (79084 tris) validated clean: closed, Euler 2, 0 wrongly oriented.
The double path's slowdown on spheres is entirely its tighter default epsilon (scenario 9): at the
same epsilon both paths take the same time and allocation, e.g. sphere 1M at `1e-7` keeps 998952
hull verts in 9.4 s / 1765 MB (`Vector3`) vs 8.9 s / 1802 MB (double).

**Surface (6).** 6 public types: `QuickHull` (static, 4 methods), `ConvexHull<T>` and
`HalfEdgeMesh<T>` (sealed, get-only, internal constructors), and readonly structs `Vector3D`,
`HalfEdge`, `Face`. Every public member has an XML doc. Runtime collections are arrays behind
`IReadOnlyList<T>`.

**Threading (7).** 64 clouds x 20k points: 150 ms sequential, 40.8 ms with `Parallel.For`,
0 mismatches. Same input twice gives identical vertices and indices.

**Half edge mesh (8).** For ball 3000, cube grid, coplanar grid and 3 points: every `Opp` is
mutual, every `Next` cycle has length 3 on one face, every `Opp` ends at the edge's start vertex,
every `Face.HalfEdgeIndex` points back to its face; on non-flat hulls the half edge winding gives
right-hand outward normals (420/420). Face and vertex counts equal `ComputeHull`'s.

## Recommended changes (not done)

### P0 - blocks 0.1.0

None.

### P1 - decide before 0.1.0 (renames are breaking afterwards)

- **Collection expressions do not compile against `ComputeHull`.**
  `QuickHull.ComputeHull([new(0, 0, 0), new(1, 0, 0), ...])` and `QuickHull.ComputeHull([])` fail
  with CS0121: the `ReadOnlySpan<Vector3>` and `ReadOnlySpan<double>` overloads are equally good
  targets. Explicitly typed elements (`[new Vector3(...)]`) or a typed array work. Removing the
  ambiguity means renaming the double overloads (for example `ComputeHullFromCoordinates`) - a
  public API change, so not done without approval.

### P2 - polish, any time before 1.0

- **Allocation grows with hull size: about 1.8-2.0 KB per hull vertex** (sphere 1M at `1e-7`:
  1.8 GB for one call; sphere 100k: 80 MB, 66x the input size). Ordinary clouds are fine (ball:
  4.5-6x the input). Likely sources, not measured separately: the `int[]` that
  `MeshBuilder.GetHalfEdgeIndicesOfFace` / `GetVertexIndicesOfFace` / `GetVertexIndicesOfHalfEdge`
  allocate per call where upstream returns `std::array`, class-based internal faces and half
  edges, and the output `Dictionary`. Internal only, no API change.
- **`Face` and `HalfEdge` have public constructors** that no consumer needs (the library only
  hands them out). Making them internal is a public API removal.
- **`Vector3D`, `HalfEdge` and `Face` do not override `ToString` or implement `IEquatable<T>`.**
  The debugger and logs show `QuickHullSharp.Vector3D`; `Equals` falls back to `ValueType.Equals`.
- **Near-planar clouds within epsilon can come out as a slightly inverted sliver.** Upstream's
  planar path adds a temporary apex and later replaces it with vertex 0; when vertex 0 is not in
  the base plane the result has negative signed volume (float cloud at offset 1e5: 4 verts,
  volume -0.011 with `ccw: false`). The result is within epsilon of flat, so it matters only to
  consumers that rely on orientation of a degenerate hull.
- **The double path's default epsilon (`1e-7`) is 1000x tighter than the `Vector3` default.**
  On dense near-spherical clouds that keeps about 20x more hull vertices and costs 5.7x the time
  (sphere 1M). Worth one README sentence so a consumer switching paths is not surprised.

## Resolved after round 1

- P1: the `ReadOnlySpan<double>` overloads are renamed to `ComputeHullFromCoordinates` and
  `ComputeHullAsMeshFromCoordinates`, so collection expressions with target-typed `new(...)` and
  `[]` bind to the `Vector3` overloads. Test `Should_BindToVectorOverload_When_CollectionExpressionIsUsed`
  keeps it that way.
- P2 (double path default epsilon): README now says the double default (`1e-7`) is 1000x tighter
  than the `Vector3` default (`1e-4`).

The other P2 items (allocation, `Face`/`HalfEdge` constructors, `ToString`/`IEquatable<T>`,
sliver orientation) remain open.

## Assets

`%TEMP%\dogfood-QuickHullSharp\Program.cs` holds scenarios 1-9 (`dotnet run -c Release -- <n>`).
