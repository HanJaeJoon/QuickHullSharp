namespace QuickHullSharp;

/// <summary>
/// A double-precision 3D vector, the vertex type of hulls computed from double-precision input.
/// </summary>
public readonly struct Vector3D
{
    /// <summary>The X component.</summary>
    public readonly double X;

    /// <summary>The Y component.</summary>
    public readonly double Y;

    /// <summary>The Z component.</summary>
    public readonly double Z;

    /// <summary>
    /// Creates a vector from its components.
    /// </summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    /// <param name="z">The Z component.</param>
    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
