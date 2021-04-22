using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselLinearStairs : ChiselDefinedGeneratorComponent<ChiselLinearStairsDefinition>
    {
        public const string kNodeTypeName = ChiselLinearStairsDefinition.kNodeTypeName;
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public float StepHeight
        {
            get { return definition.stepHeight; }
            set { if (definition.stepHeight == value) return; definition.stepHeight = value; OnValidateState(); }
        }

        public float StepDepth
        {
            get { return definition.stepDepth; }
            set { if (definition.stepDepth == value) return; definition.stepDepth = value; OnValidateState(); }
        }

        public float TreadHeight
        {
            get { return definition.treadHeight; }
            set { if (definition.treadHeight == value) return; definition.treadHeight = value; OnValidateState(); }
        }

        public float NosingDepth
        {
            get { return definition.nosingDepth; }
            set { if (definition.nosingDepth == value) return; definition.nosingDepth = value; OnValidateState(); }
        }

        public float NosingWidth
        {
            get { return definition.nosingWidth; }
            set { if (definition.nosingWidth == value) return; definition.nosingWidth = value; OnValidateState(); }
        }

        public float RiserDepth
        {
            get { return definition.riserDepth; }
            set { if (definition.riserDepth == value) return; definition.riserDepth = value; OnValidateState(); }
        }

        public float SideDepth
        {
            get { return definition.sideDepth; }
            set { if (definition.sideDepth == value) return; definition.sideDepth = value; OnValidateState(); }
        }

        public float SideWidth
        {
            get { return definition.sideWidth; }
            set { if (definition.sideWidth == value) return; definition.sideWidth = value; OnValidateState(); }
        }
        
        public float SideHeight
        {
            get { return definition.sideHeight; }	
            set { if (definition.sideHeight == value) return; definition.sideHeight = value; OnValidateState(); }
        }

        public StairsRiserType RiserType
        {
            get { return definition.riserType; }
            set { if (definition.riserType == value) return; definition.riserType = value; OnValidateState(); }
        }

        public StairsSideType LeftSide
        {
            get { return definition.leftSide; }
            set { if (definition.leftSide == value) return; definition.leftSide = value; OnValidateState(); }
        }

        public StairsSideType RightSide
        {
            get { return definition.rightSide; }
            set { if (definition.rightSide == value) return; definition.rightSide = value; OnValidateState(); }
        }

        public float Width
        {
            get { return definition.width; }
            set { if (definition.width == value) return; definition.width = value; OnValidateState(); }
        }

        public float Height
        {
            get { return definition.height; }
            set { if (definition.height == value) return; definition.height = value; OnValidateState(); }
        }

        public float Depth
        {
            get { return definition.depth; }
            set { if (definition.depth == value) return; definition.depth = value; OnValidateState(); }
        }

        public float PlateauHeight
        {
            get { return definition.plateauHeight; }
            set { if (definition.plateauHeight == value) return; definition.plateauHeight = value; OnValidateState(); }
        }

        public Bounds Bounds
        {
            get { return definition.bounds; }
            set { if (definition.bounds == value) return; definition.bounds = value; OnValidateState(); }
        }
        #endregion
    }
}
