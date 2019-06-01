using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselSpiralStairs : ChiselDefinedGeneratorComponent<ChiselSpiralStairsDefinition>
    {
        public const string kNodeTypeName = "Spiral Stairs";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Vector3 Origin
        {
            get { return definition.origin; }
            set { if (definition.origin == value) return; definition.origin = value; OnValidateInternal(); }
        }
        
        public float StepHeight
        {
            get { return definition.stepHeight; }
            set { if (definition.stepHeight == value) return; definition.stepHeight = value; OnValidateInternal(); }
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
        
        public float TreadHeight
        {
            get { return definition.treadHeight; }
            set { if (definition.treadHeight == value) return; definition.treadHeight = value; OnValidateInternal(); }
        }

        public float StartAngle
        {
            get { return definition.startAngle; }
            set { if (definition.startAngle == value) return; definition.startAngle = value; OnValidateInternal(); }
        }

        public float Rotation
        {
            get { return definition.rotation; }
            set { if (definition.rotation == value) return; definition.rotation = value; OnValidateInternal(); }
        }

        public float OuterDiameter
        {
            get { return definition.outerDiameter; }
            set { if (value == definition.outerDiameter) return; definition.outerDiameter = value; OnValidateInternal(); }
        }

        public int OuterSegments
        {
            get { return definition.outerSegments; }
            set { if (value == definition.outerSegments) return; definition.outerSegments = value; OnValidateInternal(); }
        }

        public float InnerDiameter
        {
            get { return definition.innerDiameter; }
            set { if (value == definition.innerDiameter) return; definition.innerDiameter = value; OnValidateInternal(); }
        }

        public int InnerSegments
        {
            get { return definition.innerSegments; }
            set { if (value == definition.innerSegments) return; definition.innerSegments = value; OnValidateInternal(); }
        }

        public float Height
        {
            get { return definition.height; }
            set { if (definition.height == value) return; definition.height = value; OnValidateInternal(); }
        }

        public StairsRiserType RiserType
        {
            get { return definition.riserType; }
            set { if (value == definition.riserType) return; definition.riserType = value; OnValidateInternal(); }
        }

        public uint BottomSmoothingGroup
        {
            get { return definition.bottomSmoothingGroup; }
            set { if (value == definition.bottomSmoothingGroup) return; definition.bottomSmoothingGroup = value; OnValidateInternal(); }
        }

        public int StepCount
        {
            get { return definition.StepCount; }
        }
        #endregion
    }
}
