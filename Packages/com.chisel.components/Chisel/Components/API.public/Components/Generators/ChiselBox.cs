using UnityEngine;
using Chisel.Core;
using Unity.Mathematics;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselBox : ChiselDefinedGeneratorComponent<ChiselBoxDefinition>
    {
        public const string kNodeTypeName = ChiselBoxDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public MinMaxAABB Bounds
        {
            get { return definition.bounds; }
            set { if (math.all(value.Min == definition.bounds.Min) && math.all(value.Max == definition.bounds.Max)) return; definition.bounds = value; OnValidateState(); }
        }

        public float3 Min
        {
            get { return definition.Min; }
            set { if (math.all(value == definition.Min)) return; definition.Min = value; OnValidateState(); }
        }

        public float3 Max
        {
            get { return definition.Max; }
            set { if (math.all(value == definition.Max)) return; definition.Max = value; OnValidateState(); }
        }

        public float3 Center
        {
            get { return definition.Center; }
            set { if (math.all(value == definition.Center)) return; definition.Center = value; OnValidateState(); }
        }

        public float3 Size
        {
            get { return definition.Size; }
            set { if (math.all(value == definition.Size)) return; definition.Size = value; OnValidateState(); }
        }
        #endregion
    }
}