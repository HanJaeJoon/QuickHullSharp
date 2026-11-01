namespace QuickHullSharp;

internal readonly struct Ray
{
    public readonly Vector3D S;
    public readonly Vector3D V;
    public readonly double VInvLengthSquared;

    public Ray(Vector3D s, Vector3D v)
    {
        S = s;
        V = v;
        VInvLengthSquared = 1 / MathUtils.Dot(v, v);
    }
}
