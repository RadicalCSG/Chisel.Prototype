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
using System.Runtime.CompilerServices;
using System.Collections;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    internal unsafe struct CreateRoutingTableJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  treeBrushIndexOrders;
        [NoAlias, ReadOnly] public BlobAssetReference<CompactTree>          compactTree;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushes;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<RoutingTable>>   routingTableLookup;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<byte>             combineUsedIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>              combineIndexRemap;
        [NativeDisableContainerSafetyRestriction] NativeList<int>               routingSteps;
        [NativeDisableContainerSafetyRestriction] NativeArray<RoutingLookup>    routingLookups;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>              nodes;
        [NativeDisableContainerSafetyRestriction] NativeArray<CategoryStackNode>    routingTable;
        [NativeDisableContainerSafetyRestriction] NativeArray<CategoryStackNode>    tempStackArray;
        [NativeDisableContainerSafetyRestriction] NativeList<QueuedEvent>           queuedEvents;
        

        const int MaxRoutesPerNode = 32; // TODO: figure out the actual possible maximum

        public void Execute(int index)
        {
            if (index >= treeBrushIndexOrders.Length)
                return;

            var processedIndexOrder = treeBrushIndexOrders[index];
            int processedNodeIndex  = processedIndexOrder.nodeIndex;
            int processedNodeOrder  = processedIndexOrder.nodeOrder;

            int categoryStackNodeCount, polygonGroupCount;
            var brushesTouchedByBrush = brushesTouchedByBrushes[processedNodeOrder];
            if (brushesTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;


            ref var topDownNodes = ref compactTree.Value.topDownNodes;
            var maxNodes        = topDownNodes.Length;
            var maxRoutes       = maxNodes * MaxRoutesPerNode;

            if (!routingTable.IsCreated || routingTable.Length < maxRoutes)
            {
                if (routingTable.IsCreated) routingTable.Dispose();
                routingTable = new NativeArray<CategoryStackNode>(maxRoutes, Allocator.Temp);
            }
            if (!tempStackArray.IsCreated || tempStackArray.Length < maxRoutes)
            {
                if (tempStackArray.IsCreated) tempStackArray.Dispose();
                tempStackArray = new NativeArray<CategoryStackNode>(maxRoutes, Allocator.Temp);
            }
            if (!queuedEvents.IsCreated)
                queuedEvents = new NativeList<QueuedEvent>(1000, Allocator.Temp);

            {
#if SHOW_DEBUG_MESSAGES
                Debug.Log($"nodeIndex: {processedNodeIndex}");
#endif
                commonData = new CommonData
                {
                    brushesTouchedByBrushAsset  = brushesTouchedByBrush,
                    maxRoutes                   = maxRoutes,
                    processedNodeIndex          = processedNodeIndex
                };
                categoryStackNodeCount = GetStackNodes(routingTable);

#if SHOW_DEBUG_MESSAGES
                if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                    Dump(processedNodeIndex, routingTable); 
#endif

                int maxCounter = (int)CategoryRoutingRow.Length;
                for (int i = 0; i < categoryStackNodeCount; i++)
                    maxCounter = Math.Max(maxCounter, (int)routingTable[i].input);
                polygonGroupCount = maxCounter + 1;
                    
                
                var totalInputsSize         = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<CategoryGroupIndex>());
                var totalRoutingRowsSize    = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<CategoryRoutingRow>());
                var totalLookupsSize        = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<RoutingLookup>());
                var totalNodesSize          = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<int>());
                var totalSize               = totalInputsSize + totalRoutingRowsSize + totalLookupsSize + totalNodesSize;

                var builder = new BlobBuilder(Allocator.Temp, totalSize);
                ref var root    = ref builder.ConstructRoot<RoutingTable>();
                var inputs      = builder.Allocate(ref root.inputs,         categoryStackNodeCount);
                var routingRows = builder.Allocate(ref root.routingRows,    categoryStackNodeCount);

                if (!routingLookups.IsCreated || routingLookups.Length < maxNodes)
                {
                    if (routingLookups.IsCreated) routingLookups.Dispose();
                    routingLookups = new NativeArray<RoutingLookup>(maxNodes, Allocator.Temp);
                }

                if (!nodes.IsCreated || nodes.Length < maxNodes)
                {
                    if (nodes.IsCreated) nodes.Dispose();
                    nodes = new NativeArray<int>(maxNodes, Allocator.Temp);
                }

                {
                    // TODO: clean up
                    int nodeCounter = 0;
                    for (int i = 0; i < categoryStackNodeCount;)
                    {
                        var cutting_node_index = routingTable[i].nodeIndex;

                        int start_index = i;
                        do
                        {
                            inputs[i]       = routingTable[i].input;
                            routingRows[i]  = routingTable[i].routingRow;
                            i++;
                        } while (i < categoryStackNodeCount && routingTable[i].nodeIndex == cutting_node_index);
                        int end_index = i;


                        nodes[nodeCounter] = cutting_node_index;
                        routingLookups[nodeCounter] = new RoutingLookup(start_index, end_index);
                        nodeCounter++;
                    }

                    builder.Construct(ref root.routingLookups,  routingLookups, nodeCounter);
                    builder.Construct(ref root.nodes,           nodes,          nodeCounter);
                        
                    var routingTableBlob = builder.CreateBlobAssetReference<RoutingTable>(Allocator.Persistent);
                    routingTableLookup[processedNodeOrder] = routingTableBlob;
                }
                builder.Dispose();
            }
        }

        [BurstDiscard]
        static void FailureMessage()
        {
            Debug.LogError("Unity Burst Compiler is broken");
        }

        // Remap indices to new destinations, used when destination rows have been merged
        static void RemapIndices(NativeArray<CategoryStackNode> stack, NativeArray<int> remap, int start, int last)
        {
#if USE_OPTIMIZATIONS
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
#endif
        }

        struct CommonData
        {
            public int                                          maxRoutes;
            public int                                          processedNodeIndex;
            public BlobAssetReference<BrushesTouchedByBrush>    brushesTouchedByBrushAsset;
        }
        [NativeDisableContainerSafetyRestriction] CommonData commonData;

        enum EventType { GetStackNode, Combine, List, ListItem }
        struct QueuedEvent
        {
            public EventType type;

            public int currIndex;

            // GetStackNode
            public int outputStartIndex;

            // List
            public int firstIndex;
            public int lastIndex;
            public int leftStackStartIndex;

            // Combine
            //public int currIndex;
            public int leftHaveGonePastSelf;
            //public int leftStackStartIndex;
            public int rightStackStartIndex;


            // ListItem
            //public int currIndex;
            //public int leftStackStartIndex;

        }

        //queuedEvents

        // TODO: rewrite in such a way that we don't rely on stack
        public int GetStackNodes(NativeArray<CategoryStackNode> output)
        {
            int haveGonePastSelf = 0;
            int outputLength = 0;
            queuedEvents.Add(new QueuedEvent
            {
                type                = EventType.GetStackNode,
                currIndex           = 0,
                outputStartIndex    = 6
            });
            ref var topDownNodes = ref compactTree.Value.topDownNodes;
            while (queuedEvents.Length > 0)
            {
                var currEvent = queuedEvents[queuedEvents.Length - 1];
                queuedEvents.Resize(queuedEvents.Length - 1, NativeArrayOptions.ClearMemory);
              
                switch (currEvent.type)
                {
                    case EventType.GetStackNode:
                    {
                        GetStack(currEvent.currIndex, ref haveGonePastSelf, output, ref outputLength, currEvent.outputStartIndex);
                        break;
                    }
                    case EventType.ListItem:
                    {
                        var leftHaveGonePastSelf = haveGonePastSelf;
                        var rightStackStartIndex = outputLength;
                        queuedEvents.Add(new QueuedEvent
                        {
                            type                    = EventType.Combine,
                            currIndex               = currEvent.currIndex,
                            leftHaveGonePastSelf    = leftHaveGonePastSelf,
                            leftStackStartIndex     = currEvent.leftStackStartIndex,
                            rightStackStartIndex    = rightStackStartIndex
                        });
                        queuedEvents.Add(new QueuedEvent
                        {
                            type                    = EventType.GetStackNode,
                            currIndex               = currEvent.currIndex,
                            outputStartIndex        = rightStackStartIndex
                        });
                        break;
                    }
                    case EventType.Combine:
                    {
                        var operation = topDownNodes[currEvent.currIndex].Operation;
                        if (operation == CSGOperationType.Invalid)
                            operation = CSGOperationType.Additive;

                        var leftCount   = currEvent.rightStackStartIndex - currEvent.leftStackStartIndex;
                        var rightCount  = outputLength - currEvent.rightStackStartIndex;
                                
                        if (leftCount == 0) // left node has a branch without children or children are not intersecting with processedNode
                        {
                            if (rightCount == 0) // right node has a branch without children or children are not intersecting with processedNode
                            {
                                // Nothing to do, both stacks are empty
                                outputLength = currEvent.leftStackStartIndex;
                                continue;
                            }
                            switch (operation)
                            {
                                case CSGOperationType.Additive:
                                case CSGOperationType.Copy:
                                {
                                    // Output stack already contains only the right stack, which is what we want
                                    continue;
                                }
                                default:
                                {
                                    // Remove both the left and rightStack, which is stored after the leftStack
                                    outputLength = currEvent.rightStackStartIndex;
                                    continue;
                                }
                            }
                        } else
                        if (rightCount == 0) // right node has a branch without children or children are not intersecting with processedNode
                        {
                            switch (operation)
                            {
                                case CSGOperationType.Additive:
                                case CSGOperationType.Copy:
                                case CSGOperationType.Subtractive:
                                {
                                    // Remove the rightStack, which is stored after the leftStack
                                    outputLength = currEvent.rightStackStartIndex;
                                    continue;
                                }
                                default:
                                {
                                    // Remove both the left and rightStack, which is stored after the leftStack
                                    outputLength = currEvent.leftStackStartIndex;
                                    continue;
                                }
                            }
                        } else
                        {
                            // Move the rightStack to it's own List and remove it from the leftStack
                            var rightStack = tempStackArray;
                            var rightStackLength = outputLength - currEvent.rightStackStartIndex;
                            rightStack.CopyFrom(output, currEvent.rightStackStartIndex, rightStackLength);
                            outputLength = currEvent.rightStackStartIndex;

                            Combine(output,     currEvent.leftHaveGonePastSelf, currEvent.leftStackStartIndex, ref outputLength, 
                                    rightStack, haveGonePastSelf, rightStackLength,
                                    operation);
                        }
                        break;
                    }
                    case EventType.List:
                    {
                        for (int i = currEvent.firstIndex + 1; i < currEvent.lastIndex; i++)
                        {
                            queuedEvents.Add(new QueuedEvent
                            {
                                type                    = EventType.ListItem,
                                currIndex               = i,
                                leftStackStartIndex     = currEvent.leftStackStartIndex
                            });
                        }
                        break;
                    }
                }

            }
            if (outputLength == 0)
            {
                output[outputLength] = new CategoryStackNode { nodeIndex = commonData.processedNodeIndex, operation = CSGOperationType.Additive, routingRow = CategoryRoutingRow.outside };
                outputLength++;
            }
            return outputLength;
        }

        void GetStack(int currIndex, ref int haveGonePastSelf, NativeArray<CategoryStackNode> output, ref int outputLength, int outputStartIndex = 0)
        {
            ref var brushesTouchedByBrush = ref commonData.brushesTouchedByBrushAsset.Value;
            ref var topDownNodes    = ref compactTree.Value.topDownNodes;
            ref var currentNode     = ref topDownNodes[currIndex];
            var intersectionType = brushesTouchedByBrush.Get(currentNode.nodeIndex);
            if (intersectionType == IntersectionType.NoIntersection)
                return;

            if (intersectionType == IntersectionType.AInsideB) 
            { 
                output[outputLength] = new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.inside }; 
                outputLength++; 
                return; 
            }
            if (intersectionType == IntersectionType.BInsideA) 
            { 
                output[outputLength] = new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.outside };
                outputLength++;
                return; 
            }

            switch (currentNode.Type)
            {
                case CSGNodeType.Brush:
                {
                    // All surfaces of processedNode are aligned with it's own surfaces, so all categories are Aligned
                    if (commonData.processedNodeIndex == currentNode.nodeIndex)
                    {
                        haveGonePastSelf = 1;
                        output[outputLength] = new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.selfAligned };
                        outputLength++;
                        return;
                    }

                    if (haveGonePastSelf > 0)
                        haveGonePastSelf = 2;

                    // Otherwise return identity categories (input == output)
                    output[outputLength] = new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.identity };
                    outputLength++;
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
                        GetStack(firstIndex, ref haveGonePastSelf, output, ref outputLength, outputStartIndex);

                        // Node operation is always Additive at this point, and operation would be performed against .. nothing ..
                        // Anything added with nothing is itself, so we don't need to apply an operation here.
                        if (outputLength - outputStartIndex > 0)
                        {
                            var item = output[outputLength - 1];
                            item.operation = topDownNodes[firstIndex].Operation;
                            output[outputLength - 1] = item;
                        }

