namespace QuickHullSharp;

/// <summary>
/// A convex hull as a triangle mesh: a vertex buffer and an index buffer.
/// </summary>
/// <typeparam name="TVertex">The vertex type, the same as the input points.</typeparam>
public sealed class ConvexHull<TVertex>
{
    // Construct vertex and index buffers from half edge mesh and point cloud
    internal ConvexHull(MeshBuilder mesh, ReadOnlySpan<TVertex> pointCloud, bool ccw, bool useOriginalIndices, int failedHorizonEdges)
    {
        FailedHorizonEdges = failedHorizonEdges;

        var optimizedVertexBuffer = new List<TVertex>();
        var indices = new List<int>();
        bool[] faceProcessed = new bool[mesh.Faces.Count];
        var faceStack = new List<int>();

        // Map vertex indices from original point cloud to the new mesh vertex indices
        var vertexIndexMapping = new Dictionary<int, int>();
        for (int i = 0; i < mesh.Faces.Count; i++)
        {
            if (!mesh.Faces[i].IsDisabled)
            {
                faceStack.Add(i);
                break;
            }
        }

        if (faceStack.Count == 0)
        {
            Vertices = [];
            Indices = [];
            return;
        }

        int iCcw = ccw ? 1 : 0;
        indices.Capacity = (mesh.Faces.Count - mesh.DisabledFaces.Count) * 3;

        while (faceStack.Count > 0)
        {
            int top = faceStack[faceStack.Count - 1];
            faceStack.RemoveAt(faceStack.Count - 1);
            if (faceProcessed[top])
            {
                continue;
            }

            faceProcessed[top] = true;
            int[] halfEdges = mesh.GetHalfEdgeIndicesOfFace(mesh.Faces[top]);
            foreach (int he in halfEdges)
            {
                int a = mesh.HalfEdges[mesh.HalfEdges[he].Opp].Face;
                if (!faceProcessed[a] && !mesh.Faces[a].IsDisabled)
                {
                    faceStack.Add(a);
                }
            }

            int[] vertices = mesh.GetVertexIndicesOfFace(mesh.Faces[top]);
            if (!useOriginalIndices)
            {
                for (int k = 0; k < 3; k++)
                {
                    if (!vertexIndexMapping.TryGetValue(vertices[k], out int mapped))
                    {
                        optimizedVertexBuffer.Add(pointCloud[vertices[k]]);
                        mapped = optimizedVertexBuffer.Count - 1;
                        vertexIndexMapping[vertices[k]] = mapped;
                    }

                    vertices[k] = mapped;
                }
            }

            indices.Add(vertices[0]);
            indices.Add(vertices[1 + iCcw]);
            indices.Add(vertices[2 - iCcw]);
        }

        Vertices = useOriginalIndices ? pointCloud.ToArray() : optimizedVertexBuffer.ToArray();
        Indices = indices.ToArray();
    }

    /// <summary>
    /// The vertex buffer. With <c>useOriginalIndices</c> it is a copy of the input points;
    /// otherwise it holds only the points that are vertices of the hull.
    /// </summary>
    public IReadOnlyList<TVertex> Vertices { get; }

    /// <summary>
    /// The index buffer: three indices into <see cref="Vertices"/> per triangle.
    /// </summary>
    public IReadOnlyList<int> Indices { get; }

    /// <summary>
    /// How many times the algorithm failed to solve the horizon edge. Failures lead to degenerate convex hulls.
    /// </summary>
    public int FailedHorizonEdges { get; }
}
