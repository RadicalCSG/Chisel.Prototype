using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using System.Collections.Generic;

public delegate void FunctionDelegate(int index, ref NativeStream stream);

public class GraphRuntime
{
    public NativeArray<FunctionPointer<FunctionDelegate>> functions;
    public NativeStream stream;

    public void ParseGraph(List<IRuntimeNode> nodes)
    {
        functions = new NativeArray<FunctionPointer<FunctionDelegate>>(nodes.Count, Allocator.Persistent);
        stream = new NativeStream(nodes.Count, Allocator.Persistent);

        for (int i = 0; i < nodes.Count; i++)
        {
            functions[i] = nodes[i].GetFunction();
            nodes[i].AddNode(stream);
        }
    }

    public void Execute()
    {
        var graphJob = new GraphJob { functions = functions, nodes = stream };
        graphJob.Run();
    }
}

[BurstCompile(CompileSynchronously = true)]
struct GraphJob : IJob
{
    public NativeArray<FunctionPointer<FunctionDelegate>> functions;
    public NativeStream nodes;

    public void Execute()
    {
        for (int i = 0; i < functions.Length; i++)
            functions[i].Invoke(i, ref nodes);
    }
}

public interface IRuntimeNode
{
    public void AddNode(NativeStream stream);
    public FunctionPointer<FunctionDelegate> GetFunction();
}