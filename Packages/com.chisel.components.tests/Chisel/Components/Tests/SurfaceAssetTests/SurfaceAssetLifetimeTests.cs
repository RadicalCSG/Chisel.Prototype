using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using Chisel.Assets;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

namespace SurfaceAssetTests
{
	public sealed class SurfaceLifetimeTests
	{
		[SetUp] public void Setup() {  }


		[UnityTest]
		public IEnumerator CreateSurfaceAsset_SurfaceIsPartOfManager()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.True(CSGSurfaceAssetManager.IsRegistered(newSurface));
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAsset_Destroy_SurfaceIsNotPartOfManager()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				yield return null;
				CSGSurfaceAssetManager.Update();

				newSurface.Dispose();
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.False(CSGSurfaceAssetManager.IsRegistered(newSurface));
			}
		}

		[UnityTest]
		public IEnumerator CreateSurfaceAsset_UnregisterSurface_SurfaceIsNotPartOfManager()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				CSGSurfaceAssetManager.Unregister(newSurface);
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.False(CSGSurfaceAssetManager.IsRegistered(newSurface));
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAssetWithRenderMaterial_ManagerKnowsMaterial()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newRenderMaterial = new Material(Shader.Find("Specular"));

				newSurface.RenderMaterial = newRenderMaterial;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.True(CSGSurfaceAssetManager.IsRegistered(newSurface));
				Assert.AreEqual(newRenderMaterial, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newRenderMaterial);
			}
		}

		[UnityTest]
		public IEnumerator CreateSurfaceAssetWithPhysicMaterial_ManagerKnowsMaterial()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial = new PhysicMaterial();

				newSurface.PhysicsMaterial = newPhysicsMaterial;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.True(CSGSurfaceAssetManager.IsRegistered(newSurface));
				Assert.AreEqual(newPhysicsMaterial, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
			}
		}



		[UnityTest]
		public IEnumerator CreateSurfaceAssetWithRenderMaterial_ChangeRenderMaterial_ManagerOnlyKnowsNewMaterial()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newRenderMaterial1 = new Material(Shader.Find("Specular"));
				var newRenderMaterial2 = new Material(Shader.Find("Specular"));

				newSurface.RenderMaterial = newRenderMaterial1;
				yield return null;

				Assert.AreNotEqual(newRenderMaterial1, newRenderMaterial2);
				Assert.AreEqual(newRenderMaterial1, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial1.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial1.GetInstanceID()));
				LogAssert.Expect(LogType.Error, new Regex("Could not find"));
				Assert.IsNull(CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial2.GetInstanceID(), false));
				newSurface.RenderMaterial = newRenderMaterial2;
				yield return null;
				CSGSurfaceAssetManager.Update();

				LogAssert.Expect(LogType.Error, new Regex("Could not find"));
				Assert.IsNull(CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial1.GetInstanceID(), false));
				Assert.AreEqual(newRenderMaterial2, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial2.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial2.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newRenderMaterial1);
				UnityEngine.Object.DestroyImmediate(newRenderMaterial2);
			}
		}

		[UnityTest]
		public IEnumerator CreateSurfaceAssetWithPhysicMaterial_ChangePhysicMaterial_ManagerOnlyKnowsNewMaterial()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial1 = new PhysicMaterial();
				var newPhysicsMaterial2 = new PhysicMaterial();

				newSurface.PhysicsMaterial = newPhysicsMaterial1;
				yield return null;

				var foundPhysicsMaterial = CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial2.GetInstanceID(), false);

				Assert.AreNotEqual(newPhysicsMaterial1, newPhysicsMaterial2);
				Assert.AreEqual(newPhysicsMaterial1, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial1.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial1.GetInstanceID()));
				LogAssert.Expect(LogType.Error, new Regex("Could not find"));
				Assert.IsNull(foundPhysicsMaterial);
				newSurface.PhysicsMaterial = newPhysicsMaterial2;
				yield return null;
				CSGSurfaceAssetManager.Update();

				LogAssert.Expect(LogType.Error, new Regex("Could not find"));
				Assert.IsNull(CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial1.GetInstanceID(), false));
				Assert.AreEqual(newPhysicsMaterial2, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial2.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial2.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial1);
				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial2);
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAssetWithRenderMaterial_RetrievePhysicsMaterialWithRenderMaterialInstanceID_ReturnsNull()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newRenderMaterial = new Material(Shader.Find("Specular"));

				newSurface.RenderMaterial = newRenderMaterial;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.True(CSGSurfaceAssetManager.IsRegistered(newSurface));
				LogAssert.Expect(LogType.Error, new Regex("Trying to use Material with"));
				Assert.IsNull(CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));

				UnityEngine.Object.DestroyImmediate(newRenderMaterial);
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAssetWithPhysicMateriall_RetrieveRenderMaterialWithPhysicsMaterialInstanceID_ReturnsNull()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial = new PhysicMaterial();

				newSurface.PhysicsMaterial = newPhysicsMaterial;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.True(CSGSurfaceAssetManager.IsRegistered(newSurface));
				LogAssert.Expect(LogType.Error, new Regex("Trying to use PhysicMaterial with"));
				Assert.IsNull(CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));

				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAssetWithRenderMaterial_DestroySurface_ManagerDoesNotKnowMaterial()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newRenderMaterial = new Material(Shader.Find("Specular"));

				newSurface.RenderMaterial = newRenderMaterial;
				newSurface.Dispose();
				yield return null;
				CSGSurfaceAssetManager.Update();

				LogAssert.Expect(LogType.Error, new Regex("Could not find"));
				Assert.IsNull(CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));

				UnityEngine.Object.DestroyImmediate(newRenderMaterial);
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAssetWithPhysicMaterial_DestroySurface_ManagerDoesNotKnowMaterial()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial = new PhysicMaterial();

				newSurface.PhysicsMaterial = newPhysicsMaterial;
				newSurface.Dispose();
				yield return null;
				CSGSurfaceAssetManager.Update();

				LogAssert.Expect(LogType.Error, new Regex("Could not find"));
				Assert.IsNull(CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));

				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
			}
		}



		[UnityTest]
		public IEnumerator CreateSurfaceAsset_ChangeUsageFlag_SurfaceChangeEventIsCalled()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var hasBeenCalled = false;
				OnSurfaceAssetDelegate localDelegate = delegate (CSGSurfaceAsset surfaceAsset) { hasBeenCalled = true; };
				newSurface.LayerUsage = LayerUsageFlags.None;
				CSGSurfaceAssetManager.OnSurfaceAssetChanged -= localDelegate;
				CSGSurfaceAssetManager.OnSurfaceAssetChanged += localDelegate;
				yield return null;

				newSurface.LayerUsage = LayerUsageFlags.Collidable;
				Assert.IsTrue(hasBeenCalled);

				CSGSurfaceAssetManager.OnSurfaceAssetChanged -= localDelegate;
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAsset_ChangeRenderMaterial_SurfaceChangeEventIsCalled()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newRenderMaterial = new Material(Shader.Find("Specular"));

				var hasBeenCalled = false;
				OnSurfaceAssetDelegate localDelegate = delegate (CSGSurfaceAsset surfaceAsset)
				{ hasBeenCalled = true; };
				newSurface.RenderMaterial = null;
				CSGSurfaceAssetManager.OnSurfaceAssetChanged -= localDelegate;
				CSGSurfaceAssetManager.OnSurfaceAssetChanged += localDelegate;
				yield return null;

				newSurface.RenderMaterial = newRenderMaterial;
				Assert.IsTrue(hasBeenCalled);

				CSGSurfaceAssetManager.OnSurfaceAssetChanged -= localDelegate;
				UnityEngine.Object.DestroyImmediate(newRenderMaterial);
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAsset_ChangePhysicsMaterial_SurfaceChangeEventIsCalled()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial = new PhysicMaterial();

				var hasBeenCalled = false;
				OnSurfaceAssetDelegate localDelegate = delegate (CSGSurfaceAsset surfaceAsset)
				{ hasBeenCalled = true; };
				newSurface.PhysicsMaterial = null;
				CSGSurfaceAssetManager.OnSurfaceAssetChanged -= localDelegate;
				CSGSurfaceAssetManager.OnSurfaceAssetChanged += localDelegate;
				yield return null;

				newSurface.PhysicsMaterial = newPhysicsMaterial;
				Assert.IsTrue(hasBeenCalled);

				CSGSurfaceAssetManager.OnSurfaceAssetChanged -= localDelegate;
				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAsset_Destroy_SurfaceRemovedEventIsCalled()
		{
			using (var newSurface = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial = new PhysicMaterial();

				var hasBeenCalled = false;
				OnSurfaceAssetDelegate localDelegate = delegate (CSGSurfaceAsset surfaceAsset)
				{ hasBeenCalled = true; };
				newSurface.PhysicsMaterial = null;
				CSGSurfaceAssetManager.OnSurfaceAssetRemoved -= localDelegate;
				CSGSurfaceAssetManager.OnSurfaceAssetRemoved += localDelegate;
				yield return null;
				newSurface.Dispose();

				Assert.IsTrue(hasBeenCalled);

				CSGSurfaceAssetManager.OnSurfaceAssetRemoved -= localDelegate;
				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
			}
		}


		[UnityTest]
		public IEnumerator CreateSurfaceAsset_SurfaceAddedEventIsCalled()
		{
			var hasBeenCalled = false;
			OnSurfaceAssetDelegate localDelegate = delegate (CSGSurfaceAsset surfaceAsset)
			{ hasBeenCalled = true; };
			CSGSurfaceAssetManager.OnSurfaceAssetAdded -= localDelegate;
			CSGSurfaceAssetManager.OnSurfaceAssetAdded += localDelegate;
			yield return null;

			using (var newSurface = new CSGSurfaceAsset())
			{
				Assert.IsTrue(hasBeenCalled);

				CSGSurfaceAssetManager.OnSurfaceAssetAdded -= localDelegate;
			}
		}


		[UnityTest]
		public IEnumerator CreateTwoSurfaceAssetsWithSameRenderMaterial_RefCountIsTwo()
		{
			using (CSGSurfaceAsset newSurface1 = new CSGSurfaceAsset(), newSurface2 = new CSGSurfaceAsset())
			{
				var newRenderMaterial = new Material(Shader.Find("Specular"));

				newSurface1.RenderMaterial = newRenderMaterial;
				newSurface2.RenderMaterial = newRenderMaterial;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.AreEqual(newRenderMaterial, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));
				Assert.AreEqual(2, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newRenderMaterial);
			}
		}

		[UnityTest]
		public IEnumerator CreateTwoSurfaceAssetsWithSamePhysicsMaterial_RefCountIsTwo()
		{
			using (CSGSurfaceAsset newSurface1 = new CSGSurfaceAsset(), newSurface2 = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial = new PhysicMaterial();

				newSurface1.PhysicsMaterial = newPhysicsMaterial;
				newSurface2.PhysicsMaterial = newPhysicsMaterial;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.AreEqual(newPhysicsMaterial, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));
				Assert.AreEqual(2, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
			}
		}


		[UnityTest]
		public IEnumerator RemoveAndAddRenderMaterialToAnotherSurface_MaterialStillRegistered()
		{
			using (CSGSurfaceAsset newSurface1 = new CSGSurfaceAsset(), newSurface2 = new CSGSurfaceAsset())
			{
				var newRenderMaterial1 = new Material(Shader.Find("Specular"));
				var newRenderMaterial2 = new Material(Shader.Find("Specular"));

				newSurface1.RenderMaterial = newRenderMaterial1;
				newSurface2.RenderMaterial = newRenderMaterial2;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.AreEqual(newRenderMaterial1, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial1.GetInstanceID(), false));
				Assert.AreEqual(newRenderMaterial2, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial2.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial1.GetInstanceID()));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial2.GetInstanceID()));
				newSurface1.RenderMaterial = newRenderMaterial2;
				newSurface2.RenderMaterial = newRenderMaterial1;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.AreEqual(newRenderMaterial1, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial1.GetInstanceID(), false));
				Assert.AreEqual(newRenderMaterial2, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial2.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial1.GetInstanceID()));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial2.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newRenderMaterial1);
				UnityEngine.Object.DestroyImmediate(newRenderMaterial2);
			}
		}

		[UnityTest]
		public IEnumerator RemoveAndAddPhysicsMaterialToAnotherSurface_MaterialStillRegistered()
		{
			using (CSGSurfaceAsset newSurface1 = new CSGSurfaceAsset(), newSurface2 = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial1 = new PhysicMaterial();
				var newPhysicsMaterial2 = new PhysicMaterial();

				newSurface1.PhysicsMaterial = newPhysicsMaterial1;
				newSurface2.PhysicsMaterial = newPhysicsMaterial2;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.AreEqual(newPhysicsMaterial1, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial1.GetInstanceID(), false));
				Assert.AreEqual(newPhysicsMaterial2, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial2.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial1.GetInstanceID()));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial2.GetInstanceID()));
				newSurface1.PhysicsMaterial = newPhysicsMaterial2;
				newSurface2.PhysicsMaterial = newPhysicsMaterial1;
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.AreEqual(newPhysicsMaterial1, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial1.GetInstanceID(), false));
				Assert.AreEqual(newPhysicsMaterial2, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial2.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial1.GetInstanceID()));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial2.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial1);
				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial2);
			}
		}


		[UnityTest]
		public IEnumerator CreateTwoSurfaceAssetsWithSameRenderMaterial_DestroyOneSurface_ManagerKnowsMaterial()
		{
			using (CSGSurfaceAsset newSurface1 = new CSGSurfaceAsset(), newSurface2 = new CSGSurfaceAsset())
			{
				var newRenderMaterial = new Material(Shader.Find("Specular"));

				newSurface1.RenderMaterial = newRenderMaterial;
				newSurface2.RenderMaterial = newRenderMaterial;
				newSurface1.Dispose();
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.AreEqual(newRenderMaterial, CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newRenderMaterial);
			}
		}

		[UnityTest]
		public IEnumerator CreateTwoSurfaceAssetsWithSamePhysicsMaterial_DestroyOneSurface_ManagerKnowsMaterial()
		{
			using (CSGSurfaceAsset newSurface1 = new CSGSurfaceAsset(), newSurface2 = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial = new PhysicMaterial();

				newSurface1.PhysicsMaterial = newPhysicsMaterial;
				newSurface2.PhysicsMaterial = newPhysicsMaterial;
				newSurface1.Dispose();
				yield return null;
				CSGSurfaceAssetManager.Update();

				Assert.AreEqual(newPhysicsMaterial, CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));
				Assert.AreEqual(1, CSGSurfaceAssetManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial.GetInstanceID()));

				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
			}
		}



		[UnityTest]
		public IEnumerator CreateTwoSurfaceAssetsWithSameRenderMaterial_DestroyBothSurfaces_ManagerDoesNotKnowMaterial()
		{
			using (CSGSurfaceAsset newSurface1 = new CSGSurfaceAsset(), newSurface2 = new CSGSurfaceAsset())
			{
				var newRenderMaterial = new Material(Shader.Find("Specular"));

				newSurface1.RenderMaterial = newRenderMaterial;
				newSurface2.RenderMaterial = newRenderMaterial;
				newSurface1.Dispose();
				newSurface2.Dispose();
				yield return null;
				CSGSurfaceAssetManager.Update();

				LogAssert.Expect(LogType.Error, new Regex("Could not find"));
				Assert.IsNull(CSGSurfaceAssetManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));

				UnityEngine.Object.DestroyImmediate(newRenderMaterial);
			}
		}

		[UnityTest]
		public IEnumerator CreateTwoSurfaceAssetsWithSamePhysicsMaterial_DestroyBothSurfaces_ManagerDoesNotKnowMaterial()
		{
			using (CSGSurfaceAsset newSurface1 = new CSGSurfaceAsset(), newSurface2 = new CSGSurfaceAsset())
			{
				var newPhysicsMaterial = new PhysicMaterial();

				newSurface1.PhysicsMaterial = newPhysicsMaterial;
				newSurface2.PhysicsMaterial = newPhysicsMaterial;
				newSurface1.Dispose();
				newSurface2.Dispose();
				yield return null;
				CSGSurfaceAssetManager.Update();

				LogAssert.Expect(LogType.Error, new Regex("Could not find"));
				Assert.IsNull(CSGSurfaceAssetManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));

				UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
			}
		}
	}
}
