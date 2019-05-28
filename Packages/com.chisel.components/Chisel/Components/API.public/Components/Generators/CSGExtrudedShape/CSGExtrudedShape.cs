using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using UnitySceneExtensions;
using System.Linq;

namespace Chisel.Components
{
    // TODO: change name
    [ExecuteInEditMode]
    public sealed class CSGExtrudedShape : CSGGeneratorComponent
    {
        public override string NodeTypeName { get { return "Extruded Shape"; } }

        public CSGExtrudedShape() : base() { }
        
        [SerializeField] Path					path			= null;
        [SerializeField] Curve2D				shape			= null;
        [SerializeField] public int             curveSegments   = 8;
        [SerializeField] ChiselBrushMaterial[]	brushMaterials;
        [SerializeField] SurfaceDescription[]	surfaceDescriptions;
        
        public static readonly Curve2D DefaultShape = new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        protected override void OnValidateInternal()
        {
            base.OnValidateInternal();
        }

        protected override void OnResetInternal()
        {
            path				= new Path(Path.Default);
            shape				= new Curve2D(DefaultShape);
            curveSegments		= 8;
            brushMaterials		= null;
            surfaceDescriptions = null;
            base.OnResetInternal();
        }


        public Path Path
        {
            get { return path; }
            set
            {
                if (value == path)
                    return;
                
                path = value;

                OnValidateInternal();
            }
        }
        
        public Curve2D Shape
        {
            get { return shape; }
            set
            {
                if (value == shape)
                    return;
                
                shape = value;

                OnValidateInternal();
            }
        }
        
        protected override void UpdateGeneratorInternal()
        {
            if (brushMaterials == null ||
                brushMaterials.Length != 3)
            {
                var defaultRenderMaterial	= CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial	= CSGMaterialManager.DefaultPhysicsMaterial;
                brushMaterials = new ChiselBrushMaterial[3];
                for (int i = 0; i < 3; i++) // Note: sides share same material
                    brushMaterials[i] = ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial);
            }

            if (Shape == null)
                Shape = new Curve2D(DefaultShape);

            int sides = Shape.controlPoints.Length;
            if (surfaceDescriptions == null ||
                surfaceDescriptions.Length != 2 + sides)
            {
                var surfaceFlags	= CSGDefaults.SurfaceFlags;
                surfaceDescriptions = new SurfaceDescription[2 + sides];
                for (int i = 0; i < 2 + sides; i++) 
                {
                    surfaceDescriptions[i] = new SurfaceDescription { surfaceFlags = surfaceFlags, UV0 = UVMatrix.centered };
                }
            }

            BrushMeshAssetFactory.GenerateExtrudedShape(brushMeshAsset, shape, path, curveSegments, brushMaterials, ref surfaceDescriptions);
        }
    }
}
