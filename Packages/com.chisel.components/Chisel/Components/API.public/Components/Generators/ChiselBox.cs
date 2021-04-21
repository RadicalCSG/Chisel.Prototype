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
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public MinMaxAABB Bounds
        {
            get { return definition.bounds; }
            set { if (math.all(value.Min == definition.bounds.Min) && math.all(value.Max == definition.bounds.Max)) return; definition.bounds = value; OnValidateInternal(); }
        }

        public float3 Min
        {
            get { return definition.Min; }
            set { if (math.all(value == definition.Min)) return; definition.Min = value; OnValidateInternal(); }
        }

        public float3 Max
        {
            get { return definition.Max; }
            set { if (math.all(value == definition.Max)) return; definition.Max = value; OnValidateInternal(); }
        }

        public float3 Center
        {
            get { return definition.Center; }
            set { if (math.all(value == definition.Center)) return; definition.Center = value; OnValidateInternal(); }
        }

        public float3 Size
        {
            get { return definition.Size; }
            set { if (math.all(value == definition.Size)) return; definition.Size = value; OnValidateInternal(); }
        }
        #endregion

        #region HasValidState
        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            if (!base.HasValidState())
                return false;

            if (Size.x == 0 ||
                Size.y == 0 ||
                Size.z == 0)
                return false;

            return true;
        }
        #endregion
    }
}