using UnityEngine;
using Chisel.Core;
using Unity.Mathematics;
using ChiselAABB = Chisel.Core.ChiselAABB;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselLinearStairsComponent : ChiselBranchGeneratorComponent<Core.ChiselLinearStairs, ChiselLinearStairsDefinition>
    {
        public const string kNodeTypeName = Core.ChiselLinearStairsDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public float StepHeight
        {
            get { return definition.settings.stepHeight; }
            set { if (definition.settings.stepHeight == value) return; definition.settings.stepHeight = value; OnValidateState(); }
        }

        public float StepDepth
        {
            get { return definition.settings.stepDepth; }
            set { if (definition.settings.stepDepth == value) return; definition.settings.stepDepth = value; OnValidateState(); }
        }

        public float TreadHeight
        {
            get { return definition.settings.treadHeight; }
            set { if (definition.settings.treadHeight == value) return; definition.settings.treadHeight = value; OnValidateState(); }
        }

        public float NosingDepth
        {
            get { return definition.settings.nosingDepth; }
            set { if (definition.settings.nosingDepth == value) return; definition.settings.nosingDepth = value; OnValidateState(); }
        }

        public float NosingWidth
        {
            get { return definition.settings.nosingWidth; }
            set { if (definition.settings.nosingWidth == value) return; definition.settings.nosingWidth = value; OnValidateState(); }
        }

        public float RiserDepth
        {
            get { return definition.settings.riserDepth; }
            set { if (definition.settings.riserDepth == value) return; definition.settings.riserDepth = value; OnValidateState(); }
        }

        public float SideDepth
        {
            get { return definition.settings.sideDepth; }
            set { if (definition.settings.sideDepth == value) return; definition.settings.sideDepth = value; OnValidateState(); }
        }

        public float SideWidth
        {
            get { return definition.settings.sideWidth; }
            set { if (definition.settings.sideWidth == value) return; definition.settings.sideWidth = value; OnValidateState(); }
        }
        
        public float SideHeight
        {
            get { return definition.settings.sideHeight; }	
            set { if (definition.settings.sideHeight == value) return; definition.settings.sideHeight = value; OnValidateState(); }
        }

        public StairsRiserType RiserType
        {
            get { return definition.settings.riserType; }
            set { if (definition.settings.riserType == value) return; definition.settings.riserType = value; OnValidateState(); }
        }

        public StairsSideType LeftSide
        {
            get { return definition.settings.leftSide; }
            set { if (definition.settings.leftSide == value) return; definition.settings.leftSide = value; OnValidateState(); }
        }

        public StairsSideType RightSide
        {
            get { return definition.settings.rightSide; }
            set { if (definition.settings.rightSide == value) return; definition.settings.rightSide = value; OnValidateState(); }
        }

        public float Width
        {
            get { return definition.settings.Width; }
            set { if (definition.settings.Width == value) return; definition.settings.Width = value; OnValidateState(); }
        }

        public float Height
        {
            get { return definition.settings.Height; }
            set { if (definition.settings.Height == value) return; definition.settings.Height = value; OnValidateState(); }
        }

        public float Depth
        {
            get { return definition.settings.Depth; }
            set { if (definition.settings.Depth == value) return; definition.settings.Depth = value; OnValidateState(); }
        }

        public float PlateauHeight
        {
            get { return definition.settings.plateauHeight; }
            set { if (definition.settings.plateauHeight == value) return; definition.settings.plateauHeight = value; OnValidateState(); }
        }

        public ChiselAABB Bounds
        {
            get { return definition.settings.bounds; }
            set
            {
                var bmin1 = math.min(value.Min, value.Max);
                var bmax1 = math.max(value.Min, value.Max);
                if (math.all(definition.settings.bounds.Min == bmin1) &&
                    math.all(definition.settings.bounds.Max == bmax1)) return;
                definition.settings.bounds = value;
                OnValidateState(); 
            }
        }
        #endregion
    }
}
