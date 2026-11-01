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
        ConvexHull<Vector3D> doubleHull = QuickHull.ComputeHull(coordinates);

        Assert.Equal(hull.Indices, doubleHull.Indices);
        Assert.Equal(hull.Vertices.Select(p => new Vector3D(p.X, p.Y, p.Z)), doubleHull.Vertices);
    }

    [Fact]
    public void Should_Throw_When_InputIsInvalid()
    {
        Assert.Throws<ArgumentException>(() => QuickHull.ComputeHull(new double[] { 0, 0 }));
        Assert.Throws<ArgumentException>(() => QuickHull.ComputeHull(new double[] { 0, 0, double.NaN }));
        Assert.Throws<ArgumentException>(() => QuickHull.ComputeHull(new[] { new Vector3(float.PositiveInfinity, 0, 0) }));
    }
}
