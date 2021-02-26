using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct BrushOutline : IDisposable
    {
        public NativeList<float3>   vertices;
        public NativeList<int>      visibleOuterLines;
        public NativeList<int>      surfaceVisibleOuterLines;
        public NativeList<int>      surfaceVisibleOuterLineRanges;
        public uint                 hash;

        public bool IsCreated
        {
            get
            {
                return visibleOuterLines.IsCreated &&
                       surfaceVisibleOuterLines.IsCreated &&
                       surfaceVisibleOuterLineRanges.IsCreated &&
                       vertices.IsCreated;
            }
        }

        public static BrushOutline Create()
        {
            return new BrushOutline
            {
                vertices                      = new NativeList<float3>(Allocator.Persistent),
                visibleOuterLines             = new NativeList<int>(Allocator.Persistent),
                surfaceVisibleOuterLines      = new NativeList<int>(Allocator.Persistent),
                surfaceVisibleOuterLineRanges = new NativeList<int>(Allocator.Persistent)
            };
        }

        public void Dispose()
        {
            if (vertices                     .IsCreated) vertices                     .Dispose(); vertices                      = default;
            if (visibleOuterLines            .IsCreated) visibleOuterLines            .Dispose(); visibleOuterLines             = default;
            if (surfaceVisibleOuterLines     .IsCreated) surfaceVisibleOuterLines     .Dispose(); surfaceVisibleOuterLines      = default;
            if (surfaceVisibleOuterLineRanges.IsCreated) surfaceVisibleOuterLineRanges.Dispose(); surfaceVisibleOuterLineRanges = default;
        }

        public void Reset()
        {
            vertices                     .Clear();
            visibleOuterLines            .Clear();
            surfaceVisibleOuterLines     .Clear();
            surfaceVisibleOuterLineRanges.Clear();
            hash = 0;
        }

        static unsafe uint GetListHash<T>(ref NativeList<T> list)
            where T : unmanaged
        {
            return math.hash(list.GetUnsafeReadOnlyPtr<T>(), list.Length * sizeof(T));
        }

        void UpdateHash()
        {
            hash = math.hash(new uint4(GetListHash(ref vertices),
                                       GetListHash(ref visibleOuterLines),
                                       GetListHash(ref surfaceVisibleOuterLines),
                                       GetListHash(ref surfaceVisibleOuterLineRanges)));
        }

        // TODO: use BrushMeshBlob instead
        public void Fill(BrushMesh brushMesh)
        {
            Reset();
            if (brushMesh == null)
                return;

            vertices.AddRange(brushMesh.vertices);
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                var firstEdge = brushMesh.polygons[p].firstEdge;
                var edgeCount = brushMesh.polygons[p].edgeCount;
                var lastEdge  = firstEdge + edgeCount;

                var vertexIndex0 = brushMesh.halfEdges[lastEdge - 1].vertexIndex;
                var vertexIndex1 = 0;
                for (int h = firstEdge; h < lastEdge; vertexIndex0 = vertexIndex1, h++)
                {
                    vertexIndex1 = brushMesh.halfEdges[h].vertexIndex;
                    if (vertexIndex0 > vertexIndex1) // avoid duplicate edges
                    {
                        visibleOuterLines.Add(vertexIndex0);
                        visibleOuterLines.Add(vertexIndex1);
                    }
                    surfaceVisibleOuterLines.Add(vertexIndex0);
                    surfaceVisibleOuterLines.Add(vertexIndex1);
                }
                surfaceVisibleOuterLineRanges.Add(surfaceVisibleOuterLines.Length);
            }
            UpdateHash();
        }
    }
}
