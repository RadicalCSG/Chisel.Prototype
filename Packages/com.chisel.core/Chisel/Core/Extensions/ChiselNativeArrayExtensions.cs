using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public static class ChiselNativeArrayExtensions
    {
        public unsafe static void ClearValues<T>(this NativeArray<T> array) where T : unmanaged
        {
            if (array.Length == 0)
                return;
            UnsafeUtility.MemSet(array.GetUnsafePtr(), 0, array.Length * sizeof(T));
        }

        public unsafe static NativeArray<T> ToNativeArray<T>(this List<T> list, Allocator allocator) where T : struct
        {
            var nativeList = new NativeArray<T>(list.Count, allocator);
            for (int i = 0; i < list.Count; i++)
                nativeList[i] = list[i];
            return nativeList;
        }

        public unsafe static NativeArray<T> ToNativeArray<T>(this T[] array, Allocator allocator) where T : unmanaged
        {
            return new NativeArray<T>(array, allocator);
        }

        public unsafe static void AddRange<T>(this List<T> list, NativeArray<T> collection) 
            where T : unmanaged
        {
            if (list.Capacity < list.Count + collection.Length)
                list.Capacity = list.Count + collection.Length;
            for (int i = 0; i < collection.Length; i++)
                list.Add(collection[i]);
        }


        public unsafe static void AddRange(this List<Vector2> list, NativeArray<float2> collection)
        {
            if (list.Capacity < list.Count + collection.Length)
                list.Capacity = list.Count + collection.Length;
            for (int i = 0; i < collection.Length; i++)
                list.Add(collection[i]);
        }

        public unsafe static void AddRange(this List<Vector3> list, NativeArray<float3> collection)
        {
            if (list.Capacity < list.Count + collection.Length)
                list.Capacity = list.Count + collection.Length;
            for (int i = 0; i < collection.Length; i++)
                list.Add(collection[i]);
        }

        public unsafe static void AddRange(this List<Vector4> list, NativeArray<float4> collection)
        {
            if (list.Capacity < list.Count + collection.Length)
                list.Capacity = list.Count + collection.Length;
            for (int i = 0; i < collection.Length; i++)
                list.Add(collection[i]);
        }
    }
}
