using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core
{
    public class Test : MonoBehaviour
    {
        void Start()
        {
            var runtime = new GraphRuntime();

            var node1 = new Node1();
            var node2 = new Node2();

            var list = new List<IRuntimeNode> { node1, node2 };
            runtime.ParseGraph(list);


        }
    }
}

public class Node1 : IRuntimeNode
{
    public int a;

    public void AddNode(NativeStream stream)
    {
        var writer = stream.AsWriter();
        writer.BeginForEachIndex(stream.Count());
        writer.Write(a);
        writer.EndForEachIndex();
    }

    public FunctionPointer<FunctionDelegate> GetFunction()
    {
        return BurstCompiler.CompileFunctionPointer<FunctionDelegate>(Node1Function.Function);
    }
}

[BurstCompile(CompileSynchronously = true)]
public static class Node1Function
{
    [BurstCompile(CompileSynchronously = true)]
    public static void Function(int index, ref NativeStream stream)
    {

    }
}

public class Node2 : IRuntimeNode
{
    public int index;
    public int output;

    public void AddNode(NativeStream stream)
    {
        var writer = stream.AsWriter();
        writer.BeginForEachIndex(stream.Count());
        writer.Write(index);
        writer.EndForEachIndex();
    }

    public FunctionPointer<FunctionDelegate> GetFunction()
    {
        return BurstCompiler.CompileFunctionPointer<FunctionDelegate>(Node2Function.Function);
    }
}

[BurstCompile(CompileSynchronously = true)]
public static class Node2Function
{
    [BurstCompile(CompileSynchronously = true)]
    public static void Function(int index, ref NativeStream stream)
    {
        var reader = stream.AsReader();
        reader.BeginForEachIndex(index);
        var targetIndex = reader.Read<int>();
        reader.EndForEachIndex();

        reader.BeginForEachIndex(targetIndex);
        var target = reader.Read<int>();
        reader.EndForEachIndex();


        var writer = stream.AsWriter();
        writer.BeginForEachIndex(index);
        writer.Write(targetIndex);
        writer.Write(target);
        writer.EndForEachIndex();
    }
}