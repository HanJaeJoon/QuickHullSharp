using System.Numerics;

namespace QuickHullSharp.Tests;

public class DoublePrecisionTests
{
    [Fact]
    public void Should_MatchVectorOverload_When_CoordinatesAreExactlyRepresentable()
    {
        var points = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            points.Add(new Vector3((i & 1) != 0 ? -1 : 1, (i & 2) != 0 ? -1 : 1, (i & 4) != 0 ? -1 : 1));
        }

        points.Add(new Vector3(0.5f, 0.25f, -0.5f));
        double[] coordinates = points.SelectMany(p => new double[] { p.X, p.Y, p.Z }).ToArray();

        ConvexHull<Vector3> hull = QuickHull.ComputeHull(points.ToArray());
        ConvexHull<Vector3D> doubleHull = QuickHull.ComputeHullFromCoordinates(coordinates);

        Assert.Equal(hull.Indices, doubleHull.Indices);
        Assert.Equal(hull.Vertices.Select(p => new Vector3D(p.X, p.Y, p.Z)), doubleHull.Vertices);
    }

    [Fact]
    public void Should_Throw_When_InputIsInvalid()
    {
        Assert.Throws<ArgumentException>(() => QuickHull.ComputeHullFromCoordinates(new double[] { 0, 0 }));
        Assert.Throws<ArgumentException>(() => QuickHull.ComputeHullFromCoordinates(new double[] { 0, 0, double.NaN }));
        Assert.Throws<ArgumentException>(() => QuickHull.ComputeHull(new[] { new Vector3(float.PositiveInfinity, 0, 0) }));
    }

    [Fact]
    public void Should_BindToVectorOverload_When_CollectionExpressionIsUsed()
    {
        // The double overloads have their own names so that target-typed collection expressions are not ambiguous (CS0121).
        ConvexHull<Vector3> hull = QuickHull.ComputeHull([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)]);
        HalfEdgeMesh<Vector3> mesh = QuickHull.ComputeHullAsMesh([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)]);
        ConvexHull<Vector3> empty = QuickHull.ComputeHull([]);

        Assert.Equal(4, hull.Vertices.Count);
        Assert.Equal(4, mesh.Faces.Count);
        Assert.Empty(empty.Indices);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1e-4)]
    [InlineData(double.PositiveInfinity)]
    public void Should_Throw_When_EpsilonIsNegativeOrNotFinite(double epsilon)
    {
        Vector3[] points = [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0.1f, 0.1f, 0.1f)];
        double[] coordinates = points.SelectMany(p => new double[] { p.X, p.Y, p.Z }).ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => QuickHull.ComputeHull(points, epsilon: (float)epsilon));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuickHull.ComputeHullFromCoordinates(coordinates, epsilon: epsilon));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuickHull.ComputeHullAsMesh(points, (float)epsilon));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuickHull.ComputeHullAsMeshFromCoordinates(coordinates, epsilon));
    }
}
