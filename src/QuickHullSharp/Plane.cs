namespace QuickHullSharp;

internal readonly struct Plane
{
    public readonly Vector3D N;

    // Signed distance (if normal is of length 1) to the plane from origin
    public readonly double D;

    // Normal length squared
    public readonly double SqrNLength;

    // Construct a plane using normal n and any point p on the plane
    public Plane(Vector3D n, Vector3D p)
    {
        N = n;
        D = -MathUtils.Dot(n, p);
        SqrNLength = (n.X * n.X) + (n.Y * n.Y) + (n.Z * n.Z);
    }

    public bool IsPointOnPositiveSide(Vector3D q)
    {
        return MathUtils.Dot(N, q) + D >= 0;
    }
}
