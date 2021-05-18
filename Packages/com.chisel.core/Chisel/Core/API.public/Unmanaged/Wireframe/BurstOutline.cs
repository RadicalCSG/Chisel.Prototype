using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct BrushOutline : IDisposable
    {
        public UnsafeList<float3>   vertices;
        public UnsafeList<int>      visibleOuterLines;
        public UnsafeList<int>      surfaceVisibleOuterLines;
        public UnsafeList<int>      surfaceVisibleOuterLineRanges;
        public bool                 initialized;
        public uint                 hash;

        public static BrushOutline Create()
        {
            return new BrushOutline
            {
                initialized                   = true,
                vertices                      = new UnsafeList<float3>(64, Allocator.Persistent),
                visibleOuterLines             = new UnsafeList<int>(64, Allocator.Persistent),
                surfaceVisibleOuterLines      = new UnsafeList<int>(64, Allocator.Persistent),
                surfaceVisibleOuterLineRanges = new UnsafeList<int>(64, Allocator.Persistent)
            };
        }

        public unsafe bool IsCreated
        {
            get
            {
                return initialized &&
                       vertices.Ptr != null && vertices.IsCreated &&
                       visibleOuterLines.Ptr != null && visibleOuterLines.IsCreated &&
                       surfaceVisibleOuterLines.Ptr != null && surfaceVisibleOuterLines.IsCreated &&
                       surfaceVisibleOuterLineRanges.Ptr != null && surfaceVisibleOuterLineRanges.IsCreated;
            }
        }

        public unsafe void Dispose()
        {
            try
            {
                initialized = false;
                if (vertices.Ptr                      != null && vertices                     .IsCreated) vertices                     .Dispose(); 
                if (visibleOuterLines.Ptr             != null && visibleOuterLines            .IsCreated) visibleOuterLines            .Dispose(); 
                if (surfaceVisibleOuterLines.Ptr      != null && surfaceVisibleOuterLines     .IsCreated) surfaceVisibleOuterLines     .Dispose(); 
                if (surfaceVisibleOuterLineRanges.Ptr != null && surfaceVisibleOuterLineRanges.IsCreated) surfaceVisibleOuterLineRanges.Dispose(); 
            }
            catch(Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
            finally
            { 
                vertices = default; 
                visibleOuterLines = default; 
                surfaceVisibleOuterLines = default; 
                surfaceVisibleOuterLineRanges = default;
            }
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
            return math.hash(list.GetUnsafePtr(), list.Length * sizeof(T));
        }

        static unsafe uint GetListHash<T>(ref UnsafeList<T> list)
            where T : unmanaged
        {
            return math.hash(list.Ptr, list.Length * sizeof(T));
        }

        void UpdateHash()
        {
            hash = math.hash(new uint4(GetListHash(ref vertices),
                                       GetListHash(ref visibleOuterLines),
                                       GetListHash(ref surfaceVisibleOuterLines),
                                       GetListHash(ref surfaceVisibleOuterLineRanges)));
        }

        // TODO: use BrushMeshBlob instead
        internal void Fill(ref BrushMeshBlob brushMesh)
        {
            if (!IsCreated)
            {
                UnityEngine.Debug.LogError($"Calling Fill on uninitialized Outline");
                return;
            }
            Reset();

            vertices.AddRange(ref brushMesh.localVertices);
            UnityEngine.Debug.Assert(vertices.length == brushMesh.localVertices.Length);
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
