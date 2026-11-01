namespace QuickHullSharp;

/*
 * Implementation of the 3D QuickHull algorithm, ported from https://github.com/akuukka/quickhull
 * (Antti Kuukka, public domain).
 *
 * SUMMARY OF THE ALGORITHM:
 *  - Create initial simplex (tetrahedron) using extreme points. We have four faces now and they form a convex mesh M.
 *  - For each point, assign them to the first face for which they are on the positive side of (so each point is assigned to at most
 *    one face). Points inside the initial tetrahedron are left behind now and no longer affect the calculations.
 *  - Add all faces that have points assigned to them to Face Stack.
 *  - Iterate until Face Stack is empty:
 *      - Pop topmost face F from the stack
 *      - From the points assigned to F, pick the point P that is farthest away from the plane defined by F.
 *      - Find all faces of M that have P on their positive side. Let us call these the "visible faces".
 *      - Because of the way M is constructed, these faces are connected. Solve their horizon edge loop.
 *      - "Extrude to P": Create new faces by connecting P with the points belonging to the horizon edge. Add the new faces to M and remove the visible
 *        faces from M.
 *      - Each point that was assigned to visible faces is now assigned to at most one of the newly created faces.
 *      - Those new faces that have points assigned to them are added to the top of Face Stack.
 *  - M is now the convex hull.
 */
internal sealed class QuickHullBuilder
{
    private readonly struct FaceData
    {
        public readonly int FaceIndex;

        // If the face turns out not to be visible, this half edge will be marked as horizon edge
        public readonly int EnteredFromHalfEdge;

        public FaceData(int faceIndex, int enteredFromHalfEdge)
        {
            FaceIndex = faceIndex;
            EnteredFromHalfEdge = enteredFromHalfEdge;
        }
    }

    private Vector3D[] _vertexData;
    private double _epsilon;
    private double _epsilonSquared;
    private bool _planar;
    private int[] _extremeValues = [];

    // Temporary variables used during iteration process
    private readonly List<int> _newFaceIndices = [];
    private readonly List<int> _newHalfEdgeIndices = [];
    private readonly List<List<int>> _disabledFacePointVectors = [];
    private readonly List<int> _visibleFaces = [];
    private readonly List<int> _horizonEdges = [];
    private readonly List<FaceData> _possiblyVisibleFaces = [];
    private readonly Queue<int> _faceList = new();

    // Point index lists of removed faces are reused for new faces: most faces have no points
    // on their positive side, and recycling the lists has a remarkable impact on performance.
    private readonly Stack<List<int>> _indexVectorPool = new();

    // Constructs the convex hull of pointCloud into Mesh.
    public QuickHullBuilder(Vector3D[] pointCloud, double epsilon)
    {
        _vertexData = pointCloud;
        if (pointCloud.Length == 0)
        {
            return;
        }

        // Very first: find extreme values and use them to compute the scale of the point cloud.
        _extremeValues = GetExtremeValues();
        double scale = GetScale(_extremeValues);

        // Epsilon we use depends on the scale
        _epsilon = epsilon * scale;
        _epsilonSquared = _epsilon * _epsilon;

        // The planar case happens when all the points appear to lie on a two dimensional subspace of R^3.
        _planar = false;

        CreateConvexHalfEdgeMesh();
        if (_planar)
        {
            int extraPointIndex = _vertexData.Length - 1;
            foreach (MeshBuilder.HalfEdge he in Mesh.HalfEdges)
            {
                if (he.EndVertex == extraPointIndex)
                {
                    he.EndVertex = 0;
                }
            }

            _vertexData = pointCloud;
        }
    }

    public MeshBuilder Mesh { get; } = new();

    // How many times QuickHull failed to solve the horizon edge. Failures lead to degenerate convex hulls.
    public int FailedHorizonEdges { get; private set; }

