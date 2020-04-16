#define USE_OPTIMIZATIONS
//#define SHOW_DEBUG_MESSAGES 
using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    internal unsafe struct CreateRoutingTableJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<int>                          treeBrushIndices;
        [NoAlias, ReadOnly] public BlobAssetReference<CompactTree>           compactTree;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushes;

        [NoAlias, WriteOnly] public NativeHashMap<int, BlobAssetReference<RoutingTable>>.ParallelWriter routingTableLookup;

        public void Execute(int index)
        {
            if (index >= treeBrushIndices.Length)
                return;

            var processedNodeIndex = treeBrushIndices[index];

            int categoryStackNodeCount, polygonGroupCount;
            if (!brushesTouchedByBrushes.TryGetValue(processedNodeIndex, out BlobAssetReference<BrushesTouchedByBrush> brushesTouchedByBrush))
                return;
            
            var maxNodes        = compactTree.Value.topDownNodes.Length;
            var routingTable    = new NativeList<CategoryStackNode>(maxNodes * 3, Allocator.Temp);
            {
                GetStackNodes(ref compactTree.Value.topDownNodes, ref brushesTouchedByBrush.Value, processedNodeIndex, routingTable);

#if SHOW_DEBUG_MESSAGES
                if (processedNode.NodeID == kDebugNode || kDebugNode == -1)
                    Dump(processedNode, stack); 
#endif
                categoryStackNodeCount = (int)routingTable.Length;

                int maxCounter = (int)CategoryRoutingRow.Length;
                for (int i = 0; i < categoryStackNodeCount; i++)
                    maxCounter = Math.Max(maxCounter, (int)routingTable[i].input);
                polygonGroupCount = maxCounter + 1;
                    
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root    = ref builder.ConstructRoot<RoutingTable>();
                var inputs      = builder.Allocate(ref root.inputs,           routingTable.Length);
                var routingRows = builder.Allocate(ref root.routingRows,      routingTable.Length);

                var routingLookups  = stackalloc RoutingLookup[maxNodes];
                var nodes           = stackalloc int[maxNodes];
                {
                    // TODO: clean up
                    int nodeCounter = 0;
                    for (int i = 0; i < routingTable.Length;)
                    {
                        var cutting_node_index = routingTable[i].nodeIndex;

                        int start_index = i;
                        do
                        {
                            inputs[i] = routingTable[i].input;
                            routingRows[i] = routingTable[i].routingRow;
                            i++;
                        } while (i < routingTable.Length && routingTable[i].nodeIndex == cutting_node_index);
                        int end_index = i;


                        nodes[nodeCounter] = cutting_node_index + 1;
                        routingLookups[nodeCounter] = new RoutingLookup(start_index, end_index);
                        nodeCounter++;
                    }

                    builder.Construct(ref root.routingLookups,  routingLookups, nodeCounter);
                    builder.Construct(ref root.nodes,           nodes,          nodeCounter);
                        
                    var routingTableBlob = builder.CreateBlobAssetReference<RoutingTable>(Allocator.Persistent);
                    builder.Dispose();

                    // TODO: figure out why this sometimes returns false, without any duplicates, yet values seem to exist??
                    routingTableLookup.TryAdd(processedNodeIndex, routingTableBlob);
                    //FailureMessage();
                }
            }
            routingTable.Dispose();
        }

        [BurstDiscard]
        static void FailureMessage()
        {
            Debug.LogError("Unity Burst Compiler is broken");
        }

        // Remap indices to new destinations, used when destination rows have been merged
        static void RemapIndices(NativeList<CategoryStackNode> stack, NativeArray<int> remap, int start, int last)
        {
            for (int i = start; i < last; i++)
            {
                var categoryRow = stack[i];
                var routingRow = categoryRow.routingRow;

                for (int r = 0; r < CategoryRoutingRow.Length; r++)
                {
                    var key = (int)routingRow[r];
                    if (key >= remap.Length || remap[key] == 0) { FailureMessage(); return; }
                }

                for (int r = 0; r < CategoryRoutingRow.Length; r++)
                    routingRow[r] = (CategoryGroupIndex)(remap[(int)routingRow[r]] - 1);

                categoryRow.routingRow = routingRow;
                stack[i] = categoryRow;
            }
        }

        // TODO: rewrite in such a way that we don't rely on stack
        public static void GetStackNodes(ref BlobArray<CompactTopDownNode> topDownNodes, ref BrushesTouchedByBrush brushesTouchedByBrush, int processedNodeIndex, NativeList<CategoryStackNode> output)
        {
            int haveGonePastSelf = 0;
            output.Clear();
            GetStack(ref topDownNodes, ref brushesTouchedByBrush, processedNodeIndex, ref topDownNodes[0], ref haveGonePastSelf, output);
            if (output.Length == 0)
                output.Add(new CategoryStackNode() { nodeIndex = processedNodeIndex, operation = CSGOperationType.Additive, routingRow = CategoryRoutingRow.outside });
        }

        static void GetStack(ref BlobArray<CompactTopDownNode> topDownNodes, ref BrushesTouchedByBrush brushesTouchedByBrush, int processedNodeIndex, ref CompactTopDownNode currentNode, ref int haveGonePastSelf, NativeList<CategoryStackNode> output)
        {
            var intersectionType    = brushesTouchedByBrush.Get(currentNode.nodeIndex);
            if (intersectionType == IntersectionType.NoIntersection)
                return;// sEmptyStack.ToList();


            // TODO: use other intersection types
            if (intersectionType == IntersectionType.AInsideB) { output.Add(new CategoryStackNode() { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.inside }); return; }
            if (intersectionType == IntersectionType.BInsideA) { output.Add(new CategoryStackNode() { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.outside }); return; }

            switch (currentNode.Type)
            {
                case CSGNodeType.Brush:
                {
                    // All surfaces of processedNode are aligned with it's own surfaces, so all categories are Aligned
                    if (processedNodeIndex == currentNode.nodeIndex)
                    {
                        haveGonePastSelf = 1;
                        output.Add(new CategoryStackNode() { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.selfAligned } );
                        return;
                    }

                    if (haveGonePastSelf > 0)
                        haveGonePastSelf = 2;

                    // Otherwise return identity categories (input == output)
                    output.Add(new CategoryStackNode() { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.identity });
                    return;
                }
                default:
                {
                    var nodeCount = currentNode.childCount;
                    if (nodeCount == 0)
                        return;// sEmptyStack.ToList();

                    // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                    var firstIndex = currentNode.childOffset;
                    var lastIndex = firstIndex + nodeCount;
                    for (; firstIndex < lastIndex && (topDownNodes[firstIndex].Operation != CSGOperationType.Additive &&
                                                      topDownNodes[firstIndex].Operation != CSGOperationType.Copy); firstIndex++)
                        firstIndex++;

                    if ((lastIndex - firstIndex) <= 0)
                        return;// sEmptyStack.ToList();

                    if ((lastIndex - firstIndex) == 1)
                    {
                        GetStack(//in operationTables, 
                                 ref topDownNodes, ref brushesTouchedByBrush, processedNodeIndex, ref topDownNodes[firstIndex], ref haveGonePastSelf, output);

                        // Node operation is always Additive at this point, and operation would be performed against .. nothing ..
                        // Anything added with nothing is itself, so we don't need to apply an operation here.

                        if (output.Length > 0)
                        {
                            var item = output[output.Length - 1];
                            item.operation = topDownNodes[firstIndex].Operation;
                            output[output.Length - 1] = item;
                        }

#if SHOW_DEBUG_MESSAGES
                        if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)
                            Dump(stack, 0, "stack return ");
#endif
                        return;
                    } else
                    {
                        var leftHaveGonePastSelf = 0;

                        var leftStack   = output;
                        var rightStack = new NativeList<CategoryStackNode>(Allocator.Temp); // TODO: get rid of allocation, store rightStack after leftStack and duplicate it -> then optimize
                        {
                            leftStack.Clear();
                            GetStack(//in operationTables, 
                                     ref topDownNodes, ref brushesTouchedByBrush, processedNodeIndex, ref topDownNodes[firstIndex], ref leftHaveGonePastSelf, leftStack);
                            haveGonePastSelf |= leftHaveGonePastSelf;
                            for (int i = firstIndex + 1; i < lastIndex; i++)
                            {
#if SHOW_DEBUG_MESSAGES
                                if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)                           
                                    Dump(leftStack, 0, $"before '{node[i - 1]}' {node[i].Operation} '{node[i]}'");
#endif
                                var rightHaveGonePastSelf = leftHaveGonePastSelf >= 1 ? 2 : 0;
                                rightStack.Clear();
                                GetStack(//in operationTables, 
                                         ref topDownNodes, ref brushesTouchedByBrush, processedNodeIndex, ref topDownNodes[i], ref rightHaveGonePastSelf, rightStack);
                                haveGonePastSelf |= rightHaveGonePastSelf;

                                Combine(//in operationTables,
                                        ref topDownNodes,
                                        ref brushesTouchedByBrush,
                                        processedNodeIndex,
                                        leftStack,
                                        leftHaveGonePastSelf,
                                        rightStack,
                                        rightHaveGonePastSelf,
                                        topDownNodes[i].Operation
                                );
                                leftHaveGonePastSelf = rightHaveGonePastSelf;

                                //if (leftStack.Length > 0 && node.Operation == CSGOperationType.Copy)
                                //    leftStack[leftStack.Length - 1].operation = node.Operation;

#if SHOW_DEBUG_MESSAGES
                                if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)
                                    Dump(leftStack, 0, $"after '{node[i - 1]}' {node[i].Operation} '{node[i]}'");
#endif
                            }
                        }
                        rightStack.Dispose();
                        return;
                    }
                }
            }
        }

        static void Combine(ref BlobArray<CompactTopDownNode> topDownNodes, ref BrushesTouchedByBrush brushesTouchedByBrush, int processedNodeIndex, NativeList<CategoryStackNode> leftStack, int leftHaveGonePastSelf, NativeList<CategoryStackNode> rightStack, int rightHaveGonePastSelf, CSGOperationType operation)
        {
            if (operation == CSGOperationType.Invalid)
                operation = CSGOperationType.Additive;

            if (leftStack.Length == 0) // left node has a branch without children or children are not intersecting with processedNode
            {
                if (rightStack.Length == 0) // right node has a branch without children or children are not intersecting with processedNode
                {
                    leftStack.Clear(); return;// sEmptyStack.ToList();
                }
                switch (operation)
                {
                    case CSGOperationType.Additive:
                    case CSGOperationType.Copy: leftStack.Clear(); leftStack.AddRange(rightStack); return; //rightStack;
                    default: leftStack.Clear(); return;// sEmptyStack.ToList();
                }
            } else
            if (rightStack.Length == 0) // right node has a branch without children or children are not intersecting with processedNode
            {
                switch (operation)
                {
                    case CSGOperationType.Additive:
                    case CSGOperationType.Copy:
                    case CSGOperationType.Subtractive: return; //leftStack
                    default: leftStack.Clear(); return;// sEmptyStack.ToList();
                }
            }

            int index       = 0;
            int vIndex      = 0;

            var firstNode   = rightStack[0].nodeIndex;

            var combineUsedIndices  = new NativeArray<byte>(leftStack.Length + (CategoryRoutingRow.Length * rightStack.Length), Allocator.Temp);
            var combineIndexRemap   = new NativeArray<int>(leftStack.Length + (CategoryRoutingRow.Length * rightStack.Length), Allocator.Temp);
            var routingSteps        = new NativeList<int>(rightStack.Length, Allocator.Temp);
            {

                // Count the number of rows for unique node
                var rightNode    = firstNode;
                int counter = 0;
                for (int r = 0; r < rightStack.Length; r++)
                {
                    if (rightNode != rightStack[r].nodeIndex)
                    {
                        routingSteps.Add(counter);
                        counter = 0;
                        rightNode = rightStack[r].nodeIndex;
                    }
                    counter++;
                }
                routingSteps.Add(counter);


                int prevNodeIndex = leftStack.Length - 1;
                while (prevNodeIndex > 0)
                {
                    if (prevNodeIndex <= 0 ||
                        leftStack[prevNodeIndex - 1].nodeIndex != leftStack[prevNodeIndex].nodeIndex)
                        break;
                    prevNodeIndex--;
                }
                int startNodeIndex = leftStack.Length;

                for (int p = prevNodeIndex; p < startNodeIndex; p++)
                {
                    for (int t = 0; t < CategoryRoutingRow.Length; t++)
                        combineUsedIndices[(int)leftStack[p].routingRow[t]] = 1;
                }
                if (startNodeIndex == 0)
                {
                    combineUsedIndices[0] = 1;
                    combineUsedIndices[1] = 1;
                    combineUsedIndices[2] = 1;
                    combineUsedIndices[3] = 1;
                }

#if HAVE_SELF_CATEGORIES
                var operationTableOffset = (int)operation;
                //var operationTable  = Operation.Tables[(int)operation];
#else
                var operationTableOffset = (leftHaveGonePastSelf >= 1 && rightStack.Length == 1 ?
                                            CategoryRoutingRow.RemoveOverlappingOffset : 0) +
                                            ((int)operation) * CategoryRoutingRow.NumberOfRowsPerOperation;
                bool haveRemap = false;
#endif
                int stackIndex = 1;
                rightNode = firstNode;
                for (int r = 0; r < rightStack.Length; r++)
                {
                    if (rightNode != rightStack[r].nodeIndex)
                    {
#if USE_OPTIMIZATIONS
                        if (prevNodeIndex >= 0 && haveRemap && combineIndexRemap.Length > 0)
                        {
                            RemapIndices(leftStack, combineIndexRemap, prevNodeIndex, startNodeIndex);
#if SHOW_DEBUG_MESSAGES
                            if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)
                                Dump(sCombineChildren.ToArray(), 0);
#endif
                        }
#endif

                        prevNodeIndex = startNodeIndex;
                        startNodeIndex = leftStack.Length;
                        index = 0; vIndex = 0; stackIndex++;
                        rightNode = rightStack[r].nodeIndex;
                        //sCombineIndexRemap = null;
                        //sCombineIndexRemap.Clear();

                        combineUsedIndices.ClearValues();
                        for (int p = prevNodeIndex; p < startNodeIndex; p++)
                        {
                            for (int t = 0; t < CategoryRoutingRow.Length; t++)
                                combineUsedIndices[(int)leftStack[p].routingRow[t]] = 1;
                        }
                        combineIndexRemap.ClearValues();
                    }

                    var leftKeepContents = leftStack[prevNodeIndex].operation == CSGOperationType.Copy;
                    var rightKeepContents = rightStack[r].operation == CSGOperationType.Copy;// || operation == CSGOperationType.Copy;
                
                    CategoryRoutingRow routingRow;
                    int routingOffset = 0;
                    if (stackIndex >= routingSteps.Length) // last node in right stack
                    {
                        int ncount = 0;
                        var startR = r;

                        // Duplicate route multiple times, bake operation into table
                        for (int t = 0; t < CategoryRoutingRow.Length; t++)
                        {
                            for (r = startR; r < rightStack.Length; r++)
                            {
                                var rightInput = rightStack[r].routingRow;
                                if (rightKeepContents)
                                    rightInput[0] = rightInput[(int)CategoryIndex.Outside];
                                var leftIndex = (leftKeepContents && t == 0) ? CategoryIndex.Outside : (CategoryIndex)t;

                                // Fix up output of last node to include operation between
                                // last left and last right.
                                // We don't add a routingOffset here since this might be the last node & 
                                // we don't even know if there is a next node at this point.
                                routingRow = new CategoryRoutingRow(operationTableOffset, leftIndex, rightInput); // applies operation

                                int foundIndex = -1;
#if USE_OPTIMIZATIONS
                                if (vIndex < combineUsedIndices.Length &&
                                    combineUsedIndices[vIndex] == 1)
#endif
                                {
#if USE_OPTIMIZATIONS
                                    for (int n = startNodeIndex; n < leftStack.Length; n++)
                                    {
                                        Debug.Assert(rightStack[r].nodeIndex == leftStack[n].nodeIndex);
                                        if (leftStack[n].routingRow.Equals(routingRow))
                                        {
                                            foundIndex = (int)leftStack[n].input;
                                            break;
                                        }
                                    }
#endif
                                    if (foundIndex == -1)
                                    {
                                        leftStack.Add(new CategoryStackNode()
                                        {
                                            nodeIndex   = rightStack[r].nodeIndex,
                                            operation   = rightStack[r].operation,
                                            input       = (CategoryGroupIndex)index,
                                            routingRow  = routingRow
                                        });
                                        foundIndex = index;
                                        index++;
                                    }
                                }

                                haveRemap = true;
                                combineIndexRemap[vIndex] = foundIndex + 1;
                                vIndex++;
                                ncount++;
                            }
                        }
#if SHOW_DEBUG_MESSAGES
                        /*
                        if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)
                        {
                            Debug.Log($"[{startR}/{rightStack.Length}] {ncount} {vIndex} / {sCombineIndexRemap.Count} {rightStack[startR].node} {rightStack.Length - startR} +");
                            Dump(sCombineChildren.ToArray(), 0);
                        }*/
#endif
                    } else
                    {
                        int ncount = 0;
                        var startR = r;
                        int routingStep = routingSteps[stackIndex];

                        // Duplicate route multiple times
                        for (int t = 0; t < CategoryRoutingRow.Length; t++, routingOffset += routingStep)
                        {
                            //for (r = startR; r < startR + routingSteps[nodeIndex - 1]; r++)
                            {
                                var rightInput = rightStack[r].routingRow;
                                if (rightKeepContents)
                                    rightInput[0] = rightInput[(int)CategoryIndex.Outside];

                                // Fix up routing to include offset b/c duplication
                                routingRow = rightInput + routingOffset;

                                int foundIndex = -1;
#if USE_OPTIMIZATIONS
                                if (vIndex < combineUsedIndices.Length &&
                                    combineUsedIndices[vIndex] == 1)
#endif
                                {
#if USE_OPTIMIZATIONS
                                    for (int n = startNodeIndex; n < leftStack.Length; n++)
                                    {
                                        Debug.Assert(rightStack[r].nodeIndex == leftStack[n].nodeIndex);
                                        if (leftStack[n].routingRow.Equals(routingRow))
                                        {
                                            foundIndex = (int)leftStack[n].input;
                                            break;
                                        }
                                    }
#endif
                                    if (foundIndex == -1)
                                    {
                                        leftStack.Add(new CategoryStackNode()
                                        {
                                            nodeIndex   = rightStack[r].nodeIndex,
                                            operation   = rightStack[r].operation,
                                            input       = (CategoryGroupIndex)index,
                                            routingRow  = routingRow
                                        });
                                        foundIndex = index;
                                        index++;
                                    }
                                }

                                haveRemap = true;
                                combineIndexRemap[vIndex] = foundIndex + 1;
                                vIndex++;
                                ncount++;
                            }
                        }
#if SHOW_DEBUG_MESSAGES
                        /*
                        if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)
                        {
                            Debug.Log($"[{r}/{rightStack.Length}] {ncount} {vIndex} / {sCombineIndexRemap.Count} {rightStack[r].node} -");
                            Dump(sCombineChildren.ToArray(), 0);
                        }*/
#endif
                    }
                }

#if USE_OPTIMIZATIONS
                if (//nodeIndex > 1 && 
                    prevNodeIndex >= 0 && haveRemap && combineIndexRemap.Length > 0)
                {
                    RemapIndices(leftStack, combineIndexRemap, prevNodeIndex, startNodeIndex);
#if SHOW_DEBUG_MESSAGES 
                    if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)
                        Dump(sCombineChildren.ToArray(), 0);
#endif
                    bool allEqual = true;
                    combineIndexRemap.ClearValues();
                    for (int i = startNodeIndex; i < leftStack.Length; i++)
                    {
                        if (!leftStack[i].routingRow.AreAllTheSame())
                        {
                            allEqual = false;
                            break;
                        }
                        combineIndexRemap[(int)leftStack[i].input] = ((int)leftStack[i].routingRow[0]) + 1;
                    }
                    if (allEqual)
                    {
                        leftStack.RemoveRange(startNodeIndex, leftStack.Length - startNodeIndex);
                        RemapIndices(leftStack, combineIndexRemap, prevNodeIndex, startNodeIndex);

#if SHOW_DEBUG_MESSAGES
                        if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)
                            Dump(sCombineChildren.ToArray(), 0);
#endif
                    }
                }

                // When all the paths for the first node lead to the same destination, just remove it
                int firstRemoveCount = 0;
                while (firstRemoveCount < leftStack.Length - 1 &&
                        leftStack[firstRemoveCount].nodeIndex != leftStack[firstRemoveCount + 1].nodeIndex &&
                        leftStack[firstRemoveCount].routingRow.AreAllValue(0))
                    firstRemoveCount++;
                if (firstRemoveCount > 0)
                    leftStack.RemoveRange(0, firstRemoveCount);
