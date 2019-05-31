using UnityEngine;
using Chisel.Core;
using UnitySceneExtensions;

namespace Chisel.Components
{
    // TODO: change name
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselExtrudedShape : ChiselDefinedGeneratorComponent<ChiselExtrudedShapeDefinition>
    {
        // This ensures names remain identical, or a compile error occurs.
        // TODO: replace this with a property drawer
        public const string kDefinition                     = nameof(ChiselExtrudedShape.definition);
        public const string kDefinitionShape                = kDefinition + "." + nameof(ChiselExtrudedShape.definition.shape);
        public const string kDefinitionShapeControlPoints   = kDefinitionShape + "." + nameof(ChiselExtrudedShape.definition.shape.controlPoints));
        public const string kDefinitionPath                 = kDefinition + "." + nameof(ChiselExtrudedShape.definition.path);
        public const string kDefinitionPathSegments         = kDefinitionPath + "." + nameof(ChiselExtrudedShape.definition.path.segments));

        public const string kNodeTypeName = "Extruded Shape";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public ChiselPath Path
        {
            get { return definition.path; }
            set
            {
                if (value == definition.path)
                    return;

                definition.path = value;

                OnValidateInternal();
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

                OnValidateInternal();
            }
        }
        #endregion
    }
}