    private List<int> GetIndexVectorFromPool()
    {
        if (_indexVectorPool.Count == 0)
        {
            return [];
        }

        List<int> r = _indexVectorPool.Pop();
        r.Clear();
        return r;
    }

    private void ReclaimToIndexVectorPool(List<int> list)
    {
        // Huge lists are needed early in the iteration; drop them later to reduce memory usage.
        if ((list.Count + 1) * 128 < list.Capacity)
        {
            return;
        }

        _indexVectorPool.Push(list);
    }

    // Associates a point with a face if the point resides on the positive side of the plane.
    // Returns true if the point was on the positive side.
    private bool AddPointToFace(MeshBuilder.Face f, int pointIndex)
    {
        double d = MathUtils.GetSignedDistanceToPlane(_vertexData[pointIndex], f.P);
        if (d > 0 && d * d > _epsilonSquared * f.P.SqrNLength)
        {
            f.PointsOnPositiveSide ??= GetIndexVectorFromPool();
            f.PointsOnPositiveSide.Add(pointIndex);
            if (d > f.MostDistantPointDist)
            {
                f.MostDistantPointDist = d;
                f.MostDistantPoint = pointIndex;
            }

            return true;
        }

        return false;
    }

    private void CreateConvexHalfEdgeMesh()
    {
        _visibleFaces.Clear();
        _horizonEdges.Clear();
        _possiblyVisibleFaces.Clear();

        // Compute base tetrahedron
        SetupInitialTetrahedron();

        // Init face stack with those faces that have points assigned to them
        _faceList.Clear();
        for (int i = 0; i < 4; i++)
        {
            MeshBuilder.Face f = Mesh.Faces[i];
            if (f.PointsOnPositiveSide is { Count: > 0 })
            {
                _faceList.Enqueue(i);
                f.InFaceStack = true;
            }
        }

        // Process faces until the face list is empty.
        int iter = 0;
        while (_faceList.Count > 0)
        {
            iter++;
            if (iter == int.MaxValue)
            {
                // Visible face traversal marks visited faces with the iteration counter, so reset it before it overflows.
                iter = 0;
            }

            int topFaceIndex = _faceList.Dequeue();

            MeshBuilder.Face tf = Mesh.Faces[topFaceIndex];
            tf.InFaceStack = false;

            if (tf.PointsOnPositiveSide is null || tf.IsDisabled)
            {
                continue;
            }

            // Pick the most distant point to this triangle plane as the point to which we extrude
            Vector3D activePoint = _vertexData[tf.MostDistantPoint];
            int activePointIndex = tf.MostDistantPoint;

            // Find out the faces that have our active point on their positive side (these are the "visible faces").
            // The face on top of the stack of course is one of them. At the same time, we create a list of horizon edges.
            _horizonEdges.Clear();
            _possiblyVisibleFaces.Clear();
            _visibleFaces.Clear();
            _possiblyVisibleFaces.Add(new FaceData(topFaceIndex, MeshBuilder.Disabled));
            while (_possiblyVisibleFaces.Count > 0)
            {
                FaceData faceData = _possiblyVisibleFaces[_possiblyVisibleFaces.Count - 1];
                _possiblyVisibleFaces.RemoveAt(_possiblyVisibleFaces.Count - 1);
                MeshBuilder.Face pvf = Mesh.Faces[faceData.FaceIndex];

                if (pvf.VisibilityCheckedOnIteration == iter)
                {
                    if (pvf.IsVisibleFaceOnCurrentIteration)
                    {
                        continue;
                    }
                }
                else
                {
                    Plane p = pvf.P;
                    pvf.VisibilityCheckedOnIteration = iter;
                    double d = MathUtils.Dot(p.N, activePoint) + p.D;
                    if (d > 0)
                    {
                        pvf.IsVisibleFaceOnCurrentIteration = true;
                        pvf.HorizonEdgesOnCurrentIteration = 0;
                        _visibleFaces.Add(faceData.FaceIndex);
                        foreach (int heIndex in Mesh.GetHalfEdgeIndicesOfFace(pvf))
                        {
                            int opp = Mesh.HalfEdges[heIndex].Opp;
                            if (opp != faceData.EnteredFromHalfEdge)
                            {
                                _possiblyVisibleFaces.Add(new FaceData(Mesh.HalfEdges[opp].Face, heIndex));
                            }
                        }

                        continue;
                    }
                }

                // The face is not visible. Therefore, the halfedge we entered from is part of the horizon edge.
                pvf.IsVisibleFaceOnCurrentIteration = false;
                _horizonEdges.Add(faceData.EnteredFromHalfEdge);

                // Store which half edge is the horizon edge. The other half edges of the face
                // will not be part of the final mesh so their data slots can be recycled.
                MeshBuilder.Face enteredFromFace = Mesh.Faces[Mesh.HalfEdges[faceData.EnteredFromHalfEdge].Face];
                int[] halfEdges = Mesh.GetHalfEdgeIndicesOfFace(enteredFromFace);
                int ind = halfEdges[0] == faceData.EnteredFromHalfEdge ? 0 : (halfEdges[1] == faceData.EnteredFromHalfEdge ? 1 : 2);
                enteredFromFace.HorizonEdgesOnCurrentIteration |= 1 << ind;
            }

            int horizonEdgeCount = _horizonEdges.Count;

            // Order horizon edges so that they form a loop. This may fail due to numerical inaccuracy in which case we give up
            // trying to solve horizon edge for this point and accept a minor degeneration in the convex hull.
            if (!ReorderHorizonEdges(_horizonEdges))
            {
                FailedHorizonEdges++;
                List<int> points = tf.PointsOnPositiveSide;
                points.Remove(activePointIndex);
                if (points.Count == 0)
                {
                    ReclaimToIndexVectorPool(points);
                    tf.PointsOnPositiveSide = null;
                }

                continue;
            }

            // Except for the horizon edges, all half edges of the visible faces can be marked as disabled. Their data slots will be reused.
            // The faces will be disabled as well, but we need to remember the points that were on the positive side of them.
            _newFaceIndices.Clear();
            _newHalfEdgeIndices.Clear();
            _disabledFacePointVectors.Clear();
            int disableCounter = 0;
            foreach (int faceIndex in _visibleFaces)
            {
                MeshBuilder.Face disabledFace = Mesh.Faces[faceIndex];
                int[] halfEdges = Mesh.GetHalfEdgeIndicesOfFace(disabledFace);
                for (int j = 0; j < 3; j++)
                {
                    if ((disabledFace.HorizonEdgesOnCurrentIteration & (1 << j)) == 0)
                    {
                        if (disableCounter < horizonEdgeCount * 2)
                        {
                            // Use on this iteration
                            _newHalfEdgeIndices.Add(halfEdges[j]);
                            disableCounter++;
                        }
                        else
                        {
                            // Mark for reusal on later iteration step
                            Mesh.DisableHalfEdge(halfEdges[j]);
                        }
                    }
                }

                // Disable the face, but retain the points that were on the positive side of it.
                // We need to assign those points to the new faces we create shortly.
                List<int>? t = Mesh.DisableFace(faceIndex);
                if (t is not null)
                {
                    _disabledFacePointVectors.Add(t);
                }
            }

            if (disableCounter < horizonEdgeCount * 2)
            {
                int newHalfEdgesNeeded = (horizonEdgeCount * 2) - disableCounter;
                for (int i = 0; i < newHalfEdgesNeeded; i++)
                {
                    _newHalfEdgeIndices.Add(Mesh.AddHalfEdge());
                }
            }

            // Create new faces using the edgeloop
            for (int i = 0; i < horizonEdgeCount; i++)
            {
                int ab = _horizonEdges[i];

                int[] horizonEdgeVertexIndices = Mesh.GetVertexIndicesOfHalfEdge(Mesh.HalfEdges[ab]);
                int a = horizonEdgeVertexIndices[0];
                int b = horizonEdgeVertexIndices[1];
                int c = activePointIndex;

                int newFaceIndex = Mesh.AddFace();
                _newFaceIndices.Add(newFaceIndex);

                int ca = _newHalfEdgeIndices[(2 * i) + 0];
                int bc = _newHalfEdgeIndices[(2 * i) + 1];

                Mesh.HalfEdges[ab].Next = bc;
                Mesh.HalfEdges[bc].Next = ca;
                Mesh.HalfEdges[ca].Next = ab;

                Mesh.HalfEdges[bc].Face = newFaceIndex;
                Mesh.HalfEdges[ca].Face = newFaceIndex;
                Mesh.HalfEdges[ab].Face = newFaceIndex;

                Mesh.HalfEdges[ca].EndVertex = a;
                Mesh.HalfEdges[bc].EndVertex = c;

                MeshBuilder.Face newFace = Mesh.Faces[newFaceIndex];

                Vector3D planeNormal = MathUtils.GetTriangleNormal(_vertexData[a], _vertexData[b], activePoint);
                newFace.P = new Plane(planeNormal, activePoint);
                newFace.He = ab;

                Mesh.HalfEdges[ca].Opp = _newHalfEdgeIndices[i > 0 ? (i * 2) - 1 : (2 * horizonEdgeCount) - 1];
                Mesh.HalfEdges[bc].Opp = _newHalfEdgeIndices[((i + 1) * 2) % (horizonEdgeCount * 2)];
            }

            // Assign points that were on the positive side of the disabled faces to the new faces.
            foreach (List<int> disabledPoints in _disabledFacePointVectors)
            {
                foreach (int point in disabledPoints)
                {
                    if (point == activePointIndex)
                    {
                        continue;
                    }

                    for (int j = 0; j < horizonEdgeCount; j++)
                    {
                        if (AddPointToFace(Mesh.Faces[_newFaceIndices[j]], point))
                        {
                            break;
                        }
                    }
                }

                // The points are no longer needed: we can move them to the pool for reuse.
                ReclaimToIndexVectorPool(disabledPoints);
            }

            // Increase face stack size if needed
            foreach (int newFaceIndex in _newFaceIndices)
            {
                MeshBuilder.Face newFace = Mesh.Faces[newFaceIndex];
                if (newFace.PointsOnPositiveSide is not null && !newFace.InFaceStack)
                {
                    _faceList.Enqueue(newFaceIndex);
                    newFace.InFaceStack = true;
                }
            }
        }

        // Cleanup
        _indexVectorPool.Clear();
    }

