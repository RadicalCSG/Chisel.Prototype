#define USE_OPTIMIZATIONS
//#define SHOW_DEBUG_MESSAGES 
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Debug = UnityEngine.Debug;

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
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<QueuedEvent>         queuedEvents;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<CategoryStackNode>   tempStackArray;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeBitArray                   combineUsedIndices;
#if USE_OPTIMIZATIONS
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<int>                 combineIndexRemap;
#endif
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<int>                 routingSteps;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<CategoryStackNode>   routingTable;
        

        const int kMaxRoutesPerNode = 16; // TODO: figure out the actual possible theoretical maximum

        public void Execute(int index)
        {
            if (index >= allUpdateBrushIndexOrders.Length)
                return;

            var processedIndexOrder = allUpdateBrushIndexOrders[index];
            var processedNodeID     = processedIndexOrder.compactNodeID;
            int processedNodeOrder  = processedIndexOrder.nodeOrder;

            var brushesTouchedByBrush = brushesTouchedByBrushes[processedNodeOrder];
            if (brushesTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                return;

            ref var topDownNodes = ref compactTree.Value.compactHierarchy;
            ref var brushesTouchedByBrushValue = ref brushesTouchedByBrush.Value;
            var maxNodes    = math.max(1, brushesTouchedByBrushValue.brushIntersections.Length);
            var maxRoutes   = maxNodes * kMaxRoutesPerNode;

            NativeCollectionHelpers.EnsureMinimumSize(ref routingTable, maxRoutes);
            NativeCollectionHelpers.EnsureMinimumSize(ref tempStackArray, maxRoutes);
            NativeCollectionHelpers.EnsureMinimumSize(ref queuedEvents, 4096);
            NativeCollectionHelpers.EnsureMinimumSize(ref routingSteps, maxRoutes);
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref combineUsedIndices, maxRoutes);
#if USE_OPTIMIZATIONS
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref combineIndexRemap, maxRoutes);
#endif

            var categoryStackNodeCount = GetStackNodes(processedNodeID, ref brushesTouchedByBrushValue, 
                                                       ref routingTable,
                                                       ref compactTree.Value.compactHierarchy,
                                                       ref queuedEvents,
                                                       ref tempStackArray,
                                                       ref combineUsedIndices,
#if USE_OPTIMIZATIONS
                                                       ref combineIndexRemap,
#endif
                                                       ref routingSteps);

            var totalInputsSize         = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<CategoryGroupIndex>());
            var totalRoutingRowsSize    = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<CategoryRoutingRow>());
            var totalLookupsSize        = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<RoutingLookup>());
            var totalNodesSize          = 16 + (categoryStackNodeCount * UnsafeUtility.SizeOf<int>());
            var totalSize               = totalInputsSize + totalRoutingRowsSize + totalLookupsSize + totalNodesSize;

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root    = ref builder.ConstructRoot<RoutingTable>();
            var routingRows = builder.Allocate(ref root.routingRows,    categoryStackNodeCount);

            // TODO: clean up
            int nodeCounter = 1;
            routingRows[0] = routingTable[0].routingRow;
            var prevNodeID = routingTable[0].NodeIDValue;
            for (int i = 1; i < categoryStackNodeCount; i++)
            {
                routingRows[i] = routingTable[i].routingRow;
                var curNodeID = routingTable[i].NodeIDValue;
                if (prevNodeID != curNodeID)
                    nodeCounter++;
                prevNodeID = curNodeID;
            }

            var routingLookups = builder.Allocate(ref root.routingLookups, nodeCounter);

            {
                // TODO: clean up
                nodeCounter = 0;
                for (int i = 0; i < categoryStackNodeCount;)
                {
                    var cuttingNodeID = routingTable[i].NodeIDValue;
                    int startIndex = i;
                    i++;
                    while (i < categoryStackNodeCount && routingTable[i].NodeIDValue == cuttingNodeID)
                        i++;
                    int endIndex = i;

                    routingLookups[nodeCounter] = new RoutingLookup { startIndex = startIndex, endIndex = endIndex };
                    nodeCounter++;
                }

                int maxNodeID = 0;
                int minNodeID = 0;
                for (int i = 0; i < nodeCounter; i++)
                {
                    var NodeID = routingTable[routingLookups[i].startIndex].NodeIDValue;
                    minNodeID = math.min(minNodeID, NodeID);
                    maxNodeID = math.max(maxNodeID, NodeID);
                }
                root.nodeIDOffset = minNodeID;

                var indexToTableIndexCount = (maxNodeID + 1) - minNodeID;
                var nodeIDToTableIndex = builder.Allocate(ref root.nodeIDToTableIndex, indexToTableIndexCount);
                for (int i = 0; i < indexToTableIndexCount; i++)
                    nodeIDToTableIndex[i] = -1;
                for (int i = 0; i < nodeCounter; i++)
                    nodeIDToTableIndex[routingTable[routingLookups[i].startIndex].NodeIDValue - minNodeID] = i;
                        
                var routingTableBlob = builder.CreateBlobAssetReference<RoutingTable>(Allocator.Persistent);
                routingTableLookup[processedNodeOrder] = routingTableBlob;
            }
            //builder.Dispose(); // Temp allocated so we don't need to dispose
        }


        enum EventType : int { GetStackNode, Combine, ListItem }
        [StructLayout(LayoutKind.Explicit)]
        struct QueuedEvent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent GetStackNode(int currIndex, int outputStartIndex, IntersectionType intersectionType)
            {
                return new QueuedEvent
                {
                    type                = EventType.GetStackNode,
                    currIndex           = currIndex,
                    intersectionType    = intersectionType,
                    outputStartIndex    = outputStartIndex
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QueuedEvent ListItem(int currIndex, int leftStackStartIndex, IntersectionType intersectionType)
            {
                return new QueuedEvent 
                {
                    type                = EventType.ListItem,
                    currIndex           = currIndex,
                    intersectionType    = intersectionType,
                    leftStackStartIndex = leftStackStartIndex
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

            [FieldOffset(0)] public EventType type;
            [FieldOffset(4)] public int currIndex;
            [FieldOffset(8)] public int leftHaveGoneBeyondSelf;
            [FieldOffset(8)] public IntersectionType intersectionType;
            [FieldOffset(12)] public int outputStartIndex;
            [FieldOffset(12)] public int leftStackStartIndex;
            [FieldOffset(16)] public int rightStackStartIndex;
        }

        static int GetStackNodes(CompactNodeID processedNodeID, 
                                 [NoAlias] ref BrushesTouchedByBrush            brushesTouchedByBrush, 
                                 [NoAlias] ref NativeArray<CategoryStackNode>   output,
                                 [NoAlias] ref BlobArray<CompactHierarchyNode>  compactHierarchy,
                                 [NoAlias] ref NativeArray<QueuedEvent>         queuedEvents,
                                 [NoAlias] ref NativeArray<CategoryStackNode>   tempStackArray,
                                 [NoAlias] ref NativeBitArray                   combineUsedIndices,
#if USE_OPTIMIZATIONS
                                 [NoAlias] ref NativeArray<int>                 combineIndexRemap,
#endif
                                 [NoAlias] ref NativeArray<int>                 routingSteps)
        {
            int haveGoneBeyondSelf = 0;
            int outputLength = 0;
            int queuedEventCount = 0;
            queuedEvents[0] = QueuedEvent.GetStackNode(0, 0, brushesTouchedByBrush.Get(compactHierarchy[0].nodeID));
            queuedEventCount++;
            //ref var compactHierarchy = ref compactTree.Value.compactHierarchy;
            while (queuedEventCount > 0)
            {
                var currEvent = queuedEvents[queuedEventCount - 1];
                queuedEventCount--;
              
                switch (currEvent.type)
                {
                    case EventType.GetStackNode:
                    {
                        var intersectionType = currEvent.intersectionType;
                        if (intersectionType == IntersectionType.NoIntersection ||
                            intersectionType == IntersectionType.InvalidValue)
                            break;

                        ref var currentNode = ref compactHierarchy[currEvent.currIndex];
                        var currentNodeID = currentNode.nodeID;
                        if (currentNode.Type == CSGNodeType.Brush)
                        {
                            if (intersectionType == IntersectionType.AInsideB) 
                            { 
                                output[outputLength] = new CategoryStackNode { NodeIDValue = currentNodeID.value, routingRow = CategoryRoutingRow.inside }; 
                                outputLength++;
                                break; 
                            }
                            if (intersectionType == IntersectionType.BInsideA) 
                            { 
                                output[outputLength] = new CategoryStackNode { NodeIDValue = currentNodeID.value, routingRow = CategoryRoutingRow.outside };
                                outputLength++;
                                break; 
                            }

                            // All surfaces of processedNode are aligned with it's own surfaces, so all categories are Aligned
                            if (processedNodeID == currentNode.nodeID)
                            {
                                haveGoneBeyondSelf = 1; // We're currently "ON" our brush
                                output[outputLength] = new CategoryStackNode { NodeIDValue = currentNodeID.value, routingRow = CategoryRoutingRow.selfAligned };
                                outputLength++;
                                break;
                            }

                            if (haveGoneBeyondSelf > 0)
                                haveGoneBeyondSelf = 2; // We're now definitely beyond our brush

                            // Otherwise return identity categories (input == output)
                            output[outputLength] = new CategoryStackNode { NodeIDValue = currentNodeID.value, routingRow = CategoryRoutingRow.identity };
                            outputLength++;
                            break;
                        }

                        var nodeCount = currentNode.childCount;
                        if (nodeCount == 0)
                            break;

                        // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                        var firstIndex = currentNode.childOffset;
                        var lastIndex  = firstIndex + nodeCount;
                        while (firstIndex < lastIndex && (compactHierarchy[firstIndex].Operation != CSGOperationType.Additive &&
                                                          compactHierarchy[firstIndex].Operation != CSGOperationType.Copy))
                            firstIndex++;

                        if ((lastIndex - firstIndex) <= 0) // no nodes left to process, nothing is visible
                            break;


                        // Note: Events are executed in reverse order, so the last one added is run first
                        var leftStackStartIndex = currEvent.outputStartIndex;
                        outputLength = leftStackStartIndex;

                        for (int i = lastIndex - 1; i >= firstIndex + 1; i--)
                        {
                            // This needs to be it's own event since we need to use intermediate data to create the next event

                            // 2. Combine the left stack (previous output stack) with the right stack
                            ref var childNode   = ref compactHierarchy[i];
                            var childNodeID     = childNode.nodeID;
                            var childIntersectionType = brushesTouchedByBrush.Get(childNodeID);
                            if (childIntersectionType != IntersectionType.NoIntersection &&
                                childIntersectionType != IntersectionType.InvalidValue)
                            {
                                queuedEvents[queuedEventCount] = QueuedEvent.ListItem(i, leftStackStartIndex, childIntersectionType);
                                queuedEventCount++;
                            }
                        }

                        // 1. Get the first stack, which gets stored in output
                        ref var firstChildNode = ref compactHierarchy[firstIndex];
                        var firstChildNodeID = firstChildNode.nodeID;
                        var firstChildIntersectionType = brushesTouchedByBrush.Get(firstChildNodeID);
                        if (firstChildIntersectionType != IntersectionType.NoIntersection &&
                            firstChildIntersectionType != IntersectionType.InvalidValue)
                        {
                            queuedEvents[queuedEventCount] = QueuedEvent.GetStackNode(firstIndex, leftStackStartIndex, firstChildIntersectionType);
                            queuedEventCount++;
                        }
                        break;
                    }

                    case EventType.ListItem:
                    {
                        var leftHaveGoneBeyondSelf = haveGoneBeyondSelf;
                        var rightStackStartIndex = outputLength;
                        // Note: Events are executed in reverse order, so the last one added is run first

                        // 2. Combine the left stack (previous output stack) with the right stack
                        queuedEvents[queuedEventCount] = QueuedEvent.Combine(currEvent.currIndex, leftHaveGoneBeyondSelf, currEvent.leftStackStartIndex, rightStackStartIndex);
                        queuedEventCount++;

                        // 1. Add the right stack to the output stack
                        queuedEvents[queuedEventCount] = QueuedEvent.GetStackNode(currEvent.currIndex, rightStackStartIndex, currEvent.intersectionType);
                        queuedEventCount++;
                        break;
                    }


                    // Combine two stacks together, currently stored behind each other in output
                    //        [left stack              ][right stack               ]  
                    // [..... leftStackStartIndex ..... rightStackStartIndex ..... ] output
                    case EventType.Combine:
                    {
                        var operation = compactHierarchy[currEvent.currIndex].Operation;
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

                        Combine(ref output,     currEvent.leftHaveGoneBeyondSelf, currEvent.leftStackStartIndex, ref outputLength, 
                                ref rightStack, haveGoneBeyondSelf, rightStackLength,
                                operation,
                                ref compactHierarchy, 
                                ref combineUsedIndices,
#if USE_OPTIMIZATIONS
                                ref combineIndexRemap,
#endif
                                ref routingSteps);
                        break;
                    }
                }
            }

            if (outputLength == 0)
            {
                output[outputLength] = new CategoryStackNode { NodeIDValue = processedNodeID.value, routingRow = CategoryRoutingRow.outside };
                outputLength++;
            }
#if SHOW_DEBUG_MESSAGES
            Dump(processedNodeID, output, outputLength);
#endif
            return outputLength;
        }



        // We combine and store the right branch with the left branch, using an operation to tie them together
        static void Combine([NoAlias] ref NativeArray<CategoryStackNode> leftStack, int leftHaveGoneBeyondSelf, int leftStackStart, ref int leftStackEnd,
                            [NoAlias] ref NativeArray<CategoryStackNode> rightStack, int rightHaveGoneBeyondSelf, int rightStackLength,
                            CSGOperationType operation,
                            [NoAlias] ref BlobArray<CompactHierarchyNode>   compactHierarchy,
                            [NoAlias] ref NativeBitArray                    combineUsedIndices,
#if USE_OPTIMIZATIONS
                            [NoAlias] ref NativeArray<int>                  combineIndexRemap,
#endif
                            [NoAlias] ref NativeArray<int>                  routingSteps)
        {
            //Debug.Assert(rightStackLength > 0);

            var leftStackCount  = leftStackEnd - leftStackStart;
            var firstNodeID     = rightStack[0].NodeIDValue;

            int routingStepsLength = 0;
            {
                // Count the number of rows for unique node
                var rightNodeID = firstNodeID;
                int counter     = 1;
                for (int r = 1; r < rightStackLength; r++)
                {
                    if (rightNodeID != rightStack[r].NodeIDValue)
                    {
                        routingSteps[routingStepsLength] = counter;
                        routingStepsLength++;
                        counter = 0;
                        rightNodeID = rightStack[r].NodeIDValue;
                    }
                    counter++;
                }
                routingSteps[routingStepsLength] = counter;
                routingStepsLength++;


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
                        if (leftStack[prevNodeIndex - 1].NodeIDValue != leftStack[prevNodeIndex].NodeIDValue)
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
                for (int stackIndex = 1; stackIndex < routingStepsLength; stackIndex++)
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
#if USE_OPTIMIZATIONS
                            combineIndexRemap[vIndex] = skip ? 0 : 
#endif
                                AddRowToOutput(outputStack, ref leftStackEnd, startSearchRowIndex,
                                               ref inputRowIndex, in routingRow, rightStack[rightStackRowIndex].NodeIDValue);
                        }
                    }

#if USE_OPTIMIZATIONS
                    if (prevNodeIndex >= outputStackStart)
                    {
                        RemapIndices(outputStack, combineIndexRemap, prevNodeIndex, startSearchRowIndex);
                    }

                    combineIndexRemap.ClearValues();
                    combineUsedIndices.Clear();
                    for (int p = startSearchRowIndex; p < leftStackEnd; p++)
                        for (int t = 0; t < CategoryRoutingRow.Length; t++)
                            combineUsedIndices.Set((int)outputStack[p].routingRow[t], true);
#endif

                    prevNodeIndex   = startSearchRowIndex;
                    startSearchRowIndex = leftStackEnd;
                    startRightStackRowIndex += routingLength;
                    routingLength = routingStep;
                }

                {
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
#if USE_OPTIMIZATIONS
                            combineIndexRemap[vIndex] = skip ? 0 : 
#endif
                                AddRowToOutput(outputStack, ref leftStackEnd, startSearchRowIndex, 
                                               ref inputRowIndex, in routingRow, rightStack[rightStackRowIndex].NodeIDValue);
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
                        combineIndexRemap[(int)outputStack[i].Input] = ((int)outputStack[i].routingRow[0]) + 1;
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
                        outputStack[lastRemoveCount].NodeIDValue != outputStack[lastRemoveCount + 1].NodeIDValue &&
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

        static int AddRowToOutput([NoAlias] NativeArray<CategoryStackNode> outputStack, ref int outputLength, int startSearchRowIndex,
                                  ref int input, [NoAlias] in CategoryRoutingRow routingRow, int nodeID)
        {
#if USE_OPTIMIZATIONS
            for (int n = startSearchRowIndex; n < outputLength; n++)
            {
                //Debug.Assert(nodeIndex == outputStack[n].nodeIndex);
                
                // We don't want to add identical rows, so if we find one, return it's input index
                if (outputStack[n].routingRow.Equals(routingRow))
                    return (int)outputStack[n].Input + 1; 
            }
#endif
            outputStack[outputLength] = new CategoryStackNode
            {
                Input       = (CategoryGroupIndex)input,
                routingRow  = routingRow,
                NodeIDValue      = nodeID
            };
            outputLength++;
            input++;
            // NOTE: we return the input row index + 1 so 0 (uninitialized value) is invalid
            return input;
        }

        // Remap indices to new destinations, used when destination rows have been merged
#if USE_OPTIMIZATIONS
        static void RemapIndices([NoAlias] NativeArray<CategoryStackNode> stack, [NoAlias] NativeArray<int> remap, int start, int last)
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
#endif

#if SHOW_DEBUG_MESSAGES
        static void Dump(int processedNodeID, NativeArray<CategoryStackNode> stack, int stackLength, int depth = 0)
        {
            var space = new String(' ', depth*4);
            if (stackLength == 0)
            {
                Debug.Log($"{space}processedNode: {processedNodeID} stack.Count == 0");
                return;
            }
            var stringBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < stackLength; i++)
            {
                if (i == 0 || stack[i - 1].nodeIndex != stack[i].nodeIndex)
                    stringBuilder.AppendLine($"  --- nodeIndex: {stack[i].nodeIndex}");
                stringBuilder.AppendLine($"\t{i,-3} -\t[{(int)stack[i].input,-3}]: {stack[i].routingRow.ToString(false)}");
            }
            Debug.LogWarning($"{space}processedNode: {processedNodeID}\n{stringBuilder.ToString()}");
        }
#endif
    }
}
