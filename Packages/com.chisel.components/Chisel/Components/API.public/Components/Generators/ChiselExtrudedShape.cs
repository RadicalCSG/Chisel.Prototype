﻿using UnityEngine;
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
        public const string kNodeTypeName = ChiselExtrudedShapeDefinition.kNodeTypeName;
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
