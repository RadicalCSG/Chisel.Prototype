using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using Chisel.Assets;

namespace Chisel.Components
{
	[ExecuteInEditMode]
	public sealed class CSGCapsule : CSGGeneratorComponent
	{
		public override string NodeTypeName { get { return "Capsule"; } }

		// TODO: make this private
		[SerializeField] public CSGCapsuleDefinition definition = new CSGCapsuleDefinition();
		

		public float Height
		{
			get { return definition.height; }
			set { if (definition.height == value) return; definition.height = value; OnValidateInternal(); }
		}
		public float TopHeight
		{
			get { return definition.topHeight; }
			set { if (definition.topHeight == value) return; definition.topHeight = value; OnValidateInternal(); }
		}
		public float BottomHeight
		{
			get { return definition.bottomHeight; }
			set { if (definition.bottomHeight == value) return; definition.bottomHeight = value; OnValidateInternal(); }
		}
		public float DiameterX
		{
			get { return definition.diameterX; }
			set { if (definition.diameterX == value) return; definition.diameterX = value; OnValidateInternal(); }
		}
		public float DiameterZ
		{
			get { return definition.diameterZ; }
			set { if (definition.diameterZ == value) return; definition.diameterZ = value; OnValidateInternal(); }
		}
		public int Sides
		{
			get { return definition.sides; }
			set { if (definition.sides == value) return; definition.sides = value; OnValidateInternal(); }
		}
		public int TopSegments
		{
			get { return definition.topSegments; }
			set { if (definition.topSegments == value) return; definition.topSegments = value; OnValidateInternal(); }
		}
		public int BottomSegments
		{
			get { return definition.bottomSegments; }
			set { if (definition.bottomSegments == value) return; definition.bottomSegments = value; OnValidateInternal(); }
		}
		public bool HaveRoundedTop
		{
			get { return definition.haveRoundedTop; }
		}
		public bool HaveRoundedBottom
		{
			get { return definition.haveRoundedBottom; }
		}

		protected override void OnValidateInternal() { definition.Validate(); base.OnValidateInternal(); }
		protected override void OnResetInternal()	 { definition.Reset(); base.OnResetInternal(); }

		protected override void UpdateGeneratorInternal()
		{
			BrushMeshAssetFactory.GenerateCapsuleAsset(brushMeshAsset, definition);
		}
	}
}
