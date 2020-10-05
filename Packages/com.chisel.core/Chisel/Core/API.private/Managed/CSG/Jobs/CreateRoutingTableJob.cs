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
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CreateRoutingTableJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public BlobAssetReference<CompactTree>          compactTree;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushes;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<RoutingTable>>   routingTableLookup;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeBitArray                    combineUsedIndices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<int>                  combineIndexRemap;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<int>                   routingSteps;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<RoutingLookup>        routingLookups;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<CategoryStackNode>    routingTable;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<CategoryStackNode>    tempStackArray;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<QueuedEvent>           queuedEvents;
        

        const int kMaxRoutesPerNode = 16; // TODO: figure out the actual possible theoretical maximum

        public void Execute(int index)
        {
            if (index >= allUpdateBrushIndexOrders.Length)
                return;

            var processedIndexOrder = allUpdateBrushIndexOrders[index];
            int processedNodeIndex  = processedIndexOrder.nodeIndex;
            int processedNodeOrder  = processedIndexOrder.nodeOrder;

            var brushesTouchedByBrush = brushesTouchedByBrushes[processedNodeOrder];
            if (brushesTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;


            ref var topDownNodes = ref compactTree.Value.topDownNodes;
            var maxNodes        = topDownNodes.Length;
            var maxRoutes       = maxNodes * kMaxRoutesPerNode;

            if (!routingTable.IsCreated || routingTable.Length < maxRoutes * 2)
            {
                if (routingTable.IsCreated) routingTable.Dispose();
                routingTable = new NativeArray<CategoryStackNode>(maxRoutes * 2, Allocator.Temp);
            }
            if (!tempStackArray.IsCreated || tempStackArray.Length < maxRoutes * 2)
            {
                if (tempStackArray.IsCreated) tempStackArray.Dispose();
                tempStackArray = new NativeArray<CategoryStackNode>(maxRoutes * 2, Allocator.Temp);
            }
            if (!queuedEvents.IsCreated)
                queuedEvents = new NativeList<QueuedEvent>(1000, Allocator.Temp);

            var categoryStackNodeCount = GetStackNodes(processedNodeIndex, ref brushesTouchedByBrush.Value, routingTable);

            int maxCounter = (int)CategoryRoutingRow.Length;
            for (int i = 0; i < categoryStackNodeCount; i++)
                maxCounter = Math.Max(maxCounter, (int)routingTable[i].input);
                
            var totalInputsSize         = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<CategoryGroupIndex>());
            var totalRoutingRowsSize    = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<CategoryRoutingRow>());
            var totalLookupsSize        = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<RoutingLookup>());
            var totalNodesSize          = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<int>());
            var totalSize               = totalInputsSize + totalRoutingRowsSize + totalLookupsSize + totalNodesSize;

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root    = ref builder.ConstructRoot<RoutingTable>();
            var routingRows = builder.Allocate(ref root.routingRows,    categoryStackNodeCount);

            if (!routingLookups.IsCreated || routingLookups.Length < maxNodes)
            {
                if (routingLookups.IsCreated) routingLookups.Dispose();
                routingLookups = new NativeArray<RoutingLookup>(maxNodes, Allocator.Temp);
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
                        routingRows[i] = routingTable[i].routingRow;
                        i++;
                    } while (i < categoryStackNodeCount && routingTable[i].nodeIndex == cutting_node_index);
                    int end_index = i;

                    routingLookups[nodeCounter] = new RoutingLookup(start_index, end_index);
                    nodeCounter++;
                }

                builder.Construct(ref root.routingLookups, routingLookups, nodeCounter);
                
                int maxNodeIndex = 0;
                for (int i = 0; i < nodeCounter; i++)
                    maxNodeIndex = math.max(maxNodeIndex, routingTable[routingLookups[i].startIndex].nodeIndex);
                
                var indexToTableIndexCount = maxNodeIndex + 1;
                var nodeIndexToTableIndex = builder.Allocate(ref root.nodeIndexToTableIndex, indexToTableIndexCount);
                for (int i = 0; i < indexToTableIndexCount; i++)
                    nodeIndexToTableIndex[i] = -1;
                for (int i = 0; i < nodeCounter; i++)
                    nodeIndexToTableIndex[routingTable[routingLookups[i].startIndex].nodeIndex] = i;
                        
                var routingTableBlob = builder.CreateBlobAssetReference<RoutingTable>(Allocator.Persistent);
                routingTableLookup[processedNodeOrder] = routingTableBlob;
            }
            //builder.Dispose(); // Temp allocated so we don't need to dispose
        }


        enum EventType : int { GetStackNode, Combine, Cleanup, ListItem }
        [StructLayout(LayoutKind.Explicit)]
        struct QueuedEvent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent GetStackNode(int currIndex, int outputStartIndex)
            {
                return new QueuedEvent
                {
                    type                = EventType.GetStackNode,
                    currIndex           = currIndex,
                    outputStartIndex    = outputStartIndex
                };
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent CleanUp(int firstIndex, int outputStartIndex)
            {
                return new QueuedEvent
                {
                    type                = EventType.Cleanup,
                    currIndex           = firstIndex,
                    outputStartIndex    = outputStartIndex
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent Combine(int currIndex, int leftHaveGoneBeyondSelf, int leftStackStartIndex, int rightStackStartIndex)
            {
                return new QueuedEvent
                {
                    type                    = EventType.Combine,
                    currIndex               = currIndex,
                    leftHaveGoneBeyondSelf  = leftHaveGoneBeyondSelf,
                    leftStackStartIndex     = leftStackStartIndex,
                    rightStackStartIndex    = rightStackStartIndex,
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent ListItem(int currIndex, int leftStackStartIndex)
            {
                return new QueuedEvent 
                {
                    type                    = EventType.ListItem,
                    currIndex               = currIndex,
                    leftStackStartIndex     = leftStackStartIndex
                };
            }

            [FieldOffset(0)] public EventType type;
            [FieldOffset(4)] public int currIndex;
            [FieldOffset(8)] public int leftHaveGoneBeyondSelf;
            [FieldOffset(12)] public int outputStartIndex;
            [FieldOffset(12)] public int leftStackStartIndex;
            [FieldOffset(16)] public int rightStackStartIndex;
        }

        public int GetStackNodes(int processedNodeIndex, [NoAlias] ref BrushesTouchedByBrush brushesTouchedByBrush, [NoAlias] NativeArray<CategoryStackNode> output)
        {
            int haveGoneBeyondSelf = 0;
            int outputLength = 0;
            queuedEvents.Add(QueuedEvent.GetStackNode(0, 0));
            ref var topDownNodes = ref compactTree.Value.topDownNodes;
            while (queuedEvents.Length > 0)
            {
                var currEvent = queuedEvents[queuedEvents.Length - 1];
                queuedEvents.Resize(queuedEvents.Length - 1, NativeArrayOptions.ClearMemory);
              
                switch (currEvent.type)
                {
                    case EventType.GetStackNode:
                    {
                        ref var currentNode  = ref topDownNodes[currEvent.currIndex];
                        var intersectionType = brushesTouchedByBrush.Get(currentNode.nodeIndex);
                        if (intersectionType == IntersectionType.NoIntersection)
                            break;

                        if (currentNode.Type == CSGNodeType.Brush)
                        {
                            if (intersectionType == IntersectionType.AInsideB) 
                            { 
                                output[outputLength] = new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.inside }; 
                                outputLength++;
                                break; 
                            }
                            if (intersectionType == IntersectionType.BInsideA) 
                            { 
                                output[outputLength] = new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.outside };
                                outputLength++;
                                break; 
                            }

                            // All surfaces of processedNode are aligned with it's own surfaces, so all categories are Aligned
                            if (processedNodeIndex == currentNode.nodeIndex)
                            {
                                haveGoneBeyondSelf = 1; // We're currently "ON" our brush
                                output[outputLength] = new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.selfAligned };
                                outputLength++;
                                break;
                            }

                            if (haveGoneBeyondSelf > 0)
                                haveGoneBeyondSelf = 2; // We're now definitely beyond our brush

                            // Otherwise return identity categories (input == output)
                            output[outputLength] = new CategoryStackNode { nodeIndex = currentNode.nodeIndex, operation = currentNode.Operation, routingRow = CategoryRoutingRow.identity };
                            outputLength++;
                            break;
                        }

                        var nodeCount = currentNode.childCount;
                        if (nodeCount == 0)
                            break;

                        // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                        var firstIndex = currentNode.childOffset;
                        var lastIndex  = firstIndex + nodeCount;
                        while (firstIndex < lastIndex && (topDownNodes[firstIndex].Operation != CSGOperationType.Additive &&
                                                          topDownNodes[firstIndex].Operation != CSGOperationType.Copy))
                            firstIndex++;
                        if ((lastIndex - firstIndex) <= 0) // no nodes left to process, nothing is visible
                            break;


                        // Note: Events are executed in reverse order, so the last one added is run first
                        var leftStackStartIndex = currEvent.outputStartIndex;
                        outputLength = leftStackStartIndex;

                        // 3. Final cleanup
                        queuedEvents.Add(QueuedEvent.CleanUp(firstIndex, currEvent.outputStartIndex));
                        for (int i = lastIndex - 1; i >= firstIndex + 1; i--)
                        {
                            // This needs to be it's own event since we need to use intermediate data to create the next event

                            // 2. Combine the left stack (previous output stack) with the right stack
                            queuedEvents.Add(QueuedEvent.ListItem(i, leftStackStartIndex));
                        }

                        // 1. Get the first stack, which gets stored in output
                        queuedEvents.Add(QueuedEvent.GetStackNode(firstIndex, leftStackStartIndex));
                        break;
                    }

                    case EventType.ListItem:
                    {
                        var leftHaveGoneBeyondSelf = haveGoneBeyondSelf;
                        var rightStackStartIndex = outputLength;
                        // Note: Events are executed in reverse order, so the last one added is run first

                        // 2. Combine the left stack (previous output stack) with the right stack
                        queuedEvents.Add(QueuedEvent.Combine(currEvent.currIndex, leftHaveGoneBeyondSelf, currEvent.leftStackStartIndex, rightStackStartIndex));

                        // 1. Add the right stack to the output stack
                        queuedEvents.Add(QueuedEvent.GetStackNode(currEvent.currIndex, rightStackStartIndex));
                        break;
                    }


                    // Combine two stacks together, currently stored behind each other in output
                    //        [left stack              ][right stack               ]  
                    // [..... leftStackStartIndex ..... rightStackStartIndex ..... ] output
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
                        }

                        // We have both a left and a right stack at this point, but we need to write in the left stack.
                        // So we move the rightStack to it's own NativeArray 
                        var rightStack = tempStackArray;
                        var rightStackLength = outputLength - currEvent.rightStackStartIndex;
                        rightStack.CopyFrom(output, currEvent.rightStackStartIndex, rightStackLength);
                        // ... and remove it from the leftStack
                        outputLength = currEvent.rightStackStartIndex;

                        Combine(output,     currEvent.leftHaveGoneBeyondSelf, currEvent.leftStackStartIndex, ref outputLength, 
                                rightStack, haveGoneBeyondSelf, rightStackLength,
                                operation);
                        break;
                    }

                    case EventType.Cleanup:
                    {
                        // Node operation is always Additive at this point, and operation would be performed against .. nothing ..
                        // Anything added with nothing is itself, so we don't need to apply an operation here.
                        if (outputLength - currEvent.outputStartIndex > 0)
                        {
                            var item = output[outputLength - 1];
                            item.operation = topDownNodes[currEvent.currIndex].Operation;
                            output[outputLength - 1] = item;
                        }
                        break;
                    }
                }
            }

            if (outputLength == 0)
            {
                output[outputLength] = new CategoryStackNode { nodeIndex = processedNodeIndex, operation = CSGOperationType.Additive, routingRow = CategoryRoutingRow.outside };
                outputLength++;
            }
