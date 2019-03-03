using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;

namespace Chisel.Components
{
	[ExecuteInEditMode]
	public sealed class CSGStadium : CSGGeneratorComponent
	{
		public override string NodeTypeName { get { return "Stadium"; } }

		// TODO: make this private
		[SerializeField] public CSGStadiumDefinition definition = new CSGStadiumDefinition();

		// TODO: implement properties

		protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
		protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

		protected override void UpdateGeneratorInternal()
		{
			BrushMeshAssetFactory.GenerateStadiumAsset(brushMeshAsset, definition);
		}
	}
}
