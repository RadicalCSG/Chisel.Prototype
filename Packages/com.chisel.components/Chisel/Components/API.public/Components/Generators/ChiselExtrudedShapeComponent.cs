using UnityEngine;
using Chisel.Core;
using UnitySceneExtensions;

namespace Chisel.Components
{
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [ExecuteInEditMode, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselExtrudedShapeComponent : ChiselBranchGeneratorComponent<Core.ChiselExtrudedShape, ChiselExtrudedShapeDefinition>
    {
        public const string kNodeTypeName = Core.ChiselExtrudedShapeDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public ChiselPath Path
        {
            get { return definition.path; }
            set
            {
                if (value == definition.path)
                    return;

                definition.path = value;

                OnValidateState();
            }
        }
        
        public Curve2D Shape
        {
            get { return definition.shape; }
            set
            {
                if (value == definition.shape)
                    return;

                definition.shape = value;

                OnValidateState();
            }
        }
        #endregion
    }
}
