using System.Numerics;

namespace QuickHullSharp.Tests;

/// <summary>
/// Port of upstream Tests/QuickHullTests.cpp.
/// </summary>
public class QuickHullTests
{
    private const int N = 200;

    private readonly Random _rng = new(20261101);

    [Fact]
    public void Should_KeepOnlyCubeCorners_When_PointsFillTheCube()
    {
        // Put N points inside unit cube. Result mesh must have exactly 8 vertices because the convex hull is the unit cube.
        List<Vector3> pc = CubeWithInteriorPoints();

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray());

        Assert.Equal(8, hull.Vertices.Count);
        Assert.Equal(3 * 2 * 6, hull.Indices.Count); // 6 faces, 2 triangles/face, 3 indices/tri
    }

    [Fact]
    public void Should_ReturnInputAsVertexBuffer_When_UsingOriginalIndices()
    {
        List<Vector3> pc = CubeWithInteriorPoints();

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray(), true, true);

        Assert.Equal(3 * 2 * 6, hull.Indices.Count);
        Assert.Equal(pc, hull.Vertices);
    }

    [Fact]
    public void Should_KeepEverySpherePoint_When_PointsAreOnASphere()
    {
        List<Vector3> pc = CreateSphere(50, Vector3.Zero);

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray());

        Assert.Equal(pc.Count, hull.Vertices.Count);
    }

    [Fact]
    public void Should_IgnoreDuplicates_When_EveryPointIsAddedTwice()
    {
        List<Vector3> pc = CreateSphere(50, Vector3.Zero);
        pc.AddRange(pc.ToArray());

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray());

        Assert.Equal(pc.Count / 2, hull.Vertices.Count);
    }

    [Fact]
    public void Should_DegenerateToLine_When_SphereIsStretchedAlongX()
    {
        // Multiply x components of the unit sphere vectors by a huge number => essentially we get a line
        List<Vector3> pc = CreateSphere(50, Vector3.Zero);
        pc.AddRange(pc.ToArray());
        const float mul = 2 * 2 * 2;
        while (true)
        {
            for (int i = 0; i < pc.Count; i++)
            {
                pc[i] = new Vector3(pc[i].X * mul, pc[i].Y, pc[i].Z);
            }

            ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray());
            if (hull.Vertices.Count == 4)
            {
                break;
            }
        }
    }

    [Fact]
    public void Should_ReturnDegenerateTetrahedron_When_PointsAreAlmostOnePoint()
    {
        var centerPoint = new Vector3(2, 2, 2);
        var pc = new List<Vector3> { centerPoint };
        for (int i = 0; i < 100; i++)
        {
            pc.Add(centerPoint + new Vector3(Rnd(-0.000001f, 0.000001f), Rnd(-0.000001f, 0.000001f), Rnd(-0.000001f, 0.000001f)));
        }

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray());

        Assert.Equal(12, hull.Indices.Count);
    }

    [Fact]
    public void Should_ReturnFourVertices_When_FourPointsAreCoplanar()
    {
        Vector3[] pc =
        [
            new(-3.000000f, -0.250000f, -0.800000f),
            new(-3.000000f, 0.250000f, -0.800000f),
            new(-3.125000f, 0.250000f, -0.750000f),
            new(-3.125000f, -0.250000f, -0.750000f),
        ];

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc);

        Assert.Equal(12, hull.Indices.Count);
        Assert.Equal(4, hull.Vertices.Count);
    }

    [Fact]
    public void Should_KeepEveryPoint_When_PointsFormAPlanarCircleAndThenACylinder()
    {
        var pc = new List<Vector3>();
        for (int i = 0; i < N; i++)
        {
            float alpha = (float)i / N * 2 * 3.14159f;
            pc.Add(new Vector3((float)Math.Cos(alpha), 0, (float)Math.Sin(alpha)));
        }

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray());
        Assert.Equal(pc.Count, hull.Vertices.Count);

        for (int i = 0; i < N; i++)
        {
            pc.Add(pc[i] + new Vector3(0, 1, 0));
        }

        hull = QuickHull.ComputeHull(pc.ToArray());
        Assert.Equal(pc.Count, hull.Vertices.Count);
        Assert.Equal((pc.Count * 2) - 4, hull.Indices.Count / 3);
    }

    [Fact]
    public void Should_DegenerateToLine_When_TubeRadiusShrinks()
    {
        for (int x = 0; ; x++)
        {
            var pc = new List<Vector3>();
            const float l = 1;
            float r = l / (float)Math.Pow(10, x);
            for (int i = 0; i < N; i++)
            {
                Vector3 p = new Vector3(1, 0, 0) * i * l / (N - 1);
                float a = Rnd(0, 2 * 3.1415f);
                Vector3 d = new Vector3(0, (float)Math.Sin(a), (float)Math.Cos(a)) * r;
                pc.Add(p + d);
            }

            ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray());
            if (hull.Vertices.Count == 4)
            {
                break;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_UseOriginalVertexBuffer_Only_When_Requested(bool useOriginalIndices)
    {
        Vector3[] pc = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc, false, useOriginalIndices);

        Assert.NotEmpty(hull.Vertices);
        if (useOriginalIndices)
        {
            Assert.Equal(pc, hull.Vertices);
        }
    }

    [Fact]
    public void Should_FlipTriangleNormals_When_OrientationIsSwitched()
    {
        Vector3[] pc = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];
        var normal = new Vector3[2];

        for (int i = 0; i < 2; i++)
        {
            bool ccw = i != 0;
            ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc, ccw, false);
            Assert.Equal(3, hull.Vertices.Count);
            Assert.True(hull.Indices.Count >= 6);
            Vector3 a = hull.Vertices[hull.Indices[0]];
            Vector3 b = hull.Vertices[hull.Indices[1]];
            Vector3 c = hull.Vertices[hull.Indices[2]];
            normal[i] = Vector3.Cross(a - c, b - c);
        }

        Assert.Equal(-1, Vector3.Dot(normal[0], normal[1]), 4);
    }

    [Fact]
    public void Should_MeasureSignedDistance_When_PointsAreOnEitherSideOfPlane()
    {
        var p = new Vector3D(2, 0, 0);
        var plane = new Plane(new Vector3D(1, 0, 0), p);

        Assert.True(plane.IsPointOnPositiveSide(p));
        Assert.True(plane.IsPointOnPositiveSide(new Vector3D(3, 0, 0)));
        Assert.False(plane.IsPointOnPositiveSide(new Vector3D(1, 0, 0)));
        Assert.Equal(1, MathUtils.GetSignedDistanceToPlane(new Vector3D(3, 0, 0), plane), 4);
        Assert.Equal(-1, MathUtils.GetSignedDistanceToPlane(new Vector3D(1, 0, 0), plane), 4);

        plane = new Plane(new Vector3D(2, 0, 0), p);
        Assert.Equal(8, MathUtils.GetSignedDistanceToPlane(new Vector3D(6, 0, 0), plane), 4);
    }

    [Fact]
    public void Should_ReturnCubeHalfEdgeMesh_When_CubeCornersEncloseRandomPoints()
    {
        // 8 corner vertices of a cube + tons of vertices inside. Output should be a half edge mesh with 12 faces
        // (6 cube faces with 2 triangles per face) and 36 half edges (3 half edges per face).
        var pc = new List<Vector3>();
        for (int h = 0; h < 1000; h++)
        {
            pc.Add(new Vector3(Rnd(-1, 1), Rnd(-1, 1), Rnd(-1, 1)));
        }

        for (int h = 0; h < 8; h++)
        {
            pc.Add(new Vector3((h & 1) != 0 ? -2 : 2, (h & 2) != 0 ? -2 : 2, (h & 4) != 0 ? -2 : 2));
        }

        HalfEdgeMesh<Vector3> mesh = QuickHull.ComputeHullAsMesh(pc.ToArray());

        Assert.Equal(12, mesh.Faces.Count);
        Assert.Equal(36, mesh.HalfEdges.Count);
        Assert.Equal(8, mesh.Vertices.Count);

        // For each face f, f.HalfEdgeIndex equals next(next(next(f.HalfEdgeIndex))).
        foreach (Face f in mesh.Faces)
        {
            int next = mesh.HalfEdges[f.HalfEdgeIndex].Next;
            next = mesh.HalfEdges[next].Next;
            next = mesh.HalfEdges[next].Next;
            Assert.Equal(f.HalfEdgeIndex, next);
        }
    }

    [Fact]
    public void Should_KeepEverySpherePoint_When_EpsilonIsSmallEnough()
    {
        float y = 1;
        while (true)
        {
            List<Vector3> pc = CreateSphere(100, new Vector3(0, y, 0));
            ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray());
            y *= 15;
            y /= 10;
            if (hull.Vertices.Count == 4)
            {
                break;
            }
        }

        // Test worst case scenario: more and more points on the unit sphere. All points should be part of the convex hull,
        // as long as we can make epsilon smaller without running out of numerical accuracy.
        int i = 1;
        float eps = 0.002f;
        while (i < 100)
        {
            List<Vector3> pc = CreateSphere(i, Vector3.Zero);
            ConvexHull<Vector3> hull = QuickHull.ComputeHull(pc.ToArray(), true, false, eps);
            Assert.Equal(0, hull.FailedHorizonEdges);
            if (pc.Count == hull.Vertices.Count)
            {
                // Fine, all the points on unit sphere do belong to the convex mesh.
                i += 1;
            }
            else
            {
                eps *= 0.5f;
            }
        }
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 12)]
    [InlineData(2, 2, 12)]
    [InlineData(3, 3, 12)]
    public void Should_ReturnDegenerateHull_When_FewerThanFourPoints(int count, int expectedVertices, int expectedIndices)
    {
        Vector3[] all = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)];

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(all.AsSpan(0, count));

        Assert.Equal(expectedVertices, hull.Vertices.Count);
        Assert.Equal(expectedIndices, hull.Indices.Count);
    }

    private float Rnd(float from, float to)
    {
        return from + ((float)_rng.NextDouble() * (to - from));
    }

    private List<Vector3> CubeWithInteriorPoints()
    {
        var pc = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            pc.Add(new Vector3((i & 1) != 0 ? -1 : 1, (i & 2) != 0 ? -1 : 1, (i & 4) != 0 ? -1 : 1));
        }

        for (int i = 0; i < N; i++)
        {
            pc.Add(new Vector3(Rnd(-1, 1), Rnd(-1, 1), Rnd(-1, 1)));
        }

        return pc;
    }

    // Upstream's createSphere: rings of points on the unit sphere, M + 1 rings from pole to pole.
    private static List<Vector3> CreateSphere(int m, Vector3 offset)
    {
        var pc = new List<Vector3>();
        const float pi = 3.14159f;
        for (int i = 0; i <= m; i++)
        {
            float y = (float)Math.Sin((pi / 2) + ((float)i / m * pi));
            float r = (float)Math.Cos((pi / 2) + ((float)i / m * pi));
            float k = 1 - (Math.Abs(i - (m / 2.0f)) / (m / 2.0f));
            int pcount = (int)(1 + (k * m) + (1f / 2));
            for (int j = 0; j < pcount; j++)
            {
                float x = pcount > 1 ? r * (float)Math.Cos((float)j / pcount * pi * 2) : 0;
                float z = pcount > 1 ? r * (float)Math.Sin((float)j / pcount * pi * 2) : 0;
                pc.Add(new Vector3(x + offset.X, y + offset.Y, z + offset.Z));
            }
        }

        return pc;
    }
}
