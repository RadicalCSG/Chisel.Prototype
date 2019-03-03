using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Assets
{
	// This used to be an actual asset, but this proved too finicky to deal with

	// TODO: rename/cleanup, create real Asset that contains this so we can drag & drop a complete "surface" onto a brush
	[Serializable, PreferBinarySerialization]
	public sealed class CSGSurfaceAsset : IDisposable //: ScriptableObject
	{
		public CSGSurfaceAsset() { CSGSurfaceAssetManager.Register(this); }
		public void Dispose() { CSGSurfaceAssetManager.Unregister(this); }
		~CSGSurfaceAsset() { Dispose(); }
//		internal void OnEnable()	{ CSGSurfaceAssetManager.Register(this); }
//		internal void OnDisable()	{ CSGSurfaceAssetManager.Unregister(this); }
//		internal void OnValidate()	{ CSGSurfaceAssetManager.NotifyContentsModified(this); }
		

		[SerializeField] private LayerUsageFlags  layerUsage = LayerUsageFlags.RenderReceiveCastShadows | LayerUsageFlags.Collidable;
		[SerializeField] private Material         renderMaterial;
		[SerializeField] private PhysicMaterial   physicsMaterial;

		public LayerUsageFlags	LayerUsage		{ get { return layerUsage;      } set { if (layerUsage      == value) return; var prevValue = layerUsage;      layerUsage      = value; CSGSurfaceAssetManager.OnLayerUsageFlagsChanged(this, prevValue, value); } }
		public Material			RenderMaterial	{ get { return renderMaterial;  } set { if (renderMaterial  == value) return; var prevValue = renderMaterial;  renderMaterial  = value; CSGSurfaceAssetManager.OnRenderMaterialChanged(this, prevValue, value); } }
		public PhysicMaterial	PhysicsMaterial { get { return physicsMaterial; } set { if (physicsMaterial == value) return; var prevValue = physicsMaterial; physicsMaterial = value; CSGSurfaceAssetManager.OnPhysicsMaterialChanged(this, prevValue, value); } }
		public int				RenderMaterialInstanceID	{ get { return renderMaterial  ? renderMaterial .GetInstanceID() : 0; } }
		public int				PhysicsMaterialInstanceID	{ get { return physicsMaterial ? physicsMaterial.GetInstanceID() : 0; } }


		public static CSGSurfaceAsset CreateInstance(CSGSurfaceAsset other)
		{
			var newSurface = new CSGSurfaceAsset();//ScriptableObject.CreateInstance<CSGSurfaceAsset>();
			//newSurface.name				= "Copy of " + other.name;
			newSurface.LayerUsage		= other.layerUsage;
			newSurface.RenderMaterial	= other.renderMaterial;
			newSurface.PhysicsMaterial	= other.physicsMaterial;
			return newSurface;
		}

		public static CSGSurfaceAsset CreateInstance(LayerUsageFlags layerUsage = LayerUsageFlags.None)
		{
			var newSurface = new CSGSurfaceAsset();//ScriptableObject.CreateInstance<CSGSurfaceAsset>();
			//newSurface.name				= "Surface";
			newSurface.LayerUsage		= layerUsage;
			return newSurface;
		}

		public static CSGSurfaceAsset CreateInstance(Material renderMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.RenderReceiveCastShadows)
		{
			var newSurface = new CSGSurfaceAsset();//ScriptableObject.CreateInstance<CSGSurfaceAsset>();
			//newSurface.name				= "Surface " + renderMaterial.name;
			newSurface.LayerUsage		= layerUsage;
			newSurface.RenderMaterial	= renderMaterial;
			return newSurface;
		}

		public static CSGSurfaceAsset CreateInstance(PhysicMaterial physicsMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.Collidable)
		{
			var newSurface = new CSGSurfaceAsset();//ScriptableObject.CreateInstance<CSGSurfaceAsset>();
			//newSurface.name				= "Surface";
			newSurface.LayerUsage		= layerUsage;
			newSurface.PhysicsMaterial	= physicsMaterial;
			return newSurface;
		}

		public static CSGSurfaceAsset CreateInstance(Material renderMaterial, PhysicMaterial physicsMaterial, LayerUsageFlags layerUsage = LayerUsageFlags.RenderReceiveCastShadows | LayerUsageFlags.Collidable)
		{
			var newSurface = new CSGSurfaceAsset();//ScriptableObject.CreateInstance<CSGSurfaceAsset>();
			//newSurface.name				= "Surface " + renderMaterial.name;
			newSurface.LayerUsage		= layerUsage;
			newSurface.RenderMaterial	= renderMaterial;
			newSurface.PhysicsMaterial	= physicsMaterial;
			return newSurface;
		} 
	}
}
