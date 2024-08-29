using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public class RectUtility
    {
        public static Rect PointsToRect(Vector2 start, Vector2 end)
        {
            start.x = Mathf.Max(start.x, 0);
            start.y = Mathf.Max(start.y, 0);
            end.x = Mathf.Max(end.x, 0);
            end.y = Mathf.Max(end.y, 0);
            Rect r = new Rect(start.x, start.y, end.x - start.x, end.y - start.y);
            if (r.width < 0)
            {
                r.x += r.width;
                r.width = -r.width;
            }
            if (r.height < 0)
            {
                r.y += r.height;
                r.height = -r.height;
            }
            return r;
        }
    }
}
