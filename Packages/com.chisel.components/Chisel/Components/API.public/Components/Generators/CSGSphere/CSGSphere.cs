using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;

namespace Chisel.Components
{
	[ExecuteInEditMode]
	public sealed class CSGSphere : CSGGeneratorComponent
	{
		public override string NodeTypeName { get { return "Sphere"; } }

		// TODO: make this private
		[SerializeField] public CSGSphereDefinition definition = new CSGSphereDefinition();

		// TODO: implement properties
		public Vector3 DiameterXYZ
		{
			get { return definition.diameterXYZ; }
			set { if (definition.diameterXYZ == value) return; definition.diameterXYZ = value; OnValidateInternal(); }
		}

		protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
		protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

		protected override void UpdateGeneratorInternal()
		{
			BrushMeshAssetFactory.GenerateSphereAsset(brushMeshAsset, definition);
		}
	}
}