#if SHOW_DEBUG_MESSAGES
            Dump(processedNodeIndex, output, outputLength);
#endif
            return outputLength;
        }



        // We combine and store the right branch with the left branch, using an operation to tie them together
        void Combine([NoAlias] NativeArray<CategoryStackNode> leftStack,  int leftHaveGoneBeyondSelf, int leftStackStart, ref int leftStackEnd,
                     [NoAlias] NativeArray<CategoryStackNode> rightStack, int rightHaveGoneBeyondSelf, int rightStackLength,
                     CSGOperationType operation)
        {
            //Debug.Assert(rightStackLength > 0);

            ref var topDownNodes = ref compactTree.Value.topDownNodes;
            
            var leftStackCount  = leftStackEnd - leftStackStart;
            var firstNode       = rightStack[0].nodeIndex;

            #region Allocation of temporaries
            int combinedLength = leftStackCount + (CategoryRoutingRow.Length * rightStackLength);
            if (!combineUsedIndices.IsCreated || combineUsedIndices.Length < combinedLength)
            {
                if (combineUsedIndices.IsCreated) combineUsedIndices.Dispose();
                combineUsedIndices = new NativeBitArray(combinedLength, Allocator.Temp);
            } else
                combineUsedIndices.Clear();

            if (!combineIndexRemap.IsCreated || combineIndexRemap.Length < combinedLength)
            {
                if (combineIndexRemap.IsCreated) combineIndexRemap.Dispose();
                combineIndexRemap = new NativeArray<int>(combinedLength, Allocator.Temp);
            }

            if (!routingSteps.IsCreated)
            {
                routingSteps = new NativeList<int>(rightStackLength, Allocator.Temp);
            } else
            {
                routingSteps.Clear(); 
                if (routingSteps.Capacity < rightStackLength * 2)
                    routingSteps.Capacity = rightStackLength * 2;
            }
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


                int startSearchRowIndex = leftStackStart + leftStackCount;
                int prevNodeIndex       = startSearchRowIndex - 1;
                if (leftStackCount == 0)
                {
                    combineUsedIndices.Set(0, true);
                    combineUsedIndices.Set(1, true);
                    combineUsedIndices.Set(2, true);
                    combineUsedIndices.Set(3, true);
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
                            combineUsedIndices.Set((int)leftStack[p].routingRow[t], true);
                    }
                }


                var outputStack         = leftStack;
                var outputStackStart    = leftStackStart;

