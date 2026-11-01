using System.Numerics;

namespace QuickHullSharp.Tests;

public class CrossCheckTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_ContainEveryPointAndSatisfyEuler_When_CloudIsRandom(int seed)
    {
        var random = new Random(seed);
        var points = new Vector3[500];
        double[] coordinates = new double[points.Length * 3];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector3((float)((random.NextDouble() * 2) - 1), (float)((random.NextDouble() * 2) - 1), (float)((random.NextDouble() * 2) - 1));
            coordinates[3 * i] = points[i].X;
            coordinates[(3 * i) + 1] = points[i].Y;
            coordinates[(3 * i) + 2] = points[i].Z;
        }

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(points, ccw: false);
        AssertContains(points.Select(p => new Vector3D(p.X, p.Y, p.Z)).ToArray(), hull.Vertices.Select(p => new Vector3D(p.X, p.Y, p.Z)).ToArray(), hull.Indices, 1e-4);
        AssertEuler(QuickHull.ComputeHullAsMesh(points));

        ConvexHull<Vector3D> doubleHull = QuickHull.ComputeHull(coordinates, ccw: false);
        AssertContains(points.Select(p => new Vector3D(p.X, p.Y, p.Z)).ToArray(), doubleHull.Vertices.ToArray(), doubleHull.Indices, 1e-7);
        AssertEuler(QuickHull.ComputeHullAsMesh(coordinates));
    }

    // Every input point must be on or inside every face plane within epsilon. With ccw false the right-hand normals point outward.
    private static void AssertContains(Vector3D[] points, Vector3D[] vertices, IReadOnlyList<int> indices, double epsilon)
    {
        Assert.NotEmpty(indices);
        for (int t = 0; t < indices.Count; t += 3)
        {
            Vector3D a = vertices[indices[t]];
            Vector3D b = vertices[indices[t + 1]];
            Vector3D c = vertices[indices[t + 2]];
            Vector3D n = Cross(Sub(b, a), Sub(c, a));
            double length = Math.Sqrt(Dot(n, n));
            foreach (Vector3D p in points)
            {
                double dist = Dot(n, Sub(p, a)) / length;
                Assert.True(dist <= epsilon, $"t={t} dist={dist} n={n.X},{n.Y},{n.Z}");
            }
        }
    }

    private static void AssertEuler<T>(HalfEdgeMesh<T> mesh)
    {
        int v = mesh.Vertices.Count;
        int e = mesh.HalfEdges.Count / 2;
        int f = mesh.Faces.Count;
        Assert.Equal(2, v - e + f);
    }

    private static Vector3D Sub(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    private static double Dot(Vector3D a, Vector3D b)
    {
        return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
    }

    private static Vector3D Cross(Vector3D a, Vector3D b)
    {
        return new Vector3D((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));
    }
}
