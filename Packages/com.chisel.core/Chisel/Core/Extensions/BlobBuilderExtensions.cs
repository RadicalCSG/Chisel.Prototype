using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public static class BlobBuilderExtensions
    {
        public unsafe static void ClearValues<T>(ref BlobArray<T> array) where T : unmanaged
        {
            if (array.Length == 0)
                return;
            UnsafeUtility.MemSet(array.GetUnsafePtr(), 0, array.Length * sizeof(T));
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, NativeList<T> data) where T : unmanaged
        {
            var blobBuilderArray = builder.Allocate(ref blobArray, data.Length);
            if (data.Length > 0)
                UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), data.GetUnsafeReadOnlyPtr(), blobBuilderArray.Length * sizeof(T));
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, ref BlobArray<T> data) where T : unmanaged
        {
            var blobBuilderArray = builder.Allocate(ref blobArray, data.Length);
            if (data.Length > 0)
                UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), data.GetUnsafePtr(), blobBuilderArray.Length * sizeof(T));
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, NativeArray<T> data) where T : unmanaged
        {
            var blobBuilderArray = builder.Allocate(ref blobArray, data.Length);
            if (data.Length > 0)
                UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), data.GetUnsafeReadOnlyPtr(), blobBuilderArray.Length * sizeof(T));
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, List<T> data) where T : unmanaged
        {
            var blobBuilderArray = builder.Allocate(ref blobArray, data.Count);
            for (int i = 0; i < data.Count; i++)
                blobBuilderArray[i] = data[i];
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, List<T> data, int length) where T : unmanaged
        {
            var blobBuilderArray = builder.Allocate(ref blobArray, length);
            for (int i = 0; i < length; i++)
                blobBuilderArray[i] = data[i];
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, T[] data, int length) where T : unmanaged
        {
            var blobBuilderArray = builder.Allocate(ref blobArray, length);
            for (int i = 0; i < length; i++)
                blobBuilderArray[i] = data[i];
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, NativeList<T> data, int length) where T : unmanaged
        {
            length = math.max(length, 0);
            var blobBuilderArray = builder.Allocate(ref blobArray, length);
            if (length > 0)
            {
                var srcPtr = data.GetUnsafeReadOnlyPtr();
                var dstPtr = blobBuilderArray.GetUnsafePtr();
                UnsafeUtility.MemCpy(dstPtr, srcPtr, blobBuilderArray.Length * sizeof(T));
            }
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, NativeArray<T> data, int length) where T : unmanaged
        {
            length = math.max(length, 0);
            var blobBuilderArray = builder.Allocate(ref blobArray, length);
            if (length > 0)
            {
                var srcPtr = data.GetUnsafeReadOnlyPtr();
                var dstPtr = blobBuilderArray.GetUnsafePtr();
                UnsafeUtility.MemCpy(dstPtr, srcPtr, blobBuilderArray.Length * sizeof(T));
            } 
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, ref BlobArray<T> data, int length) where T : unmanaged
        {
            length = math.max(length, 0);
            var blobBuilderArray = builder.Allocate(ref blobArray, length);
            if (length > 0)
            {
                var srcPtr = data.GetUnsafePtr();
                var dstPtr = blobBuilderArray.GetUnsafePtr();
                UnsafeUtility.MemCpy(dstPtr, srcPtr, blobBuilderArray.Length * sizeof(T));
            }
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, T* data, int length) where T : unmanaged
        {
            length = math.max(length, 0);
            var blobBuilderArray = builder.Allocate(ref blobArray, length);
            if (length > 0)
                UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), data, blobBuilderArray.Length * sizeof(T));
            return blobBuilderArray;
        }

        public static unsafe BlobBuilderArray<T> Construct<T>(this BlobBuilder builder, ref BlobArray<T> blobArray, HashedVertices data) where T : unmanaged
        {
            var blobBuilderArray = builder.Allocate(ref blobArray, data.Length);
            if (data.Length > 0)
                UnsafeUtility.MemCpy(blobBuilderArray.GetUnsafePtr(), data.GetUnsafeReadOnlyPtr(), blobBuilderArray.Length * sizeof(T));
            return blobBuilderArray;
        }
    }
}
