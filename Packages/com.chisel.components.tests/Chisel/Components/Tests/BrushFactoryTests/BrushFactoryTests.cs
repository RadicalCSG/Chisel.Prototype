using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BrushFactoryTests
{
	public sealed class BrushFactoryTests
	{
		[SetUp] public void Setup() { }

		[UnityTest]
		public IEnumerator CreateBrushMeshAsset_IsPartOfManager()
		{
			var layers = new SurfaceLayers
			{
				layerUsage = LayerUsageFlags.None
			};
			var box = BrushMeshFactory.CreateBox(Vector3.one, layers, SurfaceFlags.None);
			yield return null;

			var instance = BrushMeshInstance.Create(box);
			Assert.IsTrue(instance.Valid);
			instance.Destroy();
		}


	}
}
