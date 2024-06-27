using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselSpiralStairsComponent : ChiselBranchGeneratorComponent<Core.ChiselSpiralStairs, ChiselSpiralStairsDefinition>
    {
        public const string kNodeTypeName = Core.ChiselSpiralStairsDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public Vector3 Origin
        {
            get { return definition.settings.origin; }
            set { if ((Vector3)definition.settings.origin == value) return; definition.settings.origin = value; OnValidateState(); }
        }
        
        public float StepHeight
        {
            get { return definition.settings.stepHeight; }
            set { if (definition.settings.stepHeight == value) return; definition.settings.stepHeight = value; OnValidateState(); }
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
        
        public float TreadHeight
        {
            get { return definition.settings.treadHeight; }
            set { if (definition.settings.treadHeight == value) return; definition.settings.treadHeight = value; OnValidateState(); }
        }

        public float StartAngle
        {
            get { return definition.settings.startAngle; }
            set { if (definition.settings.startAngle == value) return; definition.settings.startAngle = value; OnValidateState(); }
        }

        public float Rotation
        {
            get { return definition.settings.rotation; }
            set { if (definition.settings.rotation == value) return; definition.settings.rotation = value; OnValidateState(); }
        }

        public float OuterDiameter
        {
            get { return definition.settings.outerDiameter; }
            set { if (value == definition.settings.outerDiameter) return; definition.settings.outerDiameter = value; OnValidateState(); }
        }

        public int OuterSegments
        {
            get { return definition.settings.outerSegments; }
            set { if (value == definition.settings.outerSegments) return; definition.settings.outerSegments = value; OnValidateState(); }
        }

        public float InnerDiameter
        {
            get { return definition.settings.innerDiameter; }
            set { if (value == definition.settings.innerDiameter) return; definition.settings.innerDiameter = value; OnValidateState(); }
        }

        public int InnerSegments
        {
            get { return definition.settings.innerSegments; }
            set { if (value == definition.settings.innerSegments) return; definition.settings.innerSegments = value; OnValidateState(); }
        }

        public float Height
        {
            get { return definition.settings.height; }
            set { if (definition.settings.height == value) return; definition.settings.height = value; OnValidateState(); }
        }

        public StairsRiserType RiserType
        {
            get { return definition.settings.riserType; }
            set { if (value == definition.settings.riserType) return; definition.settings.riserType = value; OnValidateState(); }
        }

        public uint BottomSmoothingGroup
        {
            get { return definition.settings.bottomSmoothingGroup; }
            set { if (value == definition.settings.bottomSmoothingGroup) return; definition.settings.bottomSmoothingGroup = value; OnValidateState(); }
        }

        public int StepCount
        {
            get { return definition.settings.StepCount; }
        }
        #endregion
    }
}
