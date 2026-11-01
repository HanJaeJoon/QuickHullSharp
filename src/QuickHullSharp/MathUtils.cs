namespace QuickHullSharp;

internal static class MathUtils
{
    public static double Dot(Vector3D a, Vector3D b)
    {
        return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
    }

    public static Vector3D Subtract(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    public static Vector3D Add(Vector3D a, Vector3D b)
    {
        return new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    public static bool AreEqual(Vector3D a, Vector3D b)
    {
        return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }

    public static double GetSquaredDistance(Vector3D a, Vector3D b)
    {
        Vector3D d = Subtract(a, b);
        return Dot(d, d);
    }

    public static double GetSquaredDistanceBetweenPointAndRay(Vector3D p, Ray r)
    {
        Vector3D s = Subtract(p, r.S);
        double t = Dot(s, r.V);
        return Dot(s, s) - (t * t * r.VInvLengthSquared);
    }

    // The unit of distance returned is relative to the length of the plane's normal.
    public static double GetSignedDistanceToPlane(Vector3D v, Plane p)
    {
        return Dot(p.N, v) + p.D;
    }

    // (a - c) x (b - c)
    public static Vector3D GetTriangleNormal(Vector3D a, Vector3D b, Vector3D c)
    {
        double x = a.X - c.X;
        double y = a.Y - c.Y;
        double z = a.Z - c.Z;
        double rhsx = b.X - c.X;
        double rhsy = b.Y - c.Y;
        double rhsz = b.Z - c.Z;
        double px = (y * rhsz) - (z * rhsy);
        double py = (z * rhsx) - (x * rhsz);
        double pz = (x * rhsy) - (y * rhsx);
        return new Vector3D(px, py, pz);
    }
}