    // Find indices of extreme values (max x, min x, max y, min y, max z, min z) for the given point cloud
    private int[] GetExtremeValues()
    {
        int[] outIndices = new int[6];
        Vector3D first = _vertexData[0];
        double[] extremeVals = [first.X, first.X, first.Y, first.Y, first.Z, first.Z];
        for (int i = 1; i < _vertexData.Length; i++)
        {
            Vector3D pos = _vertexData[i];
            if (pos.X > extremeVals[0])
            {
                extremeVals[0] = pos.X;
                outIndices[0] = i;
            }
            else if (pos.X < extremeVals[1])
            {
                extremeVals[1] = pos.X;
                outIndices[1] = i;
            }

            if (pos.Y > extremeVals[2])
            {
                extremeVals[2] = pos.Y;
                outIndices[2] = i;
            }
            else if (pos.Y < extremeVals[3])
            {
                extremeVals[3] = pos.Y;
                outIndices[3] = i;
            }

            if (pos.Z > extremeVals[4])
            {
                extremeVals[4] = pos.Z;
                outIndices[4] = i;
            }
            else if (pos.Z < extremeVals[5])
            {
                extremeVals[5] = pos.Z;
                outIndices[5] = i;
            }
        }

        return outIndices;
    }

    // Given a list of half edges, try to rearrange them so that they form a loop. Return true on success.
    private bool ReorderHorizonEdges(List<int> horizonEdges)
    {
        int horizonEdgeCount = horizonEdges.Count;
        for (int i = 0; i < horizonEdgeCount - 1; i++)
        {
            int endVertex = Mesh.HalfEdges[horizonEdges[i]].EndVertex;
            bool foundNext = false;
            for (int j = i + 1; j < horizonEdgeCount; j++)
            {
                int beginVertex = Mesh.HalfEdges[Mesh.HalfEdges[horizonEdges[j]].Opp].EndVertex;
                if (beginVertex == endVertex)
                {
                    (horizonEdges[i + 1], horizonEdges[j]) = (horizonEdges[j], horizonEdges[i + 1]);
                    foundNext = true;
                    break;
                }
            }

            if (!foundNext)
            {
                return false;
            }
        }

        return true;
    }

