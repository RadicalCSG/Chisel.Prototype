using System;
using System.Linq;
using Chisel.Assets;
using Chisel.Core;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;

namespace Chisel.Components
{
	[Serializable]
	public enum CylinderShapeType
	{
		Cylinder,
		Cone,
		ConicalFrustum
	}
	
	[Serializable]
    public struct CSGCylinderDefinition
	{
		public CSGCylinderDefinition(float diameterX, float diameterZ, float height) { this.diameterX = diameterX; this.diameterZ = diameterZ; this.height = height; }
		public CSGCylinderDefinition(float diameter, float height) { this.diameterX = diameter; this.diameterZ = diameter; this.height = height; }
		[DistanceValue] public float diameterX;
		[DistanceValue] public float diameterZ;
		[DistanceValue] public float height;
		public void Reset()
		{
		}
		public void Validate()
		{
		}
	}
}