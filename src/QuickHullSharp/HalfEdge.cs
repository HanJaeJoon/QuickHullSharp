namespace QuickHullSharp;

/// <summary>
/// A half edge of a <see cref="HalfEdgeMesh{TVertex}"/>. All fields are indices into the mesh.
/// </summary>
public readonly struct HalfEdge
{
    /// <summary>
    /// Creates a half edge.
    /// </summary>
    /// <param name="endVertex">Index of the vertex this half edge points to.</param>
    /// <param name="opp">Index of the opposite half edge.</param>
    /// <param name="face">Index of the face this half edge belongs to.</param>
    /// <param name="next">Index of the next half edge of the same face.</param>
    public HalfEdge(int endVertex, int opp, int face, int next)
    {
        EndVertex = endVertex;
        Opp = opp;
        Face = face;
        Next = next;
    }

    /// <summary>Index of the vertex this half edge points to.</summary>
    public int EndVertex { get; }

    /// <summary>Index of the opposite half edge.</summary>
    public int Opp { get; }

    /// <summary>Index of the face this half edge belongs to.</summary>
    public int Face { get; }

    /// <summary>Index of the next half edge of the same face.</summary>
    public int Next { get; }
}
