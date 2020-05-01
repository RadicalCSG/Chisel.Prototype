using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Chisel.Editors
{
    public static class ChiselEditorResources
    {
        internal const string kLargeIconID      = "@2x";
        internal const string kIconPath         = "Icons/";
        internal const string kActiveIconID     = "_ON";
        internal const string kDarkIconID       = "d_";

        internal static string[] resourcePaths;

        static ChiselEditorResources()
        {
            editorPixelsPerPoint    = EditorGUIUtility.pixelsPerPoint;
            isProSkin               = EditorGUIUtility.isProSkin;

            resourcePaths = GetResourcePaths();
        }

        // Should be safe since when these parameters change, Unity will do a domain reload, 
        // which will call the constructor in which these are set.
        static float    editorPixelsPerPoint;
        static bool     isProSkin;

        public static float    ImageScale
        {
            get
            {
                if (editorPixelsPerPoint > 1.0f)
                    return 2.0f;
                return 1.0f;
            }
        }

        static Texture2D LoadImageFromResourcePaths(string name)
        {
            name += imageExtension;
            for (int i = 0; i < resourcePaths.Length; i++)
            {
                var path = resourcePaths[i] + name;
                if (!System.IO.File.Exists(path))
                    continue;
                var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (image)
                    return image;
            }
            return null;
        }

        static Texture2D LoadScaledTexture(string name)
        {
            Texture2D image = null;
            var imagePixelsPerPoint = 1.0f;
            if (editorPixelsPerPoint > 1.0f)
            {
                image = LoadImageFromResourcePaths(name + kLargeIconID);
                if (image != null)
                    imagePixelsPerPoint = 2.0f;
            }

            if (image == null)
                image = LoadImageFromResourcePaths(name);

            if (image == null)
                return null;

            if (!Mathf.Approximately(imagePixelsPerPoint, editorPixelsPerPoint) &&    // scaling are different
                !Mathf.Approximately(editorPixelsPerPoint % 1, 0))                    // screen scaling is non-integer
                image.filterMode = FilterMode.Bilinear;
            return image;
        }

        // TODO: add a AssetPostProcessor to detect if images changed/added/removed and remove those from the lookup
        static Dictionary<string, Texture2D>    imagesLookup                = new Dictionary<string, Texture2D>();
        static Dictionary<string, Texture2D[]>  iconImagesLookup            = new Dictionary<string, Texture2D[]>();
        static Dictionary<int, GUIContent[]>    iconContentLookup           = new Dictionary<int, GUIContent[]>();
        static Dictionary<int, GUIContent[]>    iconContentWithNameLookup   = new Dictionary<int, GUIContent[]>();

        public static Texture2D LoadImage(string name)
        {
            name = FixSlashes(name);
            Texture2D image = null;
            if (imagesLookup.TryGetValue(name, out image))
                return image;
            image = LoadScaledTexture(name);
            if (!image)
                return image;
            imagesLookup[name] = image;
            return image;
        }

        public static Texture2D LoadIconImage(string name, bool active)
        {
            Texture2D result = null;
            name = name.ToLowerInvariant().Replace(' ', '_');  
            if (isProSkin)
            {
                if (active        ) result = LoadImage($@"{kIconPath}{kDarkIconID}{name}{kActiveIconID}");
                if (result == null) result = LoadImage($@"{kIconPath}{kDarkIconID}{name}");
            }
            if (result == null)
            {
                if (active        ) result = LoadImage($@"{kIconPath}{name}{kActiveIconID}");
                if (result == null) result = LoadImage($@"{kIconPath}{name}");
            }
            return result;
        } 

        public static Texture2D[] LoadIconImages(string name)
        {
            Texture2D[] iconImages;
            if (iconImagesLookup.TryGetValue(name, out iconImages))
                return iconImages;

            iconImages = new[] { LoadIconImage(name, false), LoadIconImage(name, true ) };

            if (iconImages[0] == null || iconImages[1] == null)
                iconImages = null;

            iconImagesLookup[name] = iconImages;
            return iconImages;
        }

        public static GUIContent[] GetIconContent(string name, string tooltip = "")
        {
            GUIContent[] contents;
            var id = (name.GetHashCode() * 33) + tooltip.GetHashCode();
            if (iconContentLookup.TryGetValue(id, out contents))
                return contents;

            if (tooltip == null)
                tooltip = string.Empty;

            var images = LoadIconImages(name);
            if (images == null)
                contents = new GUIContent[] { new GUIContent(name, tooltip), new GUIContent(name, tooltip) };
            else
                contents = new GUIContent[] { new GUIContent(images[0], tooltip), new GUIContent(images[1], tooltip) };

            iconContentLookup[id] = contents;
            return contents;
        }

        public static GUIContent[] GetIconContentWithName(string name, string tooltip = "")
        {
            GUIContent[] contents;
            var id = (name.GetHashCode() * 33) + tooltip.GetHashCode();
            if (iconContentWithNameLookup.TryGetValue(id, out contents))
                return contents;

            if (tooltip == null)
                tooltip = string.Empty;

            var images = LoadIconImages(name);
            if (images == null)
                contents = new GUIContent[] { new GUIContent(name, tooltip), new GUIContent(name, tooltip) };
            else
                contents = new GUIContent[] { new GUIContent(name, images[0], tooltip), new GUIContent(name, images[1], tooltip) };

            iconContentWithNameLookup[id] = contents;
            return contents;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        public static void ClearCache() { imagesLookup.Clear(); iconImagesLookup.Clear(); iconContentLookup.Clear(); iconContentWithNameLookup.Clear(); }

        
        #region Editor Resource Paths
        const string editorResourcesPath    = @"Editor Resources";
        const string imageExtension         = @".png";

        static readonly string[] searchPaths = new string[]
        {
            @"Assets/" + editorResourcesPath,
            @"Packages/com.chisel.editor/" + editorResourcesPath,
            @"Packages/com.chisel.components/" + editorResourcesPath
        };

        static string[] GetResourcePaths()
        {
            var paths = new List<string>();
            var foundPaths = new HashSet<string>();

            for (int i = 0; i < searchPaths.Length; i++)
            {
                var packagePath = System.IO.Path.GetFullPath(searchPaths[i]);
                if (System.IO.Directory.Exists(packagePath))
                {
                    var localPath = ToLocalPath(packagePath);
                    if (foundPaths.Add(localPath)) paths.Add(localPath);
                }
            }
            return paths.ToArray();
        }

        static string FixSlashes(string path)
        {
            return path.Replace('\\', '/');
        }

        static string ToLocalPath(string path)
        {
            path = FixSlashes(path);
            var assetsPathIndex = path.IndexOf(@"/Assets/");
            if (assetsPathIndex != -1)
            {
                path = path.Substring(assetsPathIndex + 1);
            } else
            {
                var packagePathIndex = path.IndexOf(@"/Packages/");
                if (packagePathIndex != -1)
                    path = path.Substring(packagePathIndex + 1);
            }
            if (!path.EndsWith("/"))
                path = path + "/";
            return path;
        }
        #endregion
    }
}
