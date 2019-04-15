using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
    public sealed partial class SceneHandles
    {
        static float SizeSlider(Vector3 center, Vector3 direction, Vector3 forward, Vector3 up, Vector3 right, float radius, Axes axes = Axes.None)
        {
            Vector3 position = center + direction * radius;
            float size = UnityEditor.HandleUtility.GetHandleSize(position);
            bool temp = GUI.changed;
            GUI.changed = false;
            position = Slider2DHandle(position, Vector3.zero, forward, up, right, size * 0.05f, OutlinedDotHandleCap, axes);
            if (GUI.changed)
                radius = Vector3.Dot(position - center, direction);
            GUI.changed |= temp;
            return radius;
        }
        
        public static float RadiusHandle(Vector3 normal, Vector3 position, float radius, bool renderDisc = true)
        {
            return RadiusHandle(Quaternion.LookRotation(normal), position, radius, renderDisc);
        }

        public static float RadiusHandle(Quaternion rotation, Vector3 position, float radius, bool renderDisc = true)
        {
            var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
            var prevDisabled	= SceneHandles.disabled;
            var prevColor		= SceneHandles.color;

            var forward = rotation * Vector3.forward;
            var up		= rotation * Vector3.up;
            var right	= rotation * Vector3.right;

            // Radius handle in zenith
            bool temp = GUI.changed;


            // Radius handles at disc
            temp = GUI.changed;
            GUI.changed = false;
            
            var isDisabled =  isStatic || prevDisabled || Snapping.AxisLocking[1];
            SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

            radius = SizeSlider(position,     up, forward, up, right, radius);
            radius = SizeSlider(position,    -up, forward, up, right, radius);
            
            isDisabled =  isStatic || prevDisabled || Snapping.AxisLocking[0];
            SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

            radius = SizeSlider(position,  right, forward, up, right, radius);
            radius = SizeSlider(position, -right, forward, up, right, radius);

            if (GUI.changed)
                radius = Mathf.Max(0.0f, radius);
            GUI.changed |= temp;
            
            isDisabled =  isStatic || prevDisabled || (Snapping.AxisLocking[0] && Snapping.AxisLocking[1]);
            SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

            // Draw gizmo
            if (radius > 0 && renderDisc)
                SceneHandles.DrawWireDisc(position, forward, radius);

            
            SceneHandles.disabled = prevDisabled;
            SceneHandles.color = prevColor;
            return radius;
        }

        public static Vector3 RadiusHandle(Vector3 center, Vector3 up, Vector3 radius)
        {
            var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
            var prevDisabled	= SceneHandles.disabled;
            var prevColor		= SceneHandles.color;
            var prevChanged		= GUI.changed;


            var delta1 = radius - center;
            var delta2 = Quaternion.AngleAxis(90, up) * delta1;

            var position0 = center + delta1;
            var position1 = center - delta1;
            var position2 = center + delta2;
            var position3 = center - delta2;


            float size;
            Vector3 forward;
            Vector3 right;
            GeometryUtility.CalculateTangents(up, out right, out forward);
            

            

            
            var isDisabled =  isStatic || prevDisabled;
            SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);


            GUI.changed = false;
            size = UnityEditor.HandleUtility.GetHandleSize(position0);
            position0 = Slider2DHandle(position0, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
            if (GUI.changed) { radius = position0; prevChanged = true; }
            
            GUI.changed = false;
            size = UnityEditor.HandleUtility.GetHandleSize(position1);
            position1 = Slider2DHandle(position1, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
            if (GUI.changed) { radius = center - (position1 - center); prevChanged = true; }
            
            GUI.changed = false;
            size = UnityEditor.HandleUtility.GetHandleSize(position2);
            position2 = Slider2DHandle(position2, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
            if (GUI.changed) { radius = center + (Quaternion.AngleAxis(-90, up) * (position2 - center)); prevChanged = true; }
            
            GUI.changed = false;
            size = UnityEditor.HandleUtility.GetHandleSize(position3);
            position3 = Slider2DHandle(position3, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
            if (GUI.changed) { radius = center - (Quaternion.AngleAxis(-90, up) * (position3 - center)); prevChanged = true; }


            
            GUI.changed |= prevChanged;
            
            isDisabled =  isStatic || prevDisabled || (Snapping.AxisLocking[0] && Snapping.AxisLocking[1]);
            SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

            
            float radiusMagnitude = delta1.magnitude;
            if (radiusMagnitude > 0)
                SceneHandles.DrawWireDisc(center, up, radiusMagnitude);
            
            
            SceneHandles.disabled = prevDisabled;
            SceneHandles.color = prevColor;
            return radius;
        }

        public static void RadiusHandle(Vector3 center, Vector3 up, ref Vector3 radius1, ref Vector3 radius2)
        {
            var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
            var prevColor		= SceneHandles.color;
            var prevMatrix		= SceneHandles.matrix;
            var prevDisabled	= SceneHandles.disabled;
            var prevChanged		= GUI.changed;


            var delta1 = radius1 - center;
            var delta2 = radius2 - center;

            var position0 = center + delta1;
            var position1 = center - delta1;
            var position2 = center + delta2;
            var position3 = center - delta2;


            float size;
            Vector3 forward;
            Vector3 right;
            GeometryUtility.CalculateTangents(up, out right, out forward);
            

            

            
            var isDisabled =  isStatic || prevDisabled;
            SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);


            GUI.changed = false;
            size = UnityEditor.HandleUtility.GetHandleSize(position0);
            position0 = Slider2DHandle(position0, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
            if (GUI.changed)
            {
                radius1 = position0;
                radius2 = center + (Quaternion.AngleAxis(-90, up) * ((radius1 - center).normalized * (radius2 - center).magnitude));
                prevChanged = true;
            }

            GUI.changed = false;
            size = UnityEditor.HandleUtility.GetHandleSize(position1);
            position1 = Slider2DHandle(position1, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
            if (GUI.changed)
            {
                radius1 = center - (position1 - center);
                radius2 = center + (Quaternion.AngleAxis(-90, up) * ((radius1 - center).normalized * (radius2 - center).magnitude));
                prevChanged = true;
            }
            
            GUI.changed = false;
            size = UnityEditor.HandleUtility.GetHandleSize(position2);
            position2 = Slider2DHandle(position2, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
            if (GUI.changed)
            {
                radius2 = center + (position2 - center);
                radius1 = center + (Quaternion.AngleAxis(-90, up) * ((radius2 - center).normalized * (radius1 - center).magnitude));
                prevChanged = true;
            }
            
            GUI.changed = false;
            size = UnityEditor.HandleUtility.GetHandleSize(position3);
            position3 = Slider2DHandle(position3, Vector3.zero, up, forward, right, size * 0.05f, OutlinedDotHandleCap);
            if (GUI.changed)
            {
                radius2 = center - (position3 - center);
                radius1 = center + (Quaternion.AngleAxis(-90, up) * ((radius2 - center).normalized * (radius1 - center).magnitude));
                prevChanged = true;
            }


            
            GUI.changed |= prevChanged;
            
            isDisabled =  isStatic || prevDisabled || (Snapping.AxisLocking[0] && Snapping.AxisLocking[1]);
            SceneHandles.color = SceneHandles.StateColor(prevColor, isDisabled, false);

            
            float radiusMagnitude1 = delta1.magnitude;
            float radiusMagnitude2 = delta2.magnitude;
            if (radiusMagnitude1 > 0 && radiusMagnitude2 > 0)
            {
                var ellipsis	= Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
                
                ellipsis.m00 = delta1.x;
                ellipsis.m10 = delta1.y;
                ellipsis.m20 = delta1.z;
                
                ellipsis.m01 = delta2.x;
                ellipsis.m11 = delta2.y;
                ellipsis.m21 = delta2.z;
                
                ellipsis.m02 = up.x;
                ellipsis.m12 = up.y;
                ellipsis.m22 = up.z;

                ellipsis *= Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);

                var newMatrix	= prevMatrix * ellipsis;

                SceneHandles.matrix = newMatrix;
                SceneHandles.DrawWireDisc(center, up, 1.0f);
            }
            
            
            SceneHandles.disabled = prevDisabled;
            SceneHandles.matrix = prevMatrix;
            SceneHandles.color = prevColor;
        }
    }
}
