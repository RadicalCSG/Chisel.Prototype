using System.Collections.Generic;
using System.Buffers;
using UnityEngine;
using UnityEditor;
using Chisel.Core;

namespace Chisel.Editors
{
    public static class ChiselEditorResources
    {
        internal const string kLargeIconID      = "@2x";
        internal const string kIconPath         = "Icons/";
        internal const string kActiveIconID     = " On";
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
        static float editorPixelsPerPoint;
        public static bool  isProSkin;

        public static float ImageScale
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

        static Texture2D LoadScaledTextureInternal(string name)
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

        public static Texture2D[] LoadIconImages(string name)
        {
            var nameID = HashLowerInvariant(name);
            if (!iconImagesLookup.TryGetValue(nameID, out var iconImages))
            {
                iconImages = LoadIconImagesInternal(name);
                iconImagesLookup[nameID] = iconImages;
            }
            return iconImages;
        }

        public static GUIContent[] GetIconContent(string name, string tooltip = null)
        {
            var nameID = HashLowerInvariant(name);
            var id = (nameID * 33) + (tooltip?.GetHashCode() ?? 0);
            if (!iconContentLookup.TryGetValue(id, out var contents))
            {
                contents = GetIconContentInternal(name, tooltip ?? string.Empty);
                iconContentLookup[id] = contents;
            }
            return contents;
        }

        public static GUIContent[] GetIconContentWithName(string name, string tooltip = null)
        {
            var nameID = HashLowerInvariant(name); 
            var id = (nameID * 33) + (tooltip?.GetHashCode() ?? 0);
            if (!iconContentWithNameLookup.TryGetValue(id, out var contents))
            {
                contents = GetIconContentWithNameInternal(name, tooltip ?? string.Empty);
                iconContentWithNameLookup[id] = contents;
            }
            return contents;
        }

        // TODO: add a AssetPostProcessor to detect if images changed/added/removed and remove those from the lookup
        static readonly Dictionary<string, Texture2D>    imagesLookup                = new Dictionary<string, Texture2D>();
        static readonly Dictionary<int, Texture2D[]>     iconImagesLookup            = new Dictionary<int, Texture2D[]>();
        static readonly Dictionary<int, GUIContent[]>    iconContentLookup           = new Dictionary<int, GUIContent[]>();
        static readonly Dictionary<int, GUIContent[]>    iconContentWithNameLookup   = new Dictionary<int, GUIContent[]>();

        static Texture2D LoadImageInternal(string name)
        {
            name = FixSlashes(name);
            if (imagesLookup.TryGetValue(name, out Texture2D image))
                return image;
            image = LoadScaledTextureInternal(name);
            if (!image)
                return image;
            imagesLookup[name] = image;
            return image;
        }

        static int HashLowerInvariant(string name)
        {
            var length = name.Length;
            if (length == 0)
                return 0;

            var nameBuffer = ArrayPool<char>.Shared.Rent(length);

            for (int i = 0; i < length; i++)
            {
                var lowerChar = char.ToLowerInvariant(name[i + 0]);
                nameBuffer[i] = lowerChar;
            }

            int result;
            unchecked { result = (int)MathExtensions.Hash(nameBuffer); }
            ArrayPool<char>.Shared.Return(nameBuffer);
            return result; 
        }


        static Texture2D LoadIconImageInternal(string name, bool active)
        {
            Texture2D result = null;
            var nameID = name.Replace(' ', '_').ToLowerInvariant();
            if (isProSkin)
            {
                if (active        ) result = LoadImageInternal($@"{kIconPath}{kDarkIconID}{nameID}{kActiveIconID}");
                if (result == null) result = LoadImageInternal($@"{kIconPath}{kDarkIconID}{nameID}");
            }
            if (result == null)
            {
                if (active        ) result = LoadImageInternal($@"{kIconPath}{nameID}{kActiveIconID}");
                if (result == null) result = LoadImageInternal($@"{kIconPath}{nameID}");
            }
            return result;
        } 

        static Texture2D[] LoadIconImagesInternal(string name)
        {
            var iconImages = new[] { LoadIconImageInternal(name, false), LoadIconImageInternal(name, true) };
            if (iconImages[0] == null || iconImages[1] == null)
                iconImages = null;
            return iconImages;
        }

        static GUIContent[] GetIconContentInternal(string name, string tooltip)
        {
            if (tooltip == null)
                tooltip = string.Empty;

            GUIContent[] contents;
            var images = LoadIconImagesInternal(name);
            if (images == null)
                contents = new GUIContent[] { new GUIContent(L10n.Tr(name), L10n.Tr(tooltip)), new GUIContent(L10n.Tr(name), L10n.Tr(tooltip)) };
            else
                contents = new GUIContent[] { new GUIContent(images[0], L10n.Tr(tooltip)), new GUIContent(images[1], L10n.Tr(tooltip)) };
            return contents;
        }


        static GUIContent[] GetIconContentWithNameInternal(string name, string tooltip = "")
        {
            if (tooltip == null)
                tooltip = string.Empty;

            GUIContent[] contents;
            var images = LoadIconImagesInternal(name);
            if (images == null)
                contents = new GUIContent[] { new GUIContent(name, tooltip), new GUIContent(name, tooltip) };
            else
                contents = new GUIContent[] { new GUIContent(name, images[0], tooltip), new GUIContent(name, images[1], tooltip) };
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
            @"Packages/com.chisel.unity.editor/" + editorResourcesPath,
			@"Packages/com.chisel.unity.components/" + editorResourcesPath
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
