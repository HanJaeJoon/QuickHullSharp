using System.Numerics;

namespace QuickHullSharp;

/// <summary>
/// Computes the 3D convex hull of a point cloud with the QuickHull algorithm.
/// </summary>
/// <remarks>
/// The hull is always computed in double precision. Degenerate input (fewer than four points,
/// coincident, collinear or coplanar points) still yields a closed but flat triangle mesh, whose triangles
/// can have zero area or repeat a vertex index.
/// The methods are thread-safe.
/// </remarks>
public static class QuickHull
{
    /// <summary>
    /// Computes the convex hull of a point cloud as a triangle mesh.
    /// </summary>
    /// <param name="points">The point cloud. May be empty, which yields an empty hull.</param>
    /// <param name="ccw">
    /// Whether the triangles are counter-clockwise seen from outside the hull in a left-handed coordinate system, as upstream defines it.
    /// With <c>false</c> the right-hand-rule normal of each triangle points out of the hull.
    /// </param>
    /// <param name="useOriginalIndices">
    /// Whether the indices refer to <paramref name="points"/> (and <see cref="ConvexHull{TVertex}.Vertices"/> is a copy of it)
    /// instead of a new vertex buffer holding only the hull vertices.
    /// </param>
    /// <param name="epsilon">
    /// Minimum distance to a plane to consider a point being on the positive side of it, relative to the scale of the point cloud,
    /// which is its largest absolute coordinate: a point cloud far from the origin keeps less detail.
    /// </param>
    /// <returns>The convex hull.</returns>
    /// <exception cref="ArgumentException">A point is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epsilon"/> is negative or not finite.</exception>
    public static ConvexHull<Vector3> ComputeHull(ReadOnlySpan<Vector3> points, bool ccw = true, bool useOriginalIndices = false, float epsilon = 0.0001f)
    {
        var builder = new QuickHullBuilder(ToVector3D(points), epsilon);
        return new ConvexHull<Vector3>(builder.Mesh, points, ccw, useOriginalIndices, builder.FailedHorizonEdges);
    }

    /// <summary>
    /// Computes the convex hull of a point cloud given in double precision as a triangle mesh.
    /// </summary>
    /// <param name="coordinates">The point cloud as consecutive (x, y, z) triples. May be empty, which yields an empty hull.</param>
    /// <param name="ccw">
    /// Whether the triangles are counter-clockwise seen from outside the hull in a left-handed coordinate system, as upstream defines it.
    /// With <c>false</c> the right-hand-rule normal of each triangle points out of the hull.
    /// </param>
    /// <param name="useOriginalIndices">
    /// Whether the indices refer to the input points (and <see cref="ConvexHull{TVertex}.Vertices"/> holds all of them)
    /// instead of a new vertex buffer holding only the hull vertices.
    /// </param>
    /// <param name="epsilon">
    /// Minimum distance to a plane to consider a point being on the positive side of it, relative to the scale of the point cloud,
    /// which is its largest absolute coordinate: a point cloud far from the origin keeps less detail.
    /// </param>
    /// <returns>The convex hull.</returns>
    /// <exception cref="ArgumentException">The length of <paramref name="coordinates"/> is not a multiple of 3, or a coordinate is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epsilon"/> is negative or not finite.</exception>
    public static ConvexHull<Vector3D> ComputeHullFromCoordinates(ReadOnlySpan<double> coordinates, bool ccw = true, bool useOriginalIndices = false, double epsilon = 0.0000001)
    {
        Vector3D[] points = ToVector3D(coordinates);
        var builder = new QuickHullBuilder(points, epsilon);
        return new ConvexHull<Vector3D>(builder.Mesh, points, ccw, useOriginalIndices, builder.FailedHorizonEdges);
    }

    /// <summary>
    /// Computes the convex hull of a point cloud as a half edge mesh.
    /// </summary>
    /// <param name="points">The point cloud. May be empty, which yields an empty mesh.</param>
    /// <param name="epsilon">
    /// Minimum distance to a plane to consider a point being on the positive side of it, relative to the scale of the point cloud,
    /// which is its largest absolute coordinate: a point cloud far from the origin keeps less detail.
    /// </param>
    /// <returns>The convex hull.</returns>
    /// <exception cref="ArgumentException">A point is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epsilon"/> is negative or not finite.</exception>
    public static HalfEdgeMesh<Vector3> ComputeHullAsMesh(ReadOnlySpan<Vector3> points, float epsilon = 0.0001f)
    {
        var builder = new QuickHullBuilder(ToVector3D(points), epsilon);
        return new HalfEdgeMesh<Vector3>(builder.Mesh, points);
    }

    /// <summary>
    /// Computes the convex hull of a point cloud given in double precision as a half edge mesh.
    /// </summary>
    /// <param name="coordinates">The point cloud as consecutive (x, y, z) triples. May be empty, which yields an empty mesh.</param>
    /// <param name="epsilon">
    /// Minimum distance to a plane to consider a point being on the positive side of it, relative to the scale of the point cloud,
    /// which is its largest absolute coordinate: a point cloud far from the origin keeps less detail.
    /// </param>
    /// <returns>The convex hull.</returns>
    /// <exception cref="ArgumentException">The length of <paramref name="coordinates"/> is not a multiple of 3, or a coordinate is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="epsilon"/> is negative or not finite.</exception>
    public static HalfEdgeMesh<Vector3D> ComputeHullAsMeshFromCoordinates(ReadOnlySpan<double> coordinates, double epsilon = 0.0000001)
    {
        Vector3D[] points = ToVector3D(coordinates);
        var builder = new QuickHullBuilder(points, epsilon);
        return new HalfEdgeMesh<Vector3D>(builder.Mesh, points);
    }

    private static Vector3D[] ToVector3D(ReadOnlySpan<Vector3> points)
    {
        var result = new Vector3D[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            result[i] = ToFiniteVector3D(points[i].X, points[i].Y, points[i].Z, i, nameof(points));
        }

        return result;
    }

    private static Vector3D[] ToVector3D(ReadOnlySpan<double> coordinates)
    {
        if (coordinates.Length % 3 != 0)
        {
            throw new ArgumentException("The number of coordinates must be a multiple of 3.", nameof(coordinates));
        }

        var result = new Vector3D[coordinates.Length / 3];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = ToFiniteVector3D(coordinates[3 * i], coordinates[(3 * i) + 1], coordinates[(3 * i) + 2], i, nameof(coordinates));
        }

        return result;
    }

    private static Vector3D ToFiniteVector3D(double x, double y, double z, int index, string paramName)
    {
        if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
        {
            throw new ArgumentException($"Point at index {index} is not finite.", paramName);
        }

        return new Vector3D(x, y, z);
    }

    // double.IsFinite does not exist on netstandard2.0.
    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
