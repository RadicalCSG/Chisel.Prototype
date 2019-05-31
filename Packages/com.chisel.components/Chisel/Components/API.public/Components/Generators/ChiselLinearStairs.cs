using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Chisel.Core;
using System;
using UnitySceneExtensions;
using System.Linq;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselLinearStairs : ChiselGeneratorComponent
    {
        public const string kNodeTypeName = "Linear Stairs";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        // TODO: make this private
        [SerializeField] public ChiselLinearStairsDefinition definition = new ChiselLinearStairsDefinition();

        #region Properties
        public float StepHeight
        {
            get { return definition.stepHeight; }
            set { if (definition.stepHeight == value) return; definition.stepHeight = value; OnValidateInternal(); }
        }

        public float StepDepth
        {
            get { return definition.stepDepth; }
            set { if (definition.stepDepth == value) return; definition.stepDepth = value; OnValidateInternal(); }
        }

        public float TreadHeight
        {
            get { return definition.treadHeight; }
            set { if (definition.treadHeight == value) return; definition.treadHeight = value; OnValidateInternal(); }
        }

        public float NosingDepth
        {
            get { return definition.nosingDepth; }
            set { if (definition.nosingDepth == value) return; definition.nosingDepth = value; OnValidateInternal(); }
        }

        public float NosingWidth
        {
            get { return definition.nosingWidth; }
            set { if (definition.nosingWidth == value) return; definition.nosingWidth = value; OnValidateInternal(); }
        }

        public float RiserDepth
        {
            get { return definition.riserDepth; }
            set { if (definition.riserDepth == value) return; definition.riserDepth = value; OnValidateInternal(); }
        }

        public float SideDepth
        {
            get { return definition.sideDepth; }
            set { if (definition.sideDepth == value) return; definition.sideDepth = value; OnValidateInternal(); }
        }

        public float SideWidth
        {
            get { return definition.sideWidth; }
            set { if (definition.sideWidth == value) return; definition.sideWidth = value; OnValidateInternal(); }
        }
        
        public float SideHeight
        {
            get { return definition.sideHeight; }	
            set { if (definition.sideHeight == value) return; definition.sideHeight = value; OnValidateInternal(); }
        }

        public StairsRiserType RiserType
        {
            get { return definition.riserType; }
            set { if (definition.riserType == value) return; definition.riserType = value; OnValidateInternal(); }
        }

        public StairsSideType LeftSide
        {
            get { return definition.leftSide; }
            set { if (definition.leftSide == value) return; definition.leftSide = value; OnValidateInternal(); }
        }

        public StairsSideType RightSide
        {
            get { return definition.rightSide; }
            set { if (definition.rightSide == value) return; definition.rightSide = value; OnValidateInternal(); }
        }

        public float Width
        {
            get { return definition.width; }
            set { if (definition.width == value) return; definition.width = value; OnValidateInternal(); }
        }

        public float Height
        {
            get { return definition.height; }
            set { if (definition.height == value) return; definition.height = value; OnValidateInternal(); }
        }

        public float Depth
        {
            get { return definition.depth; }
            set { if (definition.depth == value) return; definition.depth = value; OnValidateInternal(); }
        }

        public float PlateauHeight
        {
            get { return definition.plateauHeight; }
            set { if (definition.plateauHeight == value) return; definition.plateauHeight = value; OnValidateInternal(); }
        }

        public int StepCount
        {
            get { return definition.StepCount; }
        }

        public float StepDepthOffset
        {
            get { return definition.StepDepthOffset; }
        }

        public Bounds Bounds
        {
            get { return definition.bounds; }
            set { if (definition.bounds == value) return; definition.bounds = value; OnValidateInternal(); }
        }
        #endregion

        protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
        protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

        protected override void UpdateGeneratorInternal()
        {
            var brushMeshes = brushContainerAsset.BrushMeshes;
            if (!BrushMeshFactory.GenerateLinearStairs(ref brushMeshes, ref definition))
            {
                brushContainerAsset.Clear();
                return;
            }

            brushContainerAsset.SetSubMeshes(brushMeshes);
            brushContainerAsset.CalculatePlanes();
            brushContainerAsset.SetDirty();
        }
    }
}
