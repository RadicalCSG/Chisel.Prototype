using System;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
    public static class GridRenderer
    {
        const int kPlaneSize = 100;
        private static Mesh gridMesh;		
        internal static Mesh GridMesh
        {
            get
            {
                if (!gridMesh)
                {
                    var vertices = new Vector3[kPlaneSize * kPlaneSize];
                    var indices  = new int[(kPlaneSize -1) * (kPlaneSize-1)*6];
                    var vertex	 = new Vector3();
                    for (int y = 0, n = 0; y < kPlaneSize; y++)
                    {
                        vertex.y = (2.0f * (y / (float)(kPlaneSize - 1))) - 1.0f;
                        for (int x = 0; x < kPlaneSize; x++, n++)
                        {
                            vertex.x = (2.0f * (x / (float)(kPlaneSize - 1))) - 1.0f;
                            vertices[n] = vertex;
                        }
                    }

                    for (int y = 0, n = 0; y < kPlaneSize - 1; y++)
                    {
                        var y0 = y;
                        var y1 = y + 1;
                        for (int x = 0; x < kPlaneSize - 1; x++, n += 6)
                        {
                            var x0 = x;
                            var x1 = x + 1;

                            var n00 = (y0 * kPlaneSize) + x0; var n10 = (y0 * kPlaneSize) + x1;
                            var n01 = (y1 * kPlaneSize) + x0; var n11 = (y1 * kPlaneSize) + x1;

                            indices[n + 0] = n00;
                            indices[n + 1] = n10;
                            indices[n + 2] = n01;

                            indices[n + 3] = n10;
                            indices[n + 4] = n01;
                            indices[n + 5] = n11;
                        }
                    }

                    gridMesh = new Mesh()
                    {
                        name = "Plane",
                        vertices  = vertices,
                        triangles = indices,
                        hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset
                    };
                    gridMesh.bounds = new Bounds(Vector3.zero, new Vector3(float.MaxValue, 0.1f, float.MaxValue));
                }
                return gridMesh;
            }
        }

        private static MaterialPropertyBlock properties = null;
        
        private static Material gridMaterial;
        internal static Material GridMaterial
        {
            get
            {
                if (!gridMaterial)
                {
                    gridMaterial = SceneHandleMaterialManager.GenerateDebugMaterial(SceneHandleMaterialManager.ShaderNameHandlesRoot + "Grid");
                    gridMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    gridMaterial.SetInt("_ZWrite", 0);   
                    gridMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual); 
                }
                return gridMaterial;
            }
        }
        
        internal static float	prevOrthoInterpolation	= 0;
        internal static float	prevSceneViewSize		= 0;
        internal static Vector3 prevGridSpacing			= Vector3.zero;
        internal static Color	prevCenterColor;
        internal static Color	prevGridColor;
        internal static Color	centerColor;
        internal static Color	gridColor;
        internal static int		counter = 0;

        public static float Opacity { get; set; } = 1.0f;


        public static void Render(this Grid grid, SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (!sceneView)
                return;

            var renderMode = sceneView.cameraMode.drawMode;
            if (renderMode != DrawCameraMode.Textured &&
                renderMode != DrawCameraMode.TexturedWire &&
                renderMode != DrawCameraMode.Normal)
                return;
             
            var camera		= sceneView.camera;
            if (!camera)
                return;
            
            var gridMaterial	= GridMaterial;
            var gridMesh		= GridMesh;
            var gridSpacing		= grid.Spacing;
            var sceneViewSize	= sceneView.size;

            Vector3 swizzledGridSpacing;
            swizzledGridSpacing.x = gridSpacing.x;
            swizzledGridSpacing.y = gridSpacing.z;
            swizzledGridSpacing.z = gridSpacing.y;

            float orthoInterpolation; // hack to get SceneView.m_Ortho.faded
            {
                const float kOneOverSqrt2 = 0.707106781f;
                const float kMinOrtho = 0.2f;
                const float kMaxOrtho = 0.95f;
                orthoInterpolation = ((Mathf.Atan(Mathf.Tan(camera.fieldOfView / (2 * Mathf.Rad2Deg)) * Mathf.Sqrt(camera.aspect) / kOneOverSqrt2) / (0.5f * Mathf.Deg2Rad)) / 90.0f);
                orthoInterpolation = Mathf.Clamp01((orthoInterpolation - kMinOrtho) / (kMaxOrtho - kMinOrtho));
            }

            counter--;
            if (counter <= 0)
            {
                var opacity = Opacity;

                // this code is slow and creates garbage, but unity doesn't give us a nice efficient mechanism to get these standard colors
                centerColor = ColorUtility.GetPreferenceColor("Scene/Center Axis", new Color(.8f, .8f, .8f, .93f));
                centerColor.a = opacity * 0.5f;
                gridColor = ColorUtility.GetPreferenceColor("Scene/Grid", new Color(.5f, .5f, .5f, .4f));
                gridColor.a = opacity;
                counter = 10;
            }

            if (renderMode == DrawCameraMode.TexturedWire)
            {
                // if we don't use DrawMeshNow Unity will draw the wireframe of the grid :(

                gridMaterial.SetColor("_GridColor",			 gridColor);
                gridMaterial.SetColor("_CenterColor",		 centerColor);
                gridMaterial.SetFloat("_OrthoInterpolation", orthoInterpolation);
                gridMaterial.SetFloat("_ViewSize",			 sceneViewSize);
                gridMaterial.SetVector("_GridSpacing",		 swizzledGridSpacing);
                gridMaterial.SetPass(0);
                Graphics.DrawMeshNow(gridMesh, grid.GridToWorldSpace, 0);
            } else
            {
                // TODO: store this for each SceneView
                if (properties == null)
                    properties = new MaterialPropertyBlock();

                if (prevGridColor != gridColor)
                {
                    properties.SetColor("_GridColor", gridColor);
                    prevGridColor = gridColor;
                }

                if (prevCenterColor != centerColor)
                {
                    properties.SetColor("_CenterColor", centerColor);
                    prevCenterColor = centerColor;
                }

                if (prevOrthoInterpolation != orthoInterpolation)
                {
                    properties.SetFloat("_OrthoInterpolation", orthoInterpolation);
                    prevOrthoInterpolation = orthoInterpolation;
                }

                if (prevSceneViewSize != sceneViewSize)
                {
                    properties.SetFloat("_ViewSize", sceneViewSize);
                    prevSceneViewSize = sceneViewSize;
                }

                if (prevGridSpacing != gridSpacing)
                {
                    properties.SetVector("_GridSpacing", swizzledGridSpacing);
                    prevGridSpacing = gridSpacing;
                }

                Graphics.DrawMesh(gridMesh, grid.GridToWorldSpace, gridMaterial, 0, camera, 0, properties, false, false);
            } 
        }
    }
}
