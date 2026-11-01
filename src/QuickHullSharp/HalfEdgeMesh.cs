namespace QuickHullSharp;

/// <summary>
/// A convex hull as a half edge mesh, which gives quick access to adjacent faces.
/// </summary>
/// <typeparam name="TVertex">The vertex type, the same as the input points.</typeparam>
public sealed class HalfEdgeMesh<TVertex>
{
    internal HalfEdgeMesh(MeshBuilder builderObject, ReadOnlySpan<TVertex> vertexData)
    {
        var faceMapping = new Dictionary<int, int>();
        var halfEdgeMapping = new Dictionary<int, int>();
        var vertexMapping = new Dictionary<int, int>();
        var vertices = new List<TVertex>();
        var faceHalfEdges = new List<int>();
        var halfEdges = new List<MeshBuilder.HalfEdge>();

        for (int i = 0; i < builderObject.Faces.Count; i++)
        {
            MeshBuilder.Face face = builderObject.Faces[i];
            if (!face.IsDisabled)
            {
                faceHalfEdges.Add(face.He);
                faceMapping[i] = faceHalfEdges.Count - 1;

                foreach (int heIndex in builderObject.GetHalfEdgeIndicesOfFace(face))
                {
                    int vertexIndex = builderObject.HalfEdges[heIndex].EndVertex;
                    if (!vertexMapping.ContainsKey(vertexIndex))
                    {
                        vertices.Add(vertexData[vertexIndex]);
                        vertexMapping[vertexIndex] = vertices.Count - 1;
                    }
                }
            }
        }

        for (int i = 0; i < builderObject.HalfEdges.Count; i++)
        {
            MeshBuilder.HalfEdge halfEdge = builderObject.HalfEdges[i];
            if (!halfEdge.IsDisabled)
            {
                halfEdges.Add(halfEdge);
                halfEdgeMapping[i] = halfEdges.Count - 1;
            }
        }

        var faces = new Face[faceHalfEdges.Count];
        for (int i = 0; i < faces.Length; i++)
        {
            faces[i] = new Face(halfEdgeMapping[faceHalfEdges[i]]);
        }

        var mappedHalfEdges = new HalfEdge[halfEdges.Count];
        for (int i = 0; i < mappedHalfEdges.Length; i++)
        {
            MeshBuilder.HalfEdge he = halfEdges[i];
            mappedHalfEdges[i] = new HalfEdge(vertexMapping[he.EndVertex], halfEdgeMapping[he.Opp], faceMapping[he.Face], halfEdgeMapping[he.Next]);
        }

        Vertices = vertices.ToArray();
        Faces = faces;
        HalfEdges = mappedHalfEdges;
    }

    /// <summary>The vertices of the hull.</summary>
    public IReadOnlyList<TVertex> Vertices { get; }

    /// <summary>The triangular faces of the hull.</summary>
    public IReadOnlyList<Face> Faces { get; }

    /// <summary>The half edges of the hull, three per face.</summary>
    public IReadOnlyList<HalfEdge> HalfEdges { get; }
}
