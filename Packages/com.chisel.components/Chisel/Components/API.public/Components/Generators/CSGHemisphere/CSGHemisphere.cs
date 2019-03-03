using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;

namespace Chisel.Components
{
	[ExecuteInEditMode]
	public sealed class CSGHemisphere : CSGGeneratorComponent
	{
		public override string NodeTypeName { get { return "Hemisphere"; } }

		// TODO: make this private
		[SerializeField] public CSGHemisphereDefinition definition = new CSGHemisphereDefinition();

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
			BrushMeshAssetFactory.GenerateHemisphereAsset(brushMeshAsset, definition);
		}
	}
}
