using UnityEngine;
using Chisel.Core;
using Unity.Mathematics;

namespace Chisel.Components
{
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [ExecuteInEditMode, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselBoxComponent : ChiselBrushGeneratorComponent<ChiselBoxDefinition, Core.ChiselBox>
    {
        public const string kNodeTypeName = Core.ChiselBoxDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public MinMaxAABB Bounds
        {
            get { return definition.settings.bounds; }
            set { if (math.all(value.Min == definition.settings.bounds.Min) && math.all(value.Max == definition.settings.bounds.Max)) return; definition.settings.bounds = value; OnValidateState(); }
        }

        public float3 Min
        {
            get { return definition.settings.Min; }
            set { if (math.all(value == definition.settings.Min)) return; definition.settings.Min = value; OnValidateState(); }
        }

        public float3 Max
        {
            get { return definition.settings.Max; }
            set { if (math.all(value == definition.settings.Max)) return; definition.settings.Max = value; OnValidateState(); }
        }

        public float3 Center
        {
            get { return definition.settings.Center; }
            set { if (math.all(value == definition.settings.Center)) return; definition.settings.Center = value; OnValidateState(); }
        }

        public float3 Size
        {
            get { return definition.settings.Size; }
            set { if (math.all(value == definition.settings.Size)) return; definition.settings.Size = value; OnValidateState(); }
        }
        #endregion
    }
}