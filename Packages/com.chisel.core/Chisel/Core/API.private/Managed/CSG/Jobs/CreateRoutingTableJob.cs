#define USE_OPTIMIZATIONS
//#define SHOW_DEBUG_MESSAGES 
using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    internal unsafe struct CreateRoutingTableJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  treeBrushIndexOrders;
        [NoAlias, ReadOnly] public BlobAssetReference<CompactTree>          compactTree;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushes;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<RoutingTable>>   routingTableLookup;

        const int MaxRoutesPerNode = 32; // TODO: figure out the actual possible maximum

        public void Execute(int index)
        {
            if (index >= treeBrushIndexOrders.Length)
                return;

            var processedIndexOrder = treeBrushIndexOrders[index];
            int processedNodeIndex  = processedIndexOrder.nodeIndex;
            int processedNodeOrder  = processedIndexOrder.nodeOrder;

            int categoryStackNodeCount, polygonGroupCount;
            if (!brushesTouchedByBrushes.TryGetValue(processedNodeIndex, out BlobAssetReference<BrushesTouchedByBrush> brushesTouchedByBrush))
                return;
            
            var maxNodes        = compactTree.Value.topDownNodes.Length;
            var maxRoutes       = maxNodes * MaxRoutesPerNode;
            var routingTable    = new NativeList<CategoryStackNode>(maxRoutes, Allocator.Temp);
            {
#if SHOW_DEBUG_MESSAGES
                Debug.Log($"nodeIndex: {processedNodeIndex}");
#endif
                GetStackNodes(ref compactTree.Value.topDownNodes, ref brushesTouchedByBrush.Value, processedNodeIndex, routingTable, maxRoutes);

#if SHOW_DEBUG_MESSAGES
                if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                    Dump(processedNodeIndex, routingTable); 
#endif
                categoryStackNodeCount = (int)routingTable.Length;

                int maxCounter = (int)CategoryRoutingRow.Length;
                for (int i = 0; i < categoryStackNodeCount; i++)
                    maxCounter = Math.Max(maxCounter, (int)routingTable[i].input);
                polygonGroupCount = maxCounter + 1;
                    
                
                var totalInputsSize         = 16 + (routingTable.Length * UnsafeUtility.SizeOf<CategoryGroupIndex>());
                var totalRoutingRowsSize    = 16 + (routingTable.Length * UnsafeUtility.SizeOf<CategoryRoutingRow>());
                var totalLookupsSize        = 16 + (routingTable.Length * UnsafeUtility.SizeOf<RoutingLookup>());
                var totalNodesSize          = 16 + (routingTable.Length * UnsafeUtility.SizeOf<int>());
                var totalSize               = totalInputsSize + totalRoutingRowsSize + totalLookupsSize + totalNodesSize;

                var builder = new BlobBuilder(Allocator.Temp, totalSize);
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


                        nodes[nodeCounter] = cutting_node_index;
                        routingLookups[nodeCounter] = new RoutingLookup(start_index, end_index);
                        nodeCounter++;
                    }

                    builder.Construct(ref root.routingLookups,  routingLookups, nodeCounter);
                    builder.Construct(ref root.nodes,           nodes,          nodeCounter);
                        
                    var routingTableBlob = builder.CreateBlobAssetReference<RoutingTable>(Allocator.Persistent);
                    //builder.Dispose();

                    // TODO: figure out why this sometimes returns false, without any duplicates, yet values seem to exist??
                    routingTableLookup[processedNodeOrder] = routingTableBlob;
                    //FailureMessage();
                }
            }
            //routingTable.Dispose();
        }

        [BurstDiscard]
        static void FailureMessage()
        {
            Debug.LogError("Unity Burst Compiler is broken");
        }

        // Remap indices to new destinations, used when destination rows have been merged
        static void RemapIndices(NativeArray<CategoryStackNode> stack, NativeArray<int> remap, int start, int last)
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
        public static void GetStackNodes(ref BlobArray<CompactTopDownNode> topDownNodes, ref BrushesTouchedByBrush brushesTouchedByBrush, int processedNodeIndex, NativeList<CategoryStackNode> output, int maxRoutes)
        {
            int haveGonePastSelf = 0;
            output.Clear();
            GetStack(ref topDownNodes, ref brushesTouchedByBrush, processedNodeIndex, ref topDownNodes[0], ref haveGonePastSelf, output, maxRoutes, 0);
            if (output.Length == 0)
                output.AddNoResize(new CategoryStackNode { nodeIndex = processedNodeIndex, operation = CSGOperationType.Additive, routingRow = CategoryRoutingRow.outside });
        }

        static void GetStack(ref BlobArray<CompactTopDownNode> topDownNodes, ref BrushesTouchedByBrush brushesTouchedByBrush, int processedNodeIndex, ref CompactTopDownNode currentNode, ref int haveGonePastSelf, NativeList<CategoryStackNode> output, int maxRoutes, int depth)
        {
            var intersectionType = brushesTouchedByBrush.Get(currentNode.nodeIndex);
            if (intersectionType == IntersectionType.NoIntersection)
                return;


            // TODO: use other intersection types
            if (intersectionType == IntersectionType.AInsideB) { output.AddNoResize(new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.inside }); return; }
            if (intersectionType == IntersectionType.BInsideA) { output.AddNoResize(new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.outside }); return; }

            switch (currentNode.Type)
            {
                case CSGNodeType.Brush:
                {
                    // All surfaces of processedNode are aligned with it's own surfaces, so all categories are Aligned
                    if (processedNodeIndex == currentNode.nodeIndex)
                    {
                        haveGonePastSelf = 1;
                        output.AddNoResize(new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.selfAligned } );
                        return;
                    }

                    if (haveGonePastSelf > 0)
                        haveGonePastSelf = 2;

                    // Otherwise return identity categories (input == output)
                    output.AddNoResize(new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.identity });
                    return;
                }
                default:
                {
                    var nodeCount = currentNode.childCount;
                    if (nodeCount == 0)
                        return;

                    // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                    var firstIndex = currentNode.childOffset;
                    var lastIndex = firstIndex + nodeCount;
                    for (; firstIndex < lastIndex && (topDownNodes[firstIndex].Operation != CSGOperationType.Additive &&
                                                      topDownNodes[firstIndex].Operation != CSGOperationType.Copy); firstIndex++)
                        firstIndex++;

                    if ((lastIndex - firstIndex) <= 0)
                        return;

                    if ((lastIndex - firstIndex) == 1)
                    {
                        GetStack(ref topDownNodes, ref brushesTouchedByBrush, processedNodeIndex, ref topDownNodes[firstIndex], ref haveGonePastSelf, output, maxRoutes, depth + 1);

                        // Node operation is always Additive at this point, and operation would be performed against .. nothing ..
                        // Anything added with nothing is itself, so we don't need to apply an operation here.

                        if (output.Length > 0)
                        {
                            var item = output[output.Length - 1];
                            item.operation = topDownNodes[firstIndex].Operation;
                            output[output.Length - 1] = item;
                        }

#if SHOW_DEBUG_MESSAGES
                        if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                            Dump(output, depth, "stack return ");
#endif
                        return;
                    } else
                    {
                        var leftHaveGonePastSelf = 0;

                        var leftStack   = output;
                        var rightStack  = new NativeList<CategoryStackNode>(maxRoutes, Allocator.Temp); // TODO: get rid of allocation, store rightStack after leftStack and duplicate it -> then optimize
                        {
                            leftStack.Clear();
                            GetStack(ref topDownNodes, ref brushesTouchedByBrush, processedNodeIndex, ref topDownNodes[firstIndex], ref leftHaveGonePastSelf, leftStack, maxRoutes, depth + 1);
                            haveGonePastSelf |= leftHaveGonePastSelf;
                            for (int i = firstIndex + 1; i < lastIndex; i++)
                            {
#if SHOW_DEBUG_MESSAGES
                                if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                                    Dump(leftStack, depth, $"before '{topDownNodes[i - 1].nodeIndex}' {topDownNodes[i].Operation} '{topDownNodes[i].nodeIndex}'");
#endif
                                var rightHaveGonePastSelf = leftHaveGonePastSelf >= 1 ? 2 : 0;
                                rightStack.Clear();
                                GetStack(ref topDownNodes, ref brushesTouchedByBrush, processedNodeIndex, ref topDownNodes[i], ref rightHaveGonePastSelf, rightStack, maxRoutes, depth + 1);
                                haveGonePastSelf |= rightHaveGonePastSelf;

                                Combine(ref topDownNodes,
                                        ref brushesTouchedByBrush,
                                        processedNodeIndex,
                                        leftStack,
                                        leftHaveGonePastSelf,
                                        rightStack,
                                        rightHaveGonePastSelf,
                                        topDownNodes[i].Operation,
                                        depth + 1
                                );
                                leftHaveGonePastSelf = rightHaveGonePastSelf;

                                //if (leftStack.Length > 0 && node.Operation == CSGOperationType.Copy)
                                //    leftStack[leftStack.Length - 1].operation = node.Operation;

#if SHOW_DEBUG_MESSAGES
                                if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                                    Dump(leftStack, depth, $"after '{topDownNodes[i - 1].nodeIndex}' {topDownNodes[i].Operation} '{topDownNodes[i].nodeIndex}'");
#endif
                            }
                        }
                        //rightStack.Dispose();
                        return;
                    }
                }
            }
        }

        // We combine the right branch after the left branch using an operation
        static void Combine(ref BlobArray<CompactTopDownNode> topDownNodes, ref BrushesTouchedByBrush brushesTouchedByBrush, int processedNodeIndex, NativeList<CategoryStackNode> leftStack, int leftHaveGonePastSelf, NativeList<CategoryStackNode> rightStack, int rightHaveGonePastSelf, CSGOperationType operation, int depth)
        {
            if (operation == CSGOperationType.Invalid)
                operation = CSGOperationType.Additive;

            if (leftStack.Length == 0) // left node has a branch without children or children are not intersecting with processedNode
            {
                if (rightStack.Length == 0) // right node has a branch without children or children are not intersecting with processedNode
                {
                    leftStack.Clear(); return;
                }
                switch (operation)
                {
                    case CSGOperationType.Additive:
                    case CSGOperationType.Copy: leftStack.Clear(); leftStack.AddRangeNoResize(rightStack); return; //rightStack;
                    default: leftStack.Clear(); return;
                }
            } else
            if (rightStack.Length == 0) // right node has a branch without children or children are not intersecting with processedNode
            {
                switch (operation)
                {
                    case CSGOperationType.Additive:
                    case CSGOperationType.Copy:
                    case CSGOperationType.Subtractive: return; //leftStack
                    default: leftStack.Clear(); return;
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
                        routingSteps.AddNoResize(counter);
                        counter = 0;
                        rightNode = rightStack[r].nodeIndex;
                    }
                    counter++;
                }
                routingSteps.AddNoResize(counter);


                int prevNodeIndex = leftStack.Length - 1;
                while (prevNodeIndex > 0)
                {
                    if (prevNodeIndex <= 0 ||
                        leftStack[prevNodeIndex - 1].nodeIndex != leftStack[prevNodeIndex].nodeIndex)
                        break;
                    prevNodeIndex--;
                }
                int startNodeIndex = leftStack.Length;

                var lastLeftNodeStart   = prevNodeIndex;
                var lastLeftNodeEnd     = startNodeIndex;

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


                var outputStack = leftStack;

#if HAVE_SELF_CATEGORIES
                var operationTableOffset = (int)operation;
#else
                var operationTableOffset = (leftHaveGonePastSelf >= 1 && rightStack.Length == 1 ?
                                            CategoryRoutingRow.RemoveOverlappingOffset : 0) +
                                            (int)operation;
#endif
                bool haveRemap = false;

                int stackIndex = 1;
                rightNode = firstNode;
                for (int rOffset = 0; rOffset < rightStack.Length; rOffset++)
                {
                    if (rightNode != rightStack[rOffset].nodeIndex)
                    {
#if USE_OPTIMIZATIONS
                        if (prevNodeIndex >= 0 && haveRemap && combineIndexRemap.Length > 0)
                        {
                            RemapIndices(outputStack, combineIndexRemap, prevNodeIndex, startNodeIndex);
#if SHOW_DEBUG_MESSAGES
                            if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                                Dump(rightStack, depth);
#endif
                        }
#endif

                        prevNodeIndex   = startNodeIndex;
                        startNodeIndex  = outputStack.Length;
                        index = 0; vIndex = 0; stackIndex++;
                        rightNode = rightStack[rOffset].nodeIndex;
                        //sCombineIndexRemap = null;
                        //sCombineIndexRemap.Clear();

                        combineUsedIndices.ClearValues();
                        for (int p = prevNodeIndex; p < startNodeIndex; p++)
                        {
                            for (int t = 0; t < CategoryRoutingRow.Length; t++)
                                combineUsedIndices[(int)outputStack[p].routingRow[t]] = 1;
                        }
                        combineIndexRemap.ClearValues();
                    }

                    CategoryRoutingRow routingRow;
                    int routingOffset = 0;
                    if (stackIndex >= routingSteps.Length) // last node in right stack
                    {
                        int ncount = 0;
                        var startR = rOffset;
                        rOffset = rightStack.Length;

                        // Duplicate route multiple times, bake operation into table for last node
                        for (int t = 0; t < CategoryRoutingRow.Length; t++) // TODO: left table might not output every one of these?
                        {
                            var leftCategoryIndex = (CategoryIndex)t;

                            for (var r = startR; r < rightStack.Length; r++)
                            {
                                var rightInput = rightStack[r].routingRow;

                                // Fix up output of last node to include operation between last left and last right.
                                // We don't add a routingOffset here since this is last node & we don't have a destination beyond this point
                                routingRow = new CategoryRoutingRow(operationTableOffset, leftCategoryIndex, rightInput); // applies operation

                                int foundIndex = -1;
#if USE_OPTIMIZATIONS
                                if (vIndex < combineUsedIndices.Length &&
                                    combineUsedIndices[vIndex] == 1)
#endif
                                {
#if USE_OPTIMIZATIONS
                                    for (int n = startNodeIndex; n < outputStack.Length; n++)
                                    {
                                        Debug.Assert(rightStack[r].nodeIndex == outputStack[n].nodeIndex);
                                        if (outputStack[n].routingRow.Equals(routingRow))
                                        {
                                            foundIndex = (int)outputStack[n].input;
                                            break;
                                        }
                                    }
#endif
                                    if (foundIndex == -1)
                                    {
                                        outputStack.AddNoResize(new CategoryStackNode
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
                        if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                        {
                            //Debug.Log($"[{startR}/{rightStack.Length}] {ncount} {vIndex} / {sCombineIndexRemap.Count} {rightStack[startR].node} {rightStack.Length - startR} +");
                            Dump(rightStack, depth);
                        }
#endif

                    } else
                    {
                        int ncount = 0;
                        var startR = rOffset;

                        int routingLength = routingSteps[stackIndex - 1];
                        rOffset += routingLength - 1;
                        
                        int routingStep = routingSteps[stackIndex];

                        // Duplicate route multiple times
                        for (int t = 0; t < CategoryRoutingRow.Length; t++, routingOffset += routingStep) // TODO: left table might not output every one of these?
                        {
                            for (var r = startR; r < startR + routingLength; r++)
                            {
                                var rightInput = rightStack[r].routingRow;
                                //if (rightKeepContents)
                                //    rightInput[0] = rightInput[(int)CategoryIndex.Outside];

                                // Fix up routing to include offset b/c duplication
                                routingRow = rightInput + routingOffset;

                                int foundIndex = -1;
#if USE_OPTIMIZATIONS
                                if (vIndex < combineUsedIndices.Length &&
                                    combineUsedIndices[vIndex] == 1)
#endif
                                {
#if USE_OPTIMIZATIONS
                                    for (int n = startNodeIndex; n < outputStack.Length; n++)
                                    {
                                        Debug.Assert(rightStack[r].nodeIndex == outputStack[n].nodeIndex);
                                        if (outputStack[n].routingRow.Equals(routingRow))
                                        {
                                            foundIndex = (int)outputStack[n].input;
                                            break;
                                        }
                                    }
#endif
                                    if (foundIndex == -1)
                                    {
                                        outputStack.AddNoResize(new CategoryStackNode
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
                        if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                        {
                            //Debug.Log($"[{r}/{rightStack.Length}] {ncount} {vIndex} / {sCombineIndexRemap.Count} {rightStack[r].node} -");
                            Dump(rightStack, depth);
                        }
#endif
                    }
                }

#if USE_OPTIMIZATIONS
                if (//nodeIndex > 1 && 
                    prevNodeIndex >= 0 && haveRemap && combineIndexRemap.Length > 0)
                {
                    RemapIndices(outputStack, combineIndexRemap, prevNodeIndex, startNodeIndex);
#if SHOW_DEBUG_MESSAGES
                    if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                        Dump(outputStack, depth);
#endif
                    bool allEqual = true;
                    combineIndexRemap.ClearValues();
                    for (int i = startNodeIndex; i < outputStack.Length; i++)
                    {
                        if (!outputStack[i].routingRow.AreAllTheSame())
                        {
                            allEqual = false;
                            break;
                        }
                        combineIndexRemap[(int)outputStack[i].input] = ((int)outputStack[i].routingRow[0]) + 1;
                    }
                    if (allEqual)
                    {
                        // Unfortunately there's a Collections version out there that adds RemoveRange to NativeList, 
                        // but used (begin, end) instead of (begin, count), which is inconsistent with List<>
                        ChiselNativeListExtensions.RemoveRange(outputStack, startNodeIndex, outputStack.Length - startNodeIndex);
                        RemapIndices(outputStack, combineIndexRemap, prevNodeIndex, startNodeIndex);

#if SHOW_DEBUG_MESSAGES
                        if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                            Dump(outputStack, depth);
#endif
                    }
                }

                // When all the paths for the first node lead to the same destination, just remove it
                int firstRemoveCount = 0;
                while (firstRemoveCount < outputStack.Length - 1 &&
                        outputStack[firstRemoveCount].nodeIndex != outputStack[firstRemoveCount + 1].nodeIndex &&
                        outputStack[firstRemoveCount].routingRow.AreAllValue(0))
                    firstRemoveCount++;
                if (firstRemoveCount > 0)
                {
                    // Unfortunately there's a Collections version out there that adds RemoveRange to NativeList, 
                    // but used (begin, end) instead of (begin, count), which is inconsistent with List<>
                    ChiselNativeListExtensions.RemoveRange(outputStack, 0, firstRemoveCount);
                }
#endif

#if SHOW_DEBUG_MESSAGES
                if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                    Dump(outputStack, depth);
#endif
            }
            //combineUsedIndices.Dispose();
            //combineIndexRemap.Dispose();
        }

#if SHOW_DEBUG_MESSAGES
        static int kDebugNode = -1; 

        static void Dump(int processedNodeIndex, NativeList<CategoryStackNode> stack, int depth = 0)
        {
            var space = new String(' ', depth*4);
            if (stack.Length == 0)
            {
                Debug.Log($"{space}processedNode: {processedNodeIndex} stack.Count == 0");
                return;
            }
            var stringBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < stack.Length; i++)
            {
                if (i == 0 || stack[i - 1].nodeIndex != stack[i].nodeIndex)
                    stringBuilder.AppendLine($"  --- nodeIndex: {stack[i].nodeIndex}");
                stringBuilder.AppendLine($"\t{i,-3} -\t[{(int)stack[i].input,-3}]: {stack[i].routingRow.ToString(false)}");
            }
            Debug.LogWarning($"{space}processedNode: {processedNodeIndex}\n{stringBuilder.ToString()}");
        }

        static void Dump(NativeList<CategoryStackNode> stack, int depth = 0, string extra = "")
        {
            var space = new String(' ', depth*4);            
            if (stack.Length == 0)
            {
                Debug.Log($"{space}---- {extra}stack.Length == 0");
                return;
            }
            var stringBuilder = new System.Text.StringBuilder();
            var lastNodeIndex = stack[stack.Length - 1].nodeIndex;
            for (int i = 0; i < stack.Length; i++)
            {
                if (i == 0 || stack[i - 1].nodeIndex != stack[i].nodeIndex)
                    stringBuilder.AppendLine($"  --- nodeIndex: {stack[i].nodeIndex}");
                stringBuilder.AppendLine($"\t{i,-3} -\t[{(int)stack[i].input,-3}]: {stack[i].routingRow.ToString(stack[i].nodeIndex == lastNodeIndex)}");
            }
            Debug.Log($"{space}---- {extra}\n{stringBuilder.ToString()}");
        }
#endif
    }
}
