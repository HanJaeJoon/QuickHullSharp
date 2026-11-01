namespace QuickHullSharp;

internal sealed class MeshBuilder
{
    public const int Disabled = -1;

    public sealed class HalfEdge
    {
        public int EndVertex;
        public int Opp;
        public int Face;
        public int Next;

        public bool IsDisabled => EndVertex == Disabled;

        public void Disable()
        {
            EndVertex = Disabled;
        }
    }

    public sealed class Face
    {
        public int He = Disabled;
        public Plane P;
        public double MostDistantPointDist;
        public int MostDistantPoint;
        public int VisibilityCheckedOnIteration;
        public bool IsVisibleFaceOnCurrentIteration;
        public bool InFaceStack;

        // One bit per half edge of this face, set when that edge belongs to the horizon
        public int HorizonEdgesOnCurrentIteration;
        public List<int>? PointsOnPositiveSide;

        public bool IsDisabled => He == Disabled;

        public void Disable()
        {
            He = Disabled;
        }
    }

    public readonly List<Face> Faces = [];
    public readonly List<HalfEdge> HalfEdges = [];

    // Removed faces and half edges are only marked disabled so that their indices can be reused.
    public readonly List<int> DisabledFaces = [];
    public readonly List<int> DisabledHalfEdges = [];

    public int AddFace()
    {
        if (DisabledFaces.Count > 0)
        {
            int index = DisabledFaces[DisabledFaces.Count - 1];
            Faces[index].MostDistantPointDist = 0;
            DisabledFaces.RemoveAt(DisabledFaces.Count - 1);
            return index;
        }

        Faces.Add(new Face());
        return Faces.Count - 1;
    }

    public int AddHalfEdge()
    {
        if (DisabledHalfEdges.Count > 0)
        {
            int index = DisabledHalfEdges[DisabledHalfEdges.Count - 1];
            DisabledHalfEdges.RemoveAt(DisabledHalfEdges.Count - 1);
            return index;
        }

        HalfEdges.Add(new HalfEdge());
        return HalfEdges.Count - 1;
    }

    // Mark a face as disabled and return the points that were on the positive side of it.
    public List<int>? DisableFace(int faceIndex)
    {
        Face f = Faces[faceIndex];
        f.Disable();
        DisabledFaces.Add(faceIndex);
        List<int>? points = f.PointsOnPositiveSide;
        f.PointsOnPositiveSide = null;
        return points;
    }

    public void DisableHalfEdge(int heIndex)
    {
        HalfEdges[heIndex].Disable();
        DisabledHalfEdges.Add(heIndex);
    }

    // Create a mesh with initial tetrahedron ABCD. Dot product of AB with the normal of triangle ABC should be negative.
    public void Setup(int a, int b, int c, int d)
    {
        Faces.Clear();
        HalfEdges.Clear();
        DisabledFaces.Clear();
        DisabledHalfEdges.Clear();

        AddSetupHalfEdge(b, 6, 0, 1);  // AB
        AddSetupHalfEdge(c, 9, 0, 2);  // BC
        AddSetupHalfEdge(a, 3, 0, 0);  // CA
        AddSetupHalfEdge(c, 2, 1, 4);  // AC
        AddSetupHalfEdge(d, 11, 1, 5); // CD
        AddSetupHalfEdge(a, 7, 1, 3);  // DA
        AddSetupHalfEdge(a, 0, 2, 7);  // BA
        AddSetupHalfEdge(d, 5, 2, 8);  // AD
        AddSetupHalfEdge(b, 10, 2, 6); // DB
        AddSetupHalfEdge(b, 1, 3, 10); // CB
        AddSetupHalfEdge(d, 8, 3, 11); // BD
        AddSetupHalfEdge(c, 4, 3, 9);  // DC

        Faces.Add(new Face { He = 0 }); // ABC
        Faces.Add(new Face { He = 3 }); // ACD
        Faces.Add(new Face { He = 6 }); // BAD
        Faces.Add(new Face { He = 9 }); // CBD
    }

    public int[] GetVertexIndicesOfFace(Face f)
    {
        HalfEdge he = HalfEdges[f.He];
        int v0 = he.EndVertex;
        he = HalfEdges[he.Next];
        int v1 = he.EndVertex;
        he = HalfEdges[he.Next];
        return [v0, v1, he.EndVertex];
    }

    public int[] GetVertexIndicesOfHalfEdge(HalfEdge he)
    {
        return [HalfEdges[he.Opp].EndVertex, he.EndVertex];
    }

    public int[] GetHalfEdgeIndicesOfFace(Face f)
    {
        int next = HalfEdges[f.He].Next;
        return [f.He, next, HalfEdges[next].Next];
    }

    private void AddSetupHalfEdge(int endVertex, int opp, int face, int next)
    {
        HalfEdges.Add(new HalfEdge { EndVertex = endVertex, Opp = opp, Face = face, Next = next });
    }
}
