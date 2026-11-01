namespace QuickHullSharp;

/// <summary>
/// A triangular face of a <see cref="HalfEdgeMesh{TVertex}"/>.
/// </summary>
public readonly struct Face
{
    /// <summary>
    /// Creates a face.
    /// </summary>
    /// <param name="halfEdgeIndex">Index of one of the half edges of this face.</param>
    public Face(int halfEdgeIndex)
    {
        HalfEdgeIndex = halfEdgeIndex;
    }

    /// <summary>Index of one of the half edges of this face.</summary>
    public int HalfEdgeIndex { get; }
}
