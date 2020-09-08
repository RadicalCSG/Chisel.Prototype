using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    public interface IGeneratorHandleRenderer
    {
        Matrix4x4 matrix { get; set; }
        void RenderBox(Bounds bounds);
        void RenderBoxMeasurements(Bounds bounds);
        void RenderCylinder(Bounds bounds, int segments);
        void RenderShape(Curve2D shape, float height);
    }
}