#if HAVE_SELF_CATEGORIES
                var operationTableOffset = (int)operation;
#else
                var operationTableOffset = (leftHaveGoneBeyondSelf >= 1 && rightStackLength == 1 ?
                                            CategoryRoutingRow.RemoveOverlappingOffset : 0) +
                                            (int)operation;
#endif

                int startRightStackRowIndex = 0;
                int routingLength           = routingSteps[0];
                for (int stackIndex = 1; stackIndex < routingSteps.Length; stackIndex++)
                {
                    int routingStep             = routingSteps[stackIndex];
                    int endRightStackRowIndex   = startRightStackRowIndex + routingLength;

                    // duplicate route multiple times
                    for (int t = 0, vIndex = 0, inputRowIndex = 0, routingOffset = 0; t < CategoryRoutingRow.Length; t++, routingOffset += routingStep) // TODO: left table might not output every one of these?
                    {
                        for (var rightStackRowIndex = startRightStackRowIndex; rightStackRowIndex < endRightStackRowIndex; rightStackRowIndex++, vIndex++)
                        {
                            var routingRow = rightStack[rightStackRowIndex].routingRow + routingOffset; // Fix up routing to include offset b/c duplication
                            bool skip = !combineUsedIndices.IsSet(vIndex);
                            combineIndexRemap[vIndex] = skip ? 0 : AddRowToOutput(outputStack, ref leftStackEnd, startSearchRowIndex,
                                                                                  ref inputRowIndex, in routingRow, rightStack[rightStackRowIndex].nodeIndex, rightStack[rightStackRowIndex].operation);
                        }
                    }

                    if (prevNodeIndex >= outputStackStart)
                    {
                        RemapIndices(outputStack, combineIndexRemap, prevNodeIndex, startSearchRowIndex);
                    }

                    combineIndexRemap.ClearValues();
                    combineUsedIndices.Clear();
                    for (int p = startSearchRowIndex; p < leftStackEnd; p++)
                        for (int t = 0; t < CategoryRoutingRow.Length; t++)
                            combineUsedIndices.Set((int)outputStack[p].routingRow[t], true);

                    prevNodeIndex   = startSearchRowIndex;
                    startSearchRowIndex = leftStackEnd;
                    startRightStackRowIndex += routingLength;
                    routingLength = routingStep;
                }

                {
                    //int routingLength = routingSteps[routingSteps.Length - 1];
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
                            var skip = !combineUsedIndices.IsSet(vIndex);
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
            }
        }


        [BurstDiscard]
        static void FailureMessage()
        {
            Debug.LogError("Unity Burst Compiler is broken");
        }

        int AddRowToOutput([NoAlias] NativeArray<CategoryStackNode> outputStack, ref int outputLength, int startSearchRowIndex,
                           ref int input, [NoAlias] in CategoryRoutingRow routingRow, int nodeIndex, CSGOperationType operation)
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

        // Remap indices to new destinations, used when destination rows have been merged
        static void RemapIndices([NoAlias] NativeArray<CategoryStackNode> stack, [NoAlias] NativeArray<int> remap, int start, int last)
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

#if SHOW_DEBUG_MESSAGES
        static void Dump(int processedNodeIndex, NativeArray<CategoryStackNode> stack, int stackLength, int depth = 0)
        {
            var space = new String(' ', depth*4);
            if (stackLength == 0)
            {
                Debug.Log($"{space}processedNode: {processedNodeIndex} stack.Count == 0");
                return;
            }
            var stringBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < stackLength; i++)
            {
                if (i == 0 || stack[i - 1].nodeIndex != stack[i].nodeIndex)
                    stringBuilder.AppendLine($"  --- nodeIndex: {stack[i].nodeIndex}");
                stringBuilder.AppendLine($"\t{i,-3} -\t[{(int)stack[i].input,-3}]: {stack[i].routingRow.ToString(false)}");
            }
            Debug.LogWarning($"{space}processedNode: {processedNodeIndex}\n{stringBuilder.ToString()}");
        }
#endif
    }
}
