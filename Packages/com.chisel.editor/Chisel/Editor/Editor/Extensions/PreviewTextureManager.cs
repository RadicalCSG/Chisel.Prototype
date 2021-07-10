using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;

namespace Chisel.Editors
{
    public static class PreviewTextureManager
    {
        static PreviewRenderUtility previewUtility;

        public static void CleanUp()
        {
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
            Clear();
            s_Requests.Clear();
        }

        public static void Clear()
        {
            s_MaterialTextures.Clear();
        }

        private static void Init()
        {
            if (previewUtility == null)
                previewUtility = new PreviewRenderUtility();
        }

        class Styles { public readonly GUIStyle background = new GUIStyle("CurveEditorBackground"); };
        static Styles styles;

        class PreviewRequest
        {
            public Material material;
            public Vector3  direction;
            public Vector3  size;
        }

        // TODO: should lookup using PreviewRequest like structure so size could be taken into account
        static readonly Dictionary<Material, Texture2D>  s_MaterialTextures   = new Dictionary<Material, Texture2D>();
        static readonly List<PreviewRequest>             s_Requests           = new List<PreviewRequest>();


        static Mesh sphereMesh;
        internal static Mesh GetPreviewSphere()
        {
            if (!sphereMesh)
                sphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            return sphereMesh;
        }

        static Texture GetPreviewTexture(PreviewRenderUtility previewUtility, Vector2 previewDir, Rect region, Material material)
        {
            if (styles == null)
                styles = new Styles();

            var mesh        = GetPreviewSphere();
            var camera      = previewUtility.camera;

            var bounds      = mesh.bounds;
            var halfSize    = bounds.extents.magnitude;
            var distance    = 2.5f * halfSize;

            camera.transform.position   = -Vector3.forward * distance;
            camera.transform.rotation   = Quaternion.identity;
            camera.nearClipPlane        = distance - halfSize * 1.1f;
            camera.farClipPlane         = distance + halfSize * 1.1f;
            camera.fieldOfView          = 30.0f;
            camera.orthographicSize     = 1.0f;
            camera.clearFlags           = CameraClearFlags.Nothing;

            previewUtility.lights[0].intensity = 1.0f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
            previewUtility.lights[1].intensity = 1.0f;

            previewUtility.BeginStaticPreview(region);

            var fog = RenderSettings.fog;
            Unsupported.SetRenderSettingsUseFogNoDirty(false);
            camera.Render();
            Unsupported.SetRenderSettingsUseFogNoDirty(fog);

            var rot = Quaternion.Euler(previewDir.y, 0, 0) * Quaternion.Euler(0, previewDir.x, 0);
            var pos = rot * (-bounds.center);

            previewUtility.DrawMesh(mesh, pos, rot, material, 0);
            previewUtility.Render(false, false);
            return previewUtility.EndStaticPreview();
        }

        public static Texture2D GetPreviewTexture(Rect region, Vector3 direction, Material material)
        {
            if (!material)
                return null;

            Texture2D texture;
            if (s_MaterialTextures.TryGetValue(material, out texture))
                return texture;

            s_Requests.Add(new PreviewRequest() { direction = direction, size = region.size, material = material });
            return texture;
        }

        public static bool Update()
        {
            Init();

            bool needRepaint = s_Requests.Count > 0;
            if (Event.current.type == EventType.Repaint)
            { 
                for (int i = s_Requests.Count - 1; i >= 0; i--)
                {
                    var request  = s_Requests[i];
                    var material = request.material;
                    if (!s_MaterialTextures.ContainsKey(material))
                    { 
                        var region  = new Rect(Vector2.zero, request.size);
                        var texture = GetPreviewTexture(previewUtility, request.direction, region, material) as Texture2D;
                        if (!texture)
                            continue;

                        s_MaterialTextures[material] = texture; 
                    }
                    s_Requests.RemoveAt(i);
                }
            }
            return needRepaint;
        }
    }
}