#endif

#if SHOW_DEBUG_MESSAGES
                if (processedNodeIndex + 1 == kDebugNode || kDebugNode == -1)
                    Dump(sCombineChildren.ToArray(), 0);
#endif
            }
            combineUsedIndices.Dispose();
            combineIndexRemap.Dispose();
        }

#if SHOW_DEBUG_MESSAGES
        static int kDebugNode = -1; 
        static void Dump(CSGTreeNode processedNode, CategoryStackNode[] stack, int depth = 0)
        {
            var space = new String(' ', depth);
            if (stack == null)
            {
                Debug.Log($"{space}processedNode: {processedNode} null");
                return;
            }
            if (stack.Length == 0)
            {
                Debug.Log($"{space}processedNode: {processedNode} stack.Length == 0");
                return;
            }
            var stringBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < stack.Length; i++)
                stringBuilder.AppendLine($"[{i}]: {stack[i]}");
            Debug.LogWarning($"{space}processedNode: {processedNode}\n{stringBuilder.ToString()}");
        }

        static void Dump(CSGTreeNode processedNode, List<CategoryStackNode> stack, int depth = 0)
        {
            var space = new String(' ', depth);
            if (stack == null)
            {
                Debug.Log($"{space}processedNode: {processedNode} null");
                return;
            }
            if (stack.Count == 0)
            {
                Debug.Log($"{space}processedNode: {processedNode} stack.Count == 0");
                return;
            }
            var stringBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < stack.Count; i++)
                stringBuilder.AppendLine($"[{i}]: {stack[i]}");
            Debug.LogWarning($"{space}processedNode: {processedNode}\n{stringBuilder.ToString()}");
        }

        static void Dump(CategoryStackNode[] stack, int depth = 0, string extra = "")
        {
            var space = new String(' ', depth);
            if (stack == null)
            {
                Debug.Log($"{space}---- {extra}null");
                return;
            }
            if (stack.Length == 0)
            {
                Debug.Log($"{space}---- {extra}stack.Length == 0");
                return;
            }
            var stringBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < stack.Length; i++)
                stringBuilder.AppendLine($"[{i}]: {stack[i]}");
            Debug.Log($"{space}---- {extra}\n{stringBuilder.ToString()}");
        }
#endif
    }
}
