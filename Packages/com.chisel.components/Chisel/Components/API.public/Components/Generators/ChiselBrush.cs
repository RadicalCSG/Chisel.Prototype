using UnityEngine;
using Chisel.Core;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselBrush : ChiselDefinedGeneratorComponent<ChiselBrushDefinition>
    {
        public const string kNodeTypeName = "Brush";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public BrushMesh BrushMesh
        {
            get { return definition.brushOutline; }
            set { if (value == definition.brushOutline) return; definition.brushOutline = value; OnValidateInternal(); }
        }
        public ChiselSurfaceDefinition Surfaces
        {
            get { return definition.surfaceDefinition; }
            set { if (value == definition.surfaceDefinition) return; definition.surfaceDefinition = value; OnValidateInternal(); }
        }
        #endregion

        #region HasValidState
        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {            
            return definition.ValidState;
        }
        #endregion
    }
}