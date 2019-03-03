using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chisel.Components
{
    public static class CSGDefaults
    {
        public static SurfaceFlags SurfaceFlags = SurfaceFlags.None; // Default surface flags when creating brushes

        // TODO: figure out a better way to associate (hierarchy) icons with a component
#if UNITY_EDITOR
        private static string packageFullPath;
        public static string PackageFullPath
        {
            get
            {
                if (string.IsNullOrEmpty(packageFullPath))
                    packageFullPath = GetPackageFullPath();

                return packageFullPath;
            }
        }

        private static string GetPackageFullPath()
        {
            string packagePath = System.IO.Path.GetFullPath("Packages/com.chisel.editor");
            if (System.IO.Directory.Exists(packagePath))
                return packagePath;

            packagePath = System.IO.Path.GetFullPath("Assets/..");
            if (System.IO.Directory.Exists(packagePath))
            {
                if (System.IO.Directory.Exists(packagePath + "/Assets/Packages/com.chisel.editor/Editor Resources"))
                    return packagePath + "/Assets/Packages/com.chisel.editor";

                if (System.IO.Directory.Exists(packagePath + "/Assets/Chisel/Editor Resources"))
                    return packagePath + "/Assets/Chisel";

                var matchingPaths   = System.IO.Directory.GetDirectories(packagePath, "Chisel", System.IO.SearchOption.AllDirectories);
                var path            = ValidateLocation(matchingPaths, packagePath);
                if (path != null) return packagePath + path;
            }
            return null;
        }

        private static string ValidateLocation(string[] paths, string projectPath)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (System.IO.Directory.Exists(paths[i] + "/Editor Resources"))
                {
                    var folderPath = paths[i].Replace(projectPath, "");
                    folderPath = folderPath.TrimStart('\\', '/');
                    return folderPath;
                }
            }
            return null;
        }

        public readonly static string AdditiveIconName		    = "csg_addition";
        public readonly static string SubtractiveIconName		= "csg_subtraction";
        public readonly static string IntersectingIconName	    = "csg_intersection";
        
        public readonly static string BranchIconName			= "csg_intersection";
        public readonly static string ModelIconName			    = "csg_intersection";

        const string AdditiveIconTooltip        = "Additive CSG Operation";
        const string SubtractiveIconTooltip     = "Subtractive CSG Operation";
        const string IntersectingIconTooltip    = "Intersecting CSG Operation";
        const string BranchIconTooltip          = "Branch CSG";
        const string ModelIconTooltip           = "Model CSG";

        public class Style
        {
            static Style instance = new Style();
            public static Style Instance {
                get
                {
                    Initialize();
                    return instance;
                }
            }

            public static void Initialize()
            {
                if (instance != null)
                    return;
                instance = new Style();
            }

            public Style()
            {
                Update();
            }

            Texture2D[] additiveImages;
            Texture2D[] subtractiveImages;
            Texture2D[] intersectingImages;
            Texture2D[] branchImages;
            Texture2D[] modelImages;

            GUIContent[] additiveIcons;
            GUIContent[] subtractiveIcons;
            GUIContent[] intersectingIcons;
            GUIContent[] branchIcons;
            GUIContent[] modelIcons;

            public static Texture2D[] AdditiveImages        { get { if (Instance == null) instance = new Style(); return Instance.additiveImages; } }
            public static Texture2D[] SubtractiveImages     { get { if (Instance == null) instance = new Style(); return Instance.subtractiveImages; } }
            public static Texture2D[] IntersectingImages    { get { if (Instance == null) instance = new Style(); return Instance.intersectingImages; } }
            public static Texture2D[] BranchImages          { get { if (Instance == null) instance = new Style(); return Instance.branchImages; } }
            public static Texture2D[] ModelImages           { get { if (Instance == null) instance = new Style(); return Instance.modelImages; } }

            public static GUIContent[] AdditiveIcons        { get { if (Instance == null) instance = new Style(); return Instance.additiveIcons; } }
            public static GUIContent[] SubtractiveIcons     { get { if (Instance == null) instance = new Style(); return Instance.subtractiveIcons; } }
            public static GUIContent[] IntersectingIcons    { get { if (Instance == null) instance = new Style(); return Instance.intersectingIcons; } }
            public static GUIContent[] BranchIcons          { get { if (Instance == null) instance = new Style(); return Instance.branchIcons; } }
            public static GUIContent[] ModelIcons           { get { if (Instance == null) instance = new Style(); return Instance.modelIcons; } }

            
            public static Texture2D AdditiveImage           { get { var images = AdditiveImages; if (images == null) return null; return images[0]; } }
            public static Texture2D SubtractiveImage        { get { var images = SubtractiveImages; if (images == null) return null; return images[0]; } }
            public static Texture2D IntersectingImage       { get { var images = IntersectingImages; if (images == null) return null; return images[0]; } }
            public static Texture2D BranchImage             { get { var images = BranchImages; if (images == null) return null; return images[0]; } }
            public static Texture2D ModelImage              { get { var images = ModelImages; if (images == null) return null; return images[0]; } }

            public static GUIContent AdditiveIcon           { get { var icons = AdditiveIcons; if (icons == null) return null; return icons[0]; } }
            public static GUIContent SubtractiveIcon        { get { var icons = SubtractiveIcons; if (icons == null) return null; return icons[0]; } }
            public static GUIContent IntersectingIcon       { get { var icons = IntersectingIcons; if (icons == null) return null; return icons[0]; } }
            public static GUIContent BranchIcon             { get { var icons = BranchIcons; if (icons == null) return null; return icons[0]; } }
            public static GUIContent ModelIcon              { get { var icons = ModelIcons; if (icons == null) return null; return icons[0]; } }

            public void Update()
            {
                var basePath        = PackageFullPath + @"\Editor Resources\";
                var isProSkin       = EditorGUIUtility.isProSkin;
                var imageFormat     = isProSkin ? (basePath + @"\Icons\d_{0}.png"   ) : (basePath + @"\Icons\{0}.png"   );
                var imageOnFormat   = isProSkin ? (basePath + @"\Icons\d_{0} On.png") : (basePath + @"\Icons\{0} On.png");
                
                if (additiveImages      == null) additiveImages     = new Texture2D[2];
                if (subtractiveImages   == null) subtractiveImages  = new Texture2D[2];
                if (intersectingImages  == null) intersectingImages = new Texture2D[2];
                if (branchImages        == null) branchImages       = new Texture2D[2];
                if (modelImages         == null) modelImages        = new Texture2D[2];
                
                if (additiveIcons       == null) additiveIcons      = new[] { new GUIContent(AdditiveIconTooltip), new GUIContent(AdditiveIconTooltip) };
                if (subtractiveIcons    == null) subtractiveIcons   = new[] { new GUIContent(SubtractiveIconTooltip), new GUIContent(SubtractiveIconTooltip) };
                if (intersectingIcons   == null) intersectingIcons  = new[] { new GUIContent(IntersectingIconTooltip), new GUIContent(IntersectingIconTooltip) };
                if (branchIcons         == null) branchIcons        = new[] { new GUIContent(BranchIconTooltip), new GUIContent(BranchIconTooltip) };
                if (modelIcons          == null) modelIcons         = new[] { new GUIContent(ModelIconTooltip), new GUIContent(ModelIconTooltip) };

                // FIXME: this works for textmesh pro, but here the AssetDatabase can't seem to find any files?
                /*
                additiveImages[0] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageFormat, AdditiveIconName));
                additiveImages[1] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageOnFormat, AdditiveIconName));

                subtractiveImages[0] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageFormat, SubtractiveIconName));
                subtractiveImages[1] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageOnFormat, SubtractiveIconName));

                intersectingImages[0] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageFormat, IntersectingIconName));
                intersectingImages[1] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageOnFormat, IntersectingIconName));

                branchImages[0] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageFormat, BranchIconName));
                branchImages[1] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageOnFormat, BranchIconName));

                modelImages[0] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageFormat, ModelIconName));
                modelImages[1] = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(imageOnFormat, ModelIconName));
                */

                Texture2D image;
                GUIContent icon;
                Texture2D[] images;
                GUIContent[] icons;

                images = additiveImages;
                icons = additiveIcons;
                image = images[0]; icon = icons[0]; if (image && icon.image != image) icon.image = image;
                image = images[1]; icon = icons[1]; if (image && icon.image != image) icon.image = image;

                images = subtractiveImages;
                icons = subtractiveIcons;
                image = images[0]; icon = icons[0]; if (image && icon.image != image) icon.image = image;
                image = images[1]; icon = icons[1]; if (image && icon.image != image) icon.image = image;

                images = intersectingImages;
                icons = intersectingIcons;
                image = images[0]; icon = icons[0]; if (image && icon.image != image) icon.image = image;
                image = images[1]; icon = icons[1]; if (image && icon.image != image) icon.image = image;

                images = branchImages;
                icons = branchIcons;
                image = images[0]; icon = icons[0]; if (image && icon.image != image) icon.image = image;
                image = images[1]; icon = icons[1]; if (image && icon.image != image) icon.image = image;

                images = modelImages;
                icons = modelIcons;
                image = images[0]; icon = icons[0]; if (image && icon.image != image) icon.image = image;
                image = images[1]; icon = icons[1]; if (image && icon.image != image) icon.image = image;
            }
        }
#endif
    }
}