#if SHOW_DEBUG_MESSAGES
                        if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                            Dump(output, depth, "stack return ");
#endif
                        return;
                    } else
                    {
                        var leftStackStartIndex = outputStartIndex;
                        outputLength = leftStackStartIndex;
                        GetStack(firstIndex, ref haveGonePastSelf, output, ref outputLength, leftStackStartIndex);
                        for (int i = firstIndex + 1; i < lastIndex; i++)
                        {
                            var leftHaveGonePastSelf = haveGonePastSelf;
                            var rightStackStartIndex = outputLength;
                            GetStack(i, ref haveGonePastSelf, output, ref outputLength, rightStackStartIndex);
                                

                            var operation = topDownNodes[i].Operation;
                            if (operation == CSGOperationType.Invalid)
                                operation = CSGOperationType.Additive;

                            var leftCount   = rightStackStartIndex - leftStackStartIndex;
                            var rightCount  = outputLength - rightStackStartIndex;
                                
                            if (leftCount == 0) // left node has a branch without children or children are not intersecting with processedNode
                            {
                                if (rightCount == 0) // right node has a branch without children or children are not intersecting with processedNode
                                {
                                    // Nothing to do, both stacks are empty
                                    outputLength = leftStackStartIndex;
                                    continue;
                                }
                                switch (operation)
                                {
                                    case CSGOperationType.Additive:
                                    case CSGOperationType.Copy:
                                    {
                                        // Output stack already contains only the right stack, which is what we want
                                        continue;
                                    }
                                    default:
                                    {
                                        // Remove both the left and rightStack, which is stored after the leftStack
                                        outputLength = rightStackStartIndex;
                                        continue;
                                    }
                                }
                            } else
                            if (rightCount == 0) // right node has a branch without children or children are not intersecting with processedNode
                            {
                                switch (operation)
                                {
                                    case CSGOperationType.Additive:
                                    case CSGOperationType.Copy:
                                    case CSGOperationType.Subtractive:
                                    {
                                        // Remove the rightStack, which is stored after the leftStack
                                        outputLength = rightStackStartIndex;
                                        continue;
                                    }
                                    default:
                                    {
                                        // Remove both the left and rightStack, which is stored after the leftStack
                                        outputLength = leftStackStartIndex;
                                        continue;
                                    }
                                }
                            } else
                            {
                                // Move the rightStack to it's own List and remove it from the leftStack
                                var rightStack = tempStackArray;
                                var rightStackLength = outputLength - rightStackStartIndex;
                                rightStack.CopyFrom(output, rightStackStartIndex, rightStackLength);
                                outputLength = rightStackStartIndex;

                                Combine(output,     leftHaveGonePastSelf, leftStackStartIndex, ref outputLength, 
                                        rightStack, haveGonePastSelf, rightStackLength,
                                        operation);
                            }
                            leftHaveGonePastSelf = haveGonePastSelf;
                        }
                        return;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        int AddRowToOutput(NativeArray<CategoryStackNode> outputStack, ref int outputLength, int startSearchRowIndex,
                           ref int input, in CategoryRoutingRow routingRow, int nodeIndex, CSGOperationType operation)
        {
#if USE_OPTIMIZATIONS
            for (int n = startSearchRowIndex; n < outputLength; n++)
            {
                //Debug.Assert(nodeIndex == outputStack[n].nodeIndex);
                
                // We don't want to add identical rows, so if we find one, return it's input index
                if (outputStack[n].routingRow.Equals(routingRow))
                    return (int)outputStack[n].input + 1; 
            }
#endif
            outputStack[outputLength] = new CategoryStackNode
            {
                input       = (CategoryGroupIndex)input,
                routingRow  = routingRow,
                nodeIndex   = nodeIndex,
                operation   = operation
            };
            outputLength++;
            input++;
            // NOTE: we return the input row index + 1 so 0 (uninitialized value) is invalid
            return input;
        }

        // We combine the right branch after the left branch using an operation
        void Combine(NativeArray<CategoryStackNode> leftStack,  int leftHaveGonePastSelf, int leftStackStart, ref int leftStackEnd, 
                     NativeArray<CategoryStackNode> rightStack, int rightHaveGonePastSelf, int rightStackLength,
                     CSGOperationType operation)
        {
            Debug.Assert(rightStackLength > 0);

            ref var topDownNodes = ref compactTree.Value.topDownNodes;
            
            var leftStackCount  = leftStackEnd - leftStackStart;
            var firstNode       = rightStack[0].nodeIndex;

            #region Allocation of temporaries
            int combinedLength = leftStackCount + (CategoryRoutingRow.Length * rightStackLength);
            Debug.Assert(combinedLength > 0);
            if (!combineUsedIndices.IsCreated || combineUsedIndices.Length < combinedLength)
            {
                if (combineUsedIndices.IsCreated) combineUsedIndices.Dispose();
                combineUsedIndices = new NativeArray<byte>(combinedLength, Allocator.Temp);
            }

            if (!combineIndexRemap.IsCreated || combineIndexRemap.Length < combinedLength)
            {
                if (combineIndexRemap.IsCreated) combineIndexRemap.Dispose();
                combineIndexRemap = new NativeArray<int>(combinedLength, Allocator.Temp);
            }

            if (!routingSteps.IsCreated || routingSteps.Capacity < rightStackLength)
            {
                if (routingSteps.IsCreated) routingSteps.Dispose();
                routingSteps = new NativeList<int>(rightStackLength, Allocator.Temp);
            } else
                routingSteps.Clear();
            #endregion

            {

                // Count the number of rows for unique node
                var rightNode       = firstNode;
                int counter         = 1;
                for (int r = 1; r < rightStackLength; r++)
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


                const int kFirstRow = 1;
                int startSearchRowIndex = leftStackStart + leftStackCount;
                int prevNodeIndex       = startSearchRowIndex - 1;
                if (leftStackCount == 0)
                {
                    combineUsedIndices[0] = kFirstRow;
                    combineUsedIndices[1] = kFirstRow;
                    combineUsedIndices[2] = kFirstRow;
                    combineUsedIndices[3] = kFirstRow;
                } else
                {
                    while (prevNodeIndex > leftStackStart)
                    {
                        if (leftStack[prevNodeIndex - 1].nodeIndex != leftStack[prevNodeIndex].nodeIndex)
                            break;
                        prevNodeIndex--;
                    }

                    for (int p = prevNodeIndex; p < startSearchRowIndex; p++)
                    {
                        for (int t = 0; t < CategoryRoutingRow.Length; t++)
                            combineUsedIndices[(int)leftStack[p].routingRow[t]] = kFirstRow;
                    }
                }


                var outputStack         = leftStack;
                var outputStackStart    = leftStackStart;

#if HAVE_SELF_CATEGORIES
                var operationTableOffset = (int)operation;
#else
                var operationTableOffset = (leftHaveGonePastSelf >= 1 && rightStackLength == 1 ?
                                            CategoryRoutingRow.RemoveOverlappingOffset : 0) +
                                            (int)operation;
#endif

                int startRightStackRowIndex = 0;
                for (int stackIndex = 0; stackIndex < routingSteps.Length - 1; stackIndex++)
                {
                    int routingLength           = routingSteps[stackIndex];
                    int routingStep             = routingSteps[stackIndex + 1];
                    int endRightStackRowIndex   = startRightStackRowIndex + routingLength;

                    // duplicate route multiple times
                    for (int t = 0, vIndex = 0, inputRowIndex = 0, routingOffset = 0; t < CategoryRoutingRow.Length; t++, routingOffset += routingStep) // TODO: left table might not output every one of these?
                    {
                        for (var rightStackRowIndex = startRightStackRowIndex; rightStackRowIndex < endRightStackRowIndex; rightStackRowIndex++, vIndex++)
                        {
                            var routingRow = rightStack[rightStackRowIndex].routingRow + routingOffset; // Fix up routing to include offset b/c duplication
                            bool skip = combineUsedIndices[vIndex] != kFirstRow;
                            combineIndexRemap[vIndex] = skip ? 0 : AddRowToOutput(outputStack, ref leftStackEnd, startSearchRowIndex,
                                                                                  ref inputRowIndex, in routingRow, rightStack[rightStackRowIndex].nodeIndex, rightStack[rightStackRowIndex].operation);
                        }
                    }

                    if (prevNodeIndex >= outputStackStart)
                    {
                        RemapIndices(outputStack, combineIndexRemap, prevNodeIndex, startSearchRowIndex);
                    }

                    combineIndexRemap.ClearValues();
                    combineUsedIndices.ClearValues();
                    for (int p = startSearchRowIndex; p < leftStackEnd; p++)
                        for (int t = 0; t < CategoryRoutingRow.Length; t++)
                            combineUsedIndices[(int)outputStack[p].routingRow[t]] = kFirstRow;

                    prevNodeIndex   = startSearchRowIndex;
                    startSearchRowIndex = leftStackEnd;
                    startRightStackRowIndex += routingLength;
                }

                {
                    int routingLength = routingSteps[routingSteps.Length - 1];
                    int endRightStackRowIndex = startRightStackRowIndex + routingLength;

                    // Duplicate route multiple times, bake operation into table for last node
                    for (int t = 0, vIndex = 0, inputRowIndex = 0; t < CategoryRoutingRow.Length; t++) // TODO: left table might not output every one of these?
                    {
                        var leftCategoryIndex = (CategoryIndex)t;
                        for (var rightStackRowIndex = startRightStackRowIndex; rightStackRowIndex < endRightStackRowIndex; rightStackRowIndex++, vIndex++)
                        {
                            // Fix up output of last node to include operation between last left and last right.
                            // We don't add a routingOffset here since this is last node & we don't have a destination beyond this point
                            var routingRow = new CategoryRoutingRow(operationTableOffset, leftCategoryIndex, rightStack[rightStackRowIndex].routingRow); // applies operation
                            bool skip = combineUsedIndices[vIndex] != kFirstRow;
                            combineIndexRemap[vIndex] = skip ? 0 : AddRowToOutput(outputStack, ref leftStackEnd, startSearchRowIndex, 
                                                                                  ref inputRowIndex, in routingRow, rightStack[rightStackRowIndex].nodeIndex, rightStack[rightStackRowIndex].operation);
                        }
                    }
                }

#if USE_OPTIMIZATIONS
                if (prevNodeIndex >= outputStackStart)
                {
                    RemapIndices(outputStack, combineIndexRemap, prevNodeIndex, startSearchRowIndex);

                    bool allEqual = true;
                    combineIndexRemap.ClearValues();
                    for (int i = startSearchRowIndex; i < leftStackEnd; i++)
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
                        leftStackEnd = startSearchRowIndex;
                        RemapIndices(outputStack, combineIndexRemap, prevNodeIndex, startSearchRowIndex);
                    }
                }

                // When all the paths for the first node lead to the same destination, just remove it
                int lastRemoveCount = outputStackStart;
                while (lastRemoveCount < leftStackEnd - 1 &&
                        outputStack[lastRemoveCount].nodeIndex != outputStack[lastRemoveCount + 1].nodeIndex &&
                        outputStack[lastRemoveCount].routingRow.AreAllValue(0))
                    lastRemoveCount++;
                if (lastRemoveCount > outputStackStart)
                {
                    // Unfortunately there's a Collections version out there that adds RemoveRange to NativeList, 
                    // but used (begin, end) instead of (begin, count), which is inconsistent with List<>
                    var removeCount = lastRemoveCount - outputStackStart;
                    ChiselNativeListExtensions.RemoveRange(outputStack, outputStackStart, removeCount, ref leftStackEnd);
                }
#endif

#if SHOW_DEBUG_MESSAGES
                if (processedNodeIndex == kDebugNode || kDebugNode == -1)
                    Dump(outputStack, depth);
#endif
            }
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