    // Compute scale of the vertex data.
    private double GetScale(int[] extremeValues)
    {
        double s = 0;
        for (int i = 0; i < 6; i++)
        {
            Vector3D v = _vertexData[extremeValues[i]];
            double a = Math.Abs(i / 2 == 0 ? v.X : (i / 2 == 1 ? v.Y : v.Z));
            if (a > s)
            {
                s = a;
            }
        }

        return s;
    }

    // Create a half edge mesh representing the base tetrahedron from which the QuickHull iteration proceeds.
    private void SetupInitialTetrahedron()
    {
        int vertexCount = _vertexData.Length;

        // If we have at most 4 points, just return a degenerate tetrahedron:
        if (vertexCount <= 4)
        {
            int[] v = [0, Math.Min(1, vertexCount - 1), Math.Min(2, vertexCount - 1), Math.Min(3, vertexCount - 1)];
            Vector3D n = MathUtils.GetTriangleNormal(_vertexData[v[0]], _vertexData[v[1]], _vertexData[v[2]]);
            var plane = new Plane(n, _vertexData[v[0]]);
            if (plane.IsPointOnPositiveSide(_vertexData[v[3]]))
            {
                (v[0], v[1]) = (v[1], v[0]);
            }

            Mesh.Setup(v[0], v[1], v[2], v[3]);
            return;
        }

        // Find two most distant extreme points.
        double maxD = _epsilonSquared;
        int selectedFirst = 0;
        int selectedSecond = 0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = i + 1; j < 6; j++)
            {
                double d = MathUtils.GetSquaredDistance(_vertexData[_extremeValues[i]], _vertexData[_extremeValues[j]]);
                if (d > maxD)
                {
                    maxD = d;
                    selectedFirst = _extremeValues[i];
                    selectedSecond = _extremeValues[j];
                }
            }
        }

        if (maxD == _epsilonSquared)
        {
            // A degenerate case: the point cloud seems to consists of a single point
            Mesh.Setup(0, Math.Min(1, vertexCount - 1), Math.Min(2, vertexCount - 1), Math.Min(3, vertexCount - 1));
            return;
        }

        // Find the most distant point to the line between the two chosen extreme points.
        var r = new Ray(_vertexData[selectedFirst], MathUtils.Subtract(_vertexData[selectedSecond], _vertexData[selectedFirst]));
        maxD = _epsilonSquared;
        int maxI = MeshBuilder.Disabled;
        int vCount = _vertexData.Length;
        for (int i = 0; i < vCount; i++)
        {
            double distToRay = MathUtils.GetSquaredDistanceBetweenPointAndRay(_vertexData[i], r);
            if (distToRay > maxD)
            {
                maxD = distToRay;
                maxI = i;
            }
        }

        if (maxD == _epsilonSquared)
        {
            // It appears that the point cloud belongs to a 1 dimensional subspace of R^3: convex hull has no volume => return a thin triangle
            // Pick any point other than selectedFirst and selectedSecond as the third point of the triangle
            Vector3D first = _vertexData[selectedFirst];
            Vector3D second = _vertexData[selectedSecond];
            int thirdPoint = selectedFirst;
            for (int i = 0; i < vCount; i++)
            {
                if (!MathUtils.AreEqual(_vertexData[i], first) && !MathUtils.AreEqual(_vertexData[i], second))
                {
                    thirdPoint = i;
                    break;
                }
            }

            Vector3D third = _vertexData[thirdPoint];
            int fourthPoint = selectedFirst;
            for (int i = 0; i < vCount; i++)
            {
                if (!MathUtils.AreEqual(_vertexData[i], first) && !MathUtils.AreEqual(_vertexData[i], second) && !MathUtils.AreEqual(_vertexData[i], third))
                {
                    fourthPoint = i;
                    break;
                }
            }

            Mesh.Setup(selectedFirst, selectedSecond, thirdPoint, fourthPoint);
            return;
        }

        // These three points form the base triangle for our tetrahedron.
        int[] baseTriangle = [selectedFirst, selectedSecond, maxI];
        Vector3D[] baseTriangleVertices = [_vertexData[baseTriangle[0]], _vertexData[baseTriangle[1]], _vertexData[baseTriangle[2]]];

        // Next step is to find the 4th vertex of the tetrahedron. We naturally choose the point farthest away from the triangle plane.
        maxD = _epsilon;
        maxI = 0;
        Vector3D normal = MathUtils.GetTriangleNormal(baseTriangleVertices[0], baseTriangleVertices[1], baseTriangleVertices[2]);
        var trianglePlane = new Plane(normal, baseTriangleVertices[0]);
        for (int i = 0; i < vCount; i++)
        {
            double d = Math.Abs(MathUtils.GetSignedDistanceToPlane(_vertexData[i], trianglePlane));
            if (d > maxD)
            {
                maxD = d;
                maxI = i;
            }
        }

        if (maxD == _epsilon)
        {
            // All the points seem to lie on a 2D subspace of R^3. How to handle this? Well, let's add one extra point to the point cloud so that the convex hull will have volume.
            _planar = true;
            Vector3D n1 = MathUtils.GetTriangleNormal(baseTriangleVertices[1], baseTriangleVertices[2], baseTriangleVertices[0]);
            var planarPointCloudTemp = new Vector3D[_vertexData.Length + 1];
            Array.Copy(_vertexData, planarPointCloudTemp, _vertexData.Length);
            planarPointCloudTemp[_vertexData.Length] = MathUtils.Add(n1, _vertexData[0]);
            maxI = planarPointCloudTemp.Length - 1;
            _vertexData = planarPointCloudTemp;
        }

        // Enforce CCW orientation (if user prefers clockwise orientation, swap two vertices in each triangle when final mesh is created)
        var triPlane = new Plane(normal, baseTriangleVertices[0]);
        if (triPlane.IsPointOnPositiveSide(_vertexData[maxI]))
        {
            (baseTriangle[0], baseTriangle[1]) = (baseTriangle[1], baseTriangle[0]);
        }

        // Create a tetrahedron half edge mesh and compute planes defined by each triangle
        Mesh.Setup(baseTriangle[0], baseTriangle[1], baseTriangle[2], maxI);
        foreach (MeshBuilder.Face f in Mesh.Faces)
        {
            int[] v = Mesh.GetVertexIndicesOfFace(f);
            Vector3D va = _vertexData[v[0]];
            Vector3D vb = _vertexData[v[1]];
            Vector3D vc = _vertexData[v[2]];
            f.P = new Plane(MathUtils.GetTriangleNormal(va, vb, vc), va);
        }

        // Finally we assign a face for each vertex outside the tetrahedron (vertices inside the tetrahedron have no role anymore)
        for (int i = 0; i < vCount; i++)
        {
            foreach (MeshBuilder.Face face in Mesh.Faces)
            {
                if (AddPointToFace(face, i))
                {
                    break;
                }
            }
        }
    }
}
