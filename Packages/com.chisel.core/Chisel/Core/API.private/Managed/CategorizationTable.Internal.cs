//#define USE_OPTIMIZATIONS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chisel.Core
{
#if USE_MANAGED_CSG_IMPLEMENTATION

	// TODO: Simplify


	enum CategoryIndex
	{
		Inside,
		Aligned,
		RevAligned,
		Outside,

		LastCategory = Outside
	};

	struct PolygonGroupIndex
	{
		public PolygonGroupIndex(PolygonGroupIndex other) { this.value = other.value; }

		public int value;

		public override bool Equals(object obj)
		{
			if (!(obj is PolygonGroupIndex))
				return false;

			var index = (PolygonGroupIndex)obj;
			return value == index.value;
		}

		public override int GetHashCode() { return value.GetHashCode(); }

		public static readonly PolygonGroupIndex First = new PolygonGroupIndex() { value = 0 };
		public static readonly PolygonGroupIndex Invalid = new PolygonGroupIndex() { value = -1 };

		public static bool operator ==(PolygonGroupIndex a, PolygonGroupIndex b) { return a.value == b.value; }
		public static bool operator !=(PolygonGroupIndex a, PolygonGroupIndex b) { return a.value != b.value; }
		public static bool operator <(PolygonGroupIndex a, PolygonGroupIndex b) { return a.value < b.value; }
		public static bool operator >(PolygonGroupIndex a, PolygonGroupIndex b) { return a.value > b.value; }
		public static bool operator <=(PolygonGroupIndex a, PolygonGroupIndex b) { return a.value <= b.value; }
		public static bool operator >=(PolygonGroupIndex a, PolygonGroupIndex b) { return a.value >= b.value; }


		public static explicit operator int(PolygonGroupIndex p) { return p.value; }
		public static explicit operator PolygonGroupIndex(int i) { return new PolygonGroupIndex() { value = i }; }
	}

	class PolygonRerouteTable
	{
		public bool AreAllTheSame() { for (var i = 1; i <= (int)CategoryIndex.LastCategory; i++) { if (destination[i - 1] != destination[i]) return false; } return true; }
		public bool Equals(PolygonRerouteTable other) { for (var i = 0; i <= (int)CategoryIndex.LastCategory; i++) { if (other.destination[i] != destination[i]) return false; } return true; }
	
		public PolygonGroupIndex		GetListForCategory(CategoryIndex category) { return destination[(int)(category)]; }

		public readonly PolygonGroupIndex[]	destination = new PolygonGroupIndex[(int)CategoryIndex.LastCategory + 1];

		public void RouteFrom(PolygonRerouteTable input, CategoryIndex inside, CategoryIndex aligned, CategoryIndex revAligned, CategoryIndex outside)
		{
			var insideDestination		= input.destination[(int)inside];
			var alignedDestination		= input.destination[(int)aligned];
			var revAlignedDestination	= input.destination[(int)revAligned];
			var outsideDestination		= input.destination[(int)outside];
			destination[(int)CategoryIndex.Inside]		= insideDestination;
			destination[(int)CategoryIndex.Aligned]		= alignedDestination;
			destination[(int)CategoryIndex.RevAligned]	= revAlignedDestination;
			destination[(int)CategoryIndex.Outside]		= outsideDestination;
		}

		public void RouteAllFrom(PolygonRerouteTable input, CategoryIndex all)
		{
			var allDestination = input.destination[(int)all];
			destination[(int)CategoryIndex.Inside]		= allDestination;
			destination[(int)CategoryIndex.Aligned]		= allDestination;
			destination[(int)CategoryIndex.RevAligned]	= allDestination;
			destination[(int)CategoryIndex.Outside]		= allDestination;
		}

		public void Reroute(CategoryIndex inside, CategoryIndex aligned, CategoryIndex revAligned, CategoryIndex outside)
		{
			var insideDestination		= destination[(int)inside];
			var alignedDestination		= destination[(int)aligned];
			var revAlignedDestination	= destination[(int)revAligned];
			var outsideDestination		= destination[(int)outside];
			destination[(int)CategoryIndex.Inside]		= insideDestination;
			destination[(int)CategoryIndex.Aligned]		= alignedDestination;
			destination[(int)CategoryIndex.RevAligned]	= revAlignedDestination;
			destination[(int)CategoryIndex.Outside]		= outsideDestination;
		}

		public void RerouteAll(CategoryIndex all)
		{
			var allDestination = destination[(int)all];
			destination[(int)CategoryIndex.Inside]		= allDestination;
			destination[(int)CategoryIndex.Aligned]		= allDestination;
			destination[(int)CategoryIndex.RevAligned]	= allDestination;
			destination[(int)CategoryIndex.Outside]		= allDestination;
		}
	}


	// TODO: store the rerouteTables per node, since inputs are in order & increase by 1, we can remove it and look them up directly
	struct CategoryStackNode
	{
		public CSGTreeNode			node;
		public PolygonGroupIndex	input;
		public PolygonRerouteTable	rerouteTable;

#region Lookup tables
		// Additive set operation on polygons: output = (left-node || right-node)
		// Defines final output from combination of categorization of left and right node
		static CategoryIndex[,] additiveOperation = new[,]
		{
			//right node                                                                                                 |
			//                          other                     other                                                  |
			//inside                    aligned                   reverse-aligned           outside                      |     left-node       
			//--------------------------------------------------------------------------------------------------------------------------------------
			{ CategoryIndex.Inside,     CategoryIndex.Inside,     CategoryIndex.Inside,     CategoryIndex.Inside         }, // inside
			{ CategoryIndex.Inside,     CategoryIndex.Aligned,    CategoryIndex.Inside,     CategoryIndex.Aligned        }, // other-aligned
			{ CategoryIndex.Inside,     CategoryIndex.Inside,     CategoryIndex.RevAligned, CategoryIndex.RevAligned     }, // other-reverse-aligned
			{ CategoryIndex.Inside,     CategoryIndex.Aligned,    CategoryIndex.RevAligned, CategoryIndex.Outside        }  // outside
		};

		// Subtractive set operation on polygons: output = !(!left-node || right-node)
		// Defines final output from combination of categorization of left and right node
		static CategoryIndex[,] subtractiveOperation = new[,]
		{
			//right node                                                                                                 |
			//                          other                     other                                                  |
			//inside                    aligned                   reverse-aligned           outside                      |     left-node       
			//--------------------------------------------------------------------------------------------------------------------------------------
			{ CategoryIndex.Outside,    CategoryIndex.RevAligned, CategoryIndex.Aligned,    CategoryIndex.Inside         }, // inside
			{ CategoryIndex.Outside,    CategoryIndex.Outside,    CategoryIndex.Aligned,    CategoryIndex.Aligned        }, // other-aligned
			{ CategoryIndex.Outside,    CategoryIndex.RevAligned, CategoryIndex.Outside,    CategoryIndex.RevAligned     }, // other-reverse-aligned
			{ CategoryIndex.Outside,    CategoryIndex.Outside,    CategoryIndex.Outside,    CategoryIndex.Outside        }  // outside
		};

		// Common set operation on polygons: output = !(!left-node || !right-node)
		// Defines final output from combination of categorization of left and right node
		static CategoryIndex[,] commonOperation = new[,]
		{
			//right node                                                                                                 |
			//                          other                     other                                                  |
			//inside                    aligned                   reverse-aligned           outside                      |     left-node       
			//--------------------------------------------------------------------------------------------------------------------------------------
			{ CategoryIndex.Inside,     CategoryIndex.Aligned,    CategoryIndex.RevAligned, CategoryIndex.Outside        }, // inside
			{ CategoryIndex.Aligned,    CategoryIndex.Aligned,    CategoryIndex.Outside,    CategoryIndex.Outside        }, // other-aligned
			{ CategoryIndex.RevAligned, CategoryIndex.Outside,    CategoryIndex.RevAligned, CategoryIndex.Outside        }, // other-reverse-aligned
			{ CategoryIndex.Outside,    CategoryIndex.Outside,    CategoryIndex.Outside,    CategoryIndex.Outside        }  // outside
		};

		static CategoryIndex[][,] csg_operations = new[]
		{
			CategoryStackNode.additiveOperation,	// CSGOperationType.Additive == 0
			CategoryStackNode.subtractiveOperation,	// CSGOperationType.Subtractive == 1
			CategoryStackNode.commonOperation,		// CSGOperationType.Common == 2

			CategoryStackNode.additiveOperation		// 3 (invalid value)
		};
#endregion


		static PolygonRerouteTable CategorizeNode(List<CategoryStackNode>	categoryNodes, 
												  IntersectionLookup 	intersectionTypeLookup,
												  PolygonRerouteTable		parentRerouteTable, 
												  CSGTreeNode				childNode,
												  CSGTreeNode				processedNode, 
												  CSGNodeType				childType,
												  CSGOperationType			childOperationType, 
												  ref int					polygonGroupCount,
												  bool						haveGonePastSelf)
		{
			if (parentRerouteTable.AreAllTheSame())
				return parentRerouteTable;

			var child_node_is_brush	= (childType & CSGNodeType.Brush) == CSGNodeType.Brush;
			var current_operation	= csg_operations[(int)childType];
			if (childNode == processedNode && child_node_is_brush)
			{
				// All categories lead to 'aligned' since this is the processed brush
				parentRerouteTable.RouteFrom(parentRerouteTable,
											   current_operation[(int)CategoryIndex.Inside		, (int)CategoryIndex.Aligned],
											   current_operation[(int)CategoryIndex.Aligned		, (int)CategoryIndex.Aligned],
											   current_operation[(int)CategoryIndex.RevAligned	, (int)CategoryIndex.Aligned],
											   current_operation[(int)CategoryIndex.Outside		, (int)CategoryIndex.Aligned]);
				return parentRerouteTable;
			}
			var intersectionType = intersectionTypeLookup.GetUnsafe(childNode.NodeID);
			if (intersectionType == IntersectionType.NoIntersection)
			{
				// All categories lead to 'outside' since this brush doesn't touch the processed brush
				parentRerouteTable.RouteFrom(parentRerouteTable,
											   current_operation[(int)CategoryIndex.Inside		, (int)CategoryIndex.Outside],
											   current_operation[(int)CategoryIndex.Aligned		, (int)CategoryIndex.Outside],
											   current_operation[(int)CategoryIndex.RevAligned	, (int)CategoryIndex.Outside],
											   current_operation[(int)CategoryIndex.Outside		, (int)CategoryIndex.Outside]);
				return parentRerouteTable;
			} else
			if (intersectionType == IntersectionType.AInsideB)
			{
				// All categories lead to 'outside' since this brush doesn't touch the processed brush
				parentRerouteTable.RouteFrom(parentRerouteTable,
											   current_operation[(int)CategoryIndex.Inside		, (int)CategoryIndex.Inside],
											   current_operation[(int)CategoryIndex.Aligned		, (int)CategoryIndex.Inside],
											   current_operation[(int)CategoryIndex.RevAligned	, (int)CategoryIndex.Inside],
											   current_operation[(int)CategoryIndex.Outside		, (int)CategoryIndex.Inside]);
				return parentRerouteTable;
			}

			var new_reroute_table		= new PolygonRerouteTable();
			var output_reroute_table	= new PolygonRerouteTable();// output polygon paths

			// determine the required outputs
			for (int rightCategoryIndex = 0; rightCategoryIndex <= (int)CategoryIndex.LastCategory; rightCategoryIndex++)
			{
				// determine the correct destination for all input polygons and add them to the stack for processing
				if (child_node_is_brush)
				{
					// Eat polygons that are aligned with another brush
					if (haveGonePastSelf)
					{
						new_reroute_table.RouteFrom(parentRerouteTable,
													current_operation[rightCategoryIndex, (int)CategoryIndex.Inside	   ],
													current_operation[rightCategoryIndex, (int)CategoryIndex.Outside   ],
													current_operation[rightCategoryIndex, (int)CategoryIndex.Inside    ],
													current_operation[rightCategoryIndex, (int)CategoryIndex.Outside   ]);
					} else
					{
						new_reroute_table.RouteFrom(parentRerouteTable,
													current_operation[rightCategoryIndex, (int)CategoryIndex.Inside     ],
													current_operation[rightCategoryIndex, (int)CategoryIndex.Inside     ],
													current_operation[rightCategoryIndex, (int)CategoryIndex.Inside     ],
													current_operation[rightCategoryIndex, (int)CategoryIndex.Outside    ]);
					}
				} else
				{
					new_reroute_table.RouteFrom(parentRerouteTable,
												current_operation[rightCategoryIndex, (int)CategoryIndex.Inside     ],
												current_operation[rightCategoryIndex, (int)CategoryIndex.Aligned    ],
												current_operation[rightCategoryIndex, (int)CategoryIndex.RevAligned ],
												current_operation[rightCategoryIndex, (int)CategoryIndex.Outside    ]);
				}

				//
				// All paths in route lead to same location, so don't bother storing it
				//
				if (new_reroute_table.AreAllTheSame())
				{
					output_reroute_table.destination[rightCategoryIndex] = new_reroute_table.GetListForCategory(CategoryIndex.Outside); // pick any one of the lists as destination
					continue;
				}

				//
				// See if we have a duplicate route
				//
				var prevCategory = (int)categoryNodes.Count - 1;
				while (prevCategory > 0 && categoryNodes[prevCategory].node == childNode)
				{ 
					if (categoryNodes[prevCategory].rerouteTable.Equals(new_reroute_table))
						// found a duplicate, use that one instead
					{
						output_reroute_table.destination[rightCategoryIndex] = categoryNodes[prevCategory].input;
						goto SkipRouteCreation;
					}
					prevCategory--; 
				}
				{
					//
					// Add a new route
					//
					var input_polygon_group_index = (PolygonGroupIndex)polygonGroupCount; polygonGroupCount++;
					{
						categoryNodes.Add(new CategoryStackNode()
						{
							input			= input_polygon_group_index,
							node			= childNode,
							rerouteTable	= new_reroute_table
						});
					}

					output_reroute_table.destination[rightCategoryIndex] = input_polygon_group_index;
				}
			SkipRouteCreation:
				;
			}

			// store the current output polygon lists, so we can use it as output for the previous node
			return output_reroute_table;
		}

		static void CategorizeFirstNode(List<CategoryStackNode>	categoryNodes, 
										IntersectionLookup 		intersectionTypeLookup,
										PolygonRerouteTable		parentRerouteTable, 
										PolygonGroupIndex 		inputPolygonGroupIndex, 
										CSGTreeNode				childNode, 
										CSGTreeNode				processedNode, 
										CSGNodeType				childNodeType,
										CSGOperationType		childOperationType,
										bool					haveGonePastSelf)
		{
			var child_node_is_brush = (childNodeType & CSGNodeType.Brush) == CSGNodeType.Brush;
			var intersectionType	= IntersectionType.InvalidValue;

			// All categories lead to 'aligned' since this is the processed brush
			if (childNode == processedNode && child_node_is_brush)
			{
				parentRerouteTable.RerouteAll(CategoryIndex.Aligned);
				goto found; 
			}
	
			// All categories lead to 'outside' since this brush doesn't touch the processed brush
			else
			{
				intersectionType = intersectionTypeLookup.GetUnsafe((int)(childNode.NodeID));
				if      (intersectionType == IntersectionType.NoIntersection) { parentRerouteTable.RerouteAll(CategoryIndex.Outside); goto found; }
				else if (intersectionType == IntersectionType.AInsideB      ) { parentRerouteTable.RerouteAll(CategoryIndex.Inside);  goto found; }
			}

			if (child_node_is_brush)
			{
				// Remove polygons that are aligned and removed by another brush
				if (haveGonePastSelf) 
				{ 
					parentRerouteTable.Reroute(CategoryIndex.Inside, CategoryIndex.Outside, CategoryIndex.Inside, CategoryIndex.Outside); 
					goto found; 
				} else
				{ 
					parentRerouteTable.Reroute(CategoryIndex.Inside, CategoryIndex.Inside, CategoryIndex.Inside, CategoryIndex.Outside); 
					goto found; 
				}
			}
			// else, no need to reroute, everything is already pointing to the correct destination

		found:
			categoryNodes.Add(new CategoryStackNode()
			{
				input		 = inputPolygonGroupIndex,
				node		 = childNode,
				rerouteTable = parentRerouteTable
			});
		}


		class CSGStackData
		{
			public CSGStackData(int first_sibling_index, int last_sibling_index, int stack_node_counter, int current_stack_node_index, CSGTreeNode categorization_node)
			{
				firstSiblingIndex		= first_sibling_index;
				lastSiblingIndex		= last_sibling_index;
				currentSiblingIndex		= last_sibling_index;
				stackNodeCount			= stack_node_counter;
				parentStackNodeIndex	= current_stack_node_index;
				parentNode				= categorization_node;
			}
			public int			firstSiblingIndex;
			public int			lastSiblingIndex;
			public int			currentSiblingIndex;
			public int			stackNodeCount;
			public int			parentStackNodeIndex;
			public CSGTreeNode	parentNode;
		};


		static void AddToCSGStack(List<CSGStackData> stackIterator, List<CategoryStackNode> categoryOperations, CSGTreeNode currentNode, int currentStackNodeIndex)
		{
			CSGTreeNode categorization_node = currentNode;
			Int32 		child_node_count	= (categorization_node.NodeID == CSGTreeNode.InvalidNodeID) ? 0 : categorization_node.Count;
			Int32		last_sibling_index	= child_node_count - 1;
			Int32		first_sibling_index = 0;

			//
			// Find first node that is actually additive (we can't subtract or find common area of ... nothing)
			//
			for (first_sibling_index = 0; first_sibling_index < child_node_count; first_sibling_index++)
			{
				var child_node				= categorization_node[first_sibling_index];
				var child_operation_type	= child_node.Operation;
				if (child_operation_type	== CSGOperationType.Additive)
					break;
			}

			if (first_sibling_index >= child_node_count)
				return;

			var stack_node_counter = 0;
			do
			{
				stack_node_counter++;
			} while (currentStackNodeIndex - stack_node_counter > 0 && categoryOperations[currentStackNodeIndex - stack_node_counter].node == currentNode);

			stackIterator.Add(new CSGStackData(first_sibling_index, last_sibling_index, stack_node_counter, currentStackNodeIndex, categorization_node));
		}


		public static void GenerateCategorizationTable(CSGTreeNode				rootNode,
													   CSGTreeBrush				processedNode,
													   IntersectionLookup		intersectionTypeLookup,
													   List<CategoryStackNode>	categoryOperations,
													   List<CategoryStackNode>	categoryBrushes,
													   ref int					polygonGroupCount)
		{
			var processed_node_index = processedNode.NodeID;
			if (!CSGManager.IsValidNodeID(processed_node_index))
				return;

			categoryOperations.Clear();
			categoryOperations.Add(new CategoryStackNode() { input = PolygonGroupIndex.First, node = rootNode });
			var rootStackNode = categoryOperations[0];
			for (int i = 0; i < (int)CategoryIndex.LastCategory; i++)
				categoryOperations[0].rerouteTable.destination[i] = (PolygonGroupIndex)(i + 1);
			categoryOperations[0].rerouteTable.destination[(int)CategoryIndex.LastCategory] = categoryOperations[0].rerouteTable.destination[0];
			categoryOperations[0] = rootStackNode;

			var stackIterator = new List<CSGStackData>();
			AddToCSGStack(stackIterator, categoryOperations, rootNode,
							0 //= root StackNodeIndex
							);

			if (stackIterator.Count == 0)
			{
				var parent_reroute_table = rootStackNode.rerouteTable;
				parent_reroute_table.RouteAllFrom(parent_reroute_table, CategoryIndex.Outside);
				return;
			}


			polygonGroupCount = (int)CategoryIndex.LastCategory + 1;
			bool haveGonePastSelf = false;
			while (stackIterator.Count > 0)
			{
				var currentStack				= stackIterator[stackIterator.Count - 1];
				var sibling_index				= currentStack.currentSiblingIndex;
				var first_sibling_index			= currentStack.firstSiblingIndex;
				var categorization_node			= currentStack.parentNode;
				var stack_node_counter			= currentStack.stackNodeCount;
				var current_stack_node_index	= currentStack.parentStackNodeIndex;

				if (sibling_index < first_sibling_index)
				{
					stackIterator.RemoveAt(stackIterator.Count - 1);
					continue;
				}

				if (!haveGonePastSelf && sibling_index == first_sibling_index && first_sibling_index > 0)
				{
					// Ensure we haven't passed ourselves in one of the brushes we skipped ..
					for (int i = 0; !haveGonePastSelf && i < first_sibling_index; i++)
						haveGonePastSelf = (categorization_node[i] == processedNode);
				}

				var child_node				= categorization_node[sibling_index];
				var child_operation_type	= child_node.Operation;
				var child_node_type			= child_node.Type;
				var child_node_is_brush		= (child_node_type & CSGNodeType.Brush) == CSGNodeType.Brush;

				var prev_stack_node_count	= categoryOperations.Count;
				var	categoryNodes			= child_node_is_brush ? categoryBrushes : categoryOperations;

				if (sibling_index != first_sibling_index)
				{
					for (int stack_node_iterator = stack_node_counter - 1; stack_node_iterator >= 0; stack_node_iterator--)
					{
						var	parent_stack_node_index = current_stack_node_index - stack_node_iterator;
						var category_stack_node		= categoryOperations[parent_stack_node_index];
						var parent_reroute_table	= category_stack_node.rerouteTable;
						category_stack_node.rerouteTable = CategorizeNode(categoryNodes, intersectionTypeLookup, parent_reroute_table, child_node, processedNode, child_node_type, child_operation_type, ref polygonGroupCount, haveGonePastSelf);
						categoryOperations[parent_stack_node_index] = category_stack_node;
					}
				} else
				{
					//var 	prev_brush_count = (int)categoryBrushes.Count;
					for (int stack_node_iterator = stack_node_counter - 1; stack_node_iterator >= 0; stack_node_iterator--)
					{
						var parentStackNodeIndex		= current_stack_node_index - stack_node_iterator;
						var parent_reroute_table		= categoryOperations[parentStackNodeIndex].rerouteTable;
						var input_polygon_group_index	= categoryOperations[parentStackNodeIndex].input;
						CategorizeFirstNode(categoryNodes, intersectionTypeLookup, parent_reroute_table, input_polygon_group_index, child_node, processedNode, child_node_type, child_operation_type, haveGonePastSelf);
					}
				}

				if (child_node == processedNode)
					haveGonePastSelf = true;

				currentStack.currentSiblingIndex--;

				if (child_node_is_brush)
					continue;

				var curr_stack_node_count = categoryOperations.Count;

				if (prev_stack_node_count == curr_stack_node_count)
					continue;

				var  intersectionType = intersectionTypeLookup.GetUnsafe((int)(categorization_node.NodeID));
				if (intersectionType == IntersectionType.NoIntersection)
					continue;

				AddToCSGStack(stackIterator, categoryOperations, child_node, (int)curr_stack_node_count - 1);
			}

			var categoryStackNodeCount = (int)categoryBrushes.Count;

#if USE_OPTIMIZATIONS
			//
			// Remove redundant nodes
			//


			//
			// Remove nodes that have a polygon destination that is unreachable
			//
			for (int i = categoryStackNodeCount - 1; i >= 0; i--)
			{
				var  input	= categoryBrushes[i].input;
				bool used	= (input == PolygonGroupIndex.First);
				for (int j = i + 1; !used && j < categoryStackNodeCount; j++)
				{
					var table = categoryBrushes[j].rerouteTable.destination;
					if (table[0] == PolygonGroupIndex.Invalid)
						continue;
					for (int c = 0; c <= (int)CategoryIndex.LastCategory; c++)
					{
						if (table[c] == input)
						{
							used = true;
							break;
						}
					}
				}

				if (used)
					continue;
		
				for (int n = 0; n <= (int)CategoryIndex.LastCategory; n++)
					categoryBrushes[i].rerouteTable.destination[n] = PolygonGroupIndex.Invalid;
			}

			//
			// Remove nodes where all its destinations go to the same location
			//
			int offset;
			offset = 0;
			for (int i = 0; i < categoryStackNodeCount; i++)
			{
				var  destination = categoryBrushes[i].rerouteTable.destination[0];
				if (destination == PolygonGroupIndex.Invalid ||
					!categoryBrushes[i].rerouteTable.AreAllTheSame())
					continue;

				var  input		= categoryBrushes[i].input;
				var  source		= input;
				if (input == PolygonGroupIndex.First)
				{
					if (destination <= (PolygonGroupIndex)(int)(CategoryIndex.LastCategory + 1))
						continue;
					for (int j = i - 1; j >= 0; j--)
					{
						if (categoryBrushes[j].input == PolygonGroupIndex.Invalid)
							continue;
						if (categoryBrushes[j].input == destination)
						{
							var categoryStackNode = categoryBrushes[j];
							categoryStackNode.input = source;
							categoryBrushes[j] = categoryStackNode;
						}

						var table = categoryBrushes[j].rerouteTable.destination;
						for (int c = 0; c <= (int)CategoryIndex.LastCategory; c++)
						{
							if (table[c] == destination) table[c] = source;
						}
					}
				} else
				{
					for (int j = offset; j < categoryStackNodeCount; j++)
					{
						if (categoryBrushes[j].input == source)
						{
							var categoryStackNode = categoryBrushes[j];
							categoryStackNode.input = destination;
							categoryBrushes[j] = categoryStackNode;
						}

						var table = categoryBrushes[j].rerouteTable.destination;
						for (int c = 0; c <= (int)CategoryIndex.LastCategory; c++)
						{
							if (table[c] == source) table[c] = destination;
						}
					}
				}

				for (int n = 0; n <= (int)CategoryIndex.LastCategory; n++)
					categoryBrushes[i].rerouteTable.destination[n] = PolygonGroupIndex.Invalid;
			}

			//
			// Same as above but in opposite direction to handle some edge cases
			//
			offset = 0;
			for (int i = categoryStackNodeCount - 1; i >= 0; i--)
			{
				var  destination = categoryBrushes[i].rerouteTable.destination[0];
				if (destination >= PolygonGroupIndex.First)
				{
					if (!categoryBrushes[i].rerouteTable.AreAllTheSame())
					{
						if (offset > 0)
						{
							categoryBrushes[i + offset] = categoryBrushes[i];
						}
						continue;
					}

					var  input		= categoryBrushes[i].input;
					var  source		= input;
					if (input == PolygonGroupIndex.First)
					{
						if (destination <= (PolygonGroupIndex)((int)CategoryIndex.LastCategory + 1))
							continue;
						for (int j = i - 1; j >= 0; j--)
						{
							if (categoryBrushes[j].input == PolygonGroupIndex.Invalid)
								continue;
							if (categoryBrushes[j].input == destination)
							{
								var categoryStackNode = categoryBrushes[j];
								categoryStackNode.input = source;
								categoryBrushes[j] = categoryStackNode;
							}

							var table = categoryBrushes[j].rerouteTable.destination;
							for (int c = 0; c <= (int)CategoryIndex.LastCategory; c++)
							{
								if (table[c] == destination) table[c] = source;
							}
						}
					} else
					{
						for (int j = offset; j < categoryStackNodeCount; j++)
						{
							if (categoryBrushes[j].input == source)
							{
								var categoryStackNode = categoryBrushes[j];
								categoryStackNode.input = destination;
								categoryBrushes[j] = categoryStackNode;
							}

							var table = categoryBrushes[j].rerouteTable.destination;
							for (int c = 0; c <= (int)CategoryIndex.LastCategory; c++)
							{
								if (table[c] == source) table[c] = destination;
							}
						}
					}
				}
				offset++;
			}//*
			if (offset > 0)
			{
				categoryStackNodeCount -= offset;
				if (categoryStackNodeCount > 0 && offset > 0)
				{
					for (int i = 0; i < categoryStackNodeCount; i++)
						categoryBrushes[i] = categoryBrushes[i + offset];
				}
				if (categoryStackNodeCount < categoryBrushes.Count)
					categoryBrushes.RemoveRange(categoryStackNodeCount, categoryBrushes.Count - categoryStackNodeCount);
			}
			//*/
#endif

			if (categoryStackNodeCount == 0)
			{
				polygonGroupCount = ((int)CategoryIndex.LastCategory + 1) + 1;
				return;
			}


			// TODO:	still possible to have first node be itself, all its destination are set to outside
			//			node could be removed


			categoryBrushes.Reverse();
	
			var kBaseIndex = (PolygonGroupIndex)(int)(CategoryIndex.LastCategory + 2); // all indices below this are used for final output / input

			//*

			//
			// For each brush, add filler nodes when a node is skipped for a specific destination
			// This allows us to just go through each polygon of each brush take its input and move it to its destination.
			//
			var activeInputs = new HashSet<PolygonGroupIndex>();
			for (int c = 0; c <= (int)CategoryIndex.LastCategory; c++)
			{
				var  destination = categoryBrushes[0].rerouteTable.destination[c];
				if (destination < kBaseIndex)
					continue;
				activeInputs.Add(destination);
			}

			for (int i = 1; i < categoryStackNodeCount; )
			{
				var  nodeIndex = categoryBrushes[i].node;
				activeInputs.Remove(categoryBrushes[i].input);
				var start_index = i;
				i++;
				while (i < categoryStackNodeCount)
				{
					if (nodeIndex != categoryBrushes[i].node)
						break;
					activeInputs.Remove(categoryBrushes[i].input);
					i++;
				}
				var end_index = i;

				if (activeInputs.Count > 0)
				{
					foreach(var destination in activeInputs)
					{
						var input	= destination;
						var node	= new CategoryStackNode() { input = input, node = nodeIndex };
						for (int t = 0; t <= (int)CategoryIndex.LastCategory; t++)
							node.rerouteTable.destination[t] = destination;
						categoryBrushes.Insert(i, node);
						i++;
					}
					categoryStackNodeCount = (int)categoryBrushes.Count;
				}

				for (int j = start_index; j < end_index;j++)
				{
					for (int c = 0; c <= (int)CategoryIndex.LastCategory; c++)
					{
						var  destination = categoryBrushes[j].rerouteTable.destination[c];
						if (destination < kBaseIndex)
							continue;
						activeInputs.Add(destination);
					}
				}
			}

			int maxCounter = ((int)CategoryIndex.LastCategory + 1);
			for (int i = 0; i < categoryStackNodeCount; i++)
				maxCounter = Math.Max(maxCounter, (int)categoryBrushes[i].input);
			polygonGroupCount = maxCounter + 1;
			//*/
		}
	}
#endif
}