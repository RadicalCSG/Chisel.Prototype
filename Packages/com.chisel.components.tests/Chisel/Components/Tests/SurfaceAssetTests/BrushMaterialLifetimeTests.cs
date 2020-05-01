using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

namespace BrushMaterialTests
{
    public sealed class BrushMaterialLifetimeTests
    {
        [SetUp] public void Setup() {  }


        [UnityTest]
        public IEnumerator CreateBrushMaterial_BrushMaterialIsPartOfManager()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.True(ChiselBrushMaterialManager.IsRegistered(newBrushMaterial));
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterial_Destroy_BrushMaterialIsNotPartOfManager()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                yield return null;
                ChiselBrushMaterialManager.Update();

                newBrushMaterial.Dispose();
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.False(ChiselBrushMaterialManager.IsRegistered(newBrushMaterial));
            }
        }

        [UnityTest]
        public IEnumerator CreateBrushMaterial_UnregisterBrushMaterial_BrushMaterialIsNotPartOfManager()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                ChiselBrushMaterialManager.Unregister(newBrushMaterial);
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.False(ChiselBrushMaterialManager.IsRegistered(newBrushMaterial));
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterialWithRenderMaterial_ManagerKnowsMaterial()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial = new Material(Shader.Find("Specular"));

                newBrushMaterial.RenderMaterial = newRenderMaterial;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.True(ChiselBrushMaterialManager.IsRegistered(newBrushMaterial));
                Assert.AreEqual(newRenderMaterial, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }

        [UnityTest]
        public IEnumerator CreateBrushMaterialWithPhysicMaterial_ManagerKnowsMaterial()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial = new PhysicMaterial();

                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.True(ChiselBrushMaterialManager.IsRegistered(newBrushMaterial));
                Assert.AreEqual(newPhysicsMaterial, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }



        [UnityTest]
        public IEnumerator CreateBrushMaterialWithRenderMaterial_ChangeRenderMaterial_ManagerOnlyKnowsNewMaterial()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial1 = new Material(Shader.Find("Specular"));
                var newRenderMaterial2 = new Material(Shader.Find("Specular"));

                newBrushMaterial.RenderMaterial = newRenderMaterial1;
                yield return null;

                Assert.AreNotEqual(newRenderMaterial1, newRenderMaterial2);
                Assert.AreEqual(newRenderMaterial1, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial1.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial1.GetInstanceID()));
                LogAssert.Expect(LogType.Error, new Regex("Could not find"));
                Assert.IsNull(ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial2.GetInstanceID(), false));
                newBrushMaterial.RenderMaterial = newRenderMaterial2;
                yield return null;
                ChiselBrushMaterialManager.Update();

                LogAssert.Expect(LogType.Error, new Regex("Could not find"));
                Assert.IsNull(ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial1.GetInstanceID(), false));
                Assert.AreEqual(newRenderMaterial2, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial2.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial2.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newRenderMaterial1);
                UnityEngine.Object.DestroyImmediate(newRenderMaterial2);
            }
        }

        [UnityTest]
        public IEnumerator CreateBrushMaterialWithPhysicMaterial_ChangePhysicMaterial_ManagerOnlyKnowsNewMaterial()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial1 = new PhysicMaterial();
                var newPhysicsMaterial2 = new PhysicMaterial();

                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial1;
                yield return null;

                var foundPhysicsMaterial = ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial2.GetInstanceID(), false);

                Assert.AreNotEqual(newPhysicsMaterial1, newPhysicsMaterial2);
                Assert.AreEqual(newPhysicsMaterial1, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial1.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial1.GetInstanceID()));
                LogAssert.Expect(LogType.Error, new Regex("Could not find"));
                Assert.IsNull(foundPhysicsMaterial);
                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial2;
                yield return null;
                ChiselBrushMaterialManager.Update();

                LogAssert.Expect(LogType.Error, new Regex("Could not find"));
                Assert.IsNull(ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial1.GetInstanceID(), false));
                Assert.AreEqual(newPhysicsMaterial2, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial2.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial2.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial1);
                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial2);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterialWithRenderMaterial_RetrievePhysicsMaterialWithRenderMaterialInstanceID_ReturnsNull()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial = new Material(Shader.Find("Specular"));

                newBrushMaterial.RenderMaterial = newRenderMaterial;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.True(ChiselBrushMaterialManager.IsRegistered(newBrushMaterial));
                LogAssert.Expect(LogType.Error, new Regex("Trying to use Material with"));
                Assert.IsNull(ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));

                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterialWithPhysicMateriall_RetrieveRenderMaterialWithPhysicsMaterialInstanceID_ReturnsNull()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial = new PhysicMaterial();

                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.True(ChiselBrushMaterialManager.IsRegistered(newBrushMaterial));
                LogAssert.Expect(LogType.Error, new Regex("Trying to use PhysicMaterial with"));
                Assert.IsNull(ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));

                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterialWithRenderMaterial_DestroyBrushMaterial_ManagerDoesNotKnowMaterial()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial = new Material(Shader.Find("Specular"));

                newBrushMaterial.RenderMaterial = newRenderMaterial;
                newBrushMaterial.Dispose();
                yield return null;
                ChiselBrushMaterialManager.Update();

                LogAssert.Expect(LogType.Error, new Regex("Could not find"));
                Assert.IsNull(ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));

                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterialWithPhysicMaterial_DestroyBrushMaterial_ManagerDoesNotKnowMaterial()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial = new PhysicMaterial();

                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial;
                newBrushMaterial.Dispose();
                yield return null;
                ChiselBrushMaterialManager.Update();

                LogAssert.Expect(LogType.Error, new Regex("Could not find"));
                Assert.IsNull(ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));

                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }



        [UnityTest]
        public IEnumerator CreateBrushMaterial_ChangeUsageFlag_BrushMaterialChangeEventIsCalled()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var hasBeenCalled = false;
                OnBrushMaterialDelegate localDelegate = delegate (ChiselBrushMaterial brushMaterial) { hasBeenCalled = true; };
                newBrushMaterial.LayerUsage = LayerUsageFlags.None;
                ChiselBrushMaterialManager.OnBrushMaterialChanged -= localDelegate;
                ChiselBrushMaterialManager.OnBrushMaterialChanged += localDelegate;
                yield return null;

                newBrushMaterial.LayerUsage = LayerUsageFlags.Collidable;
                Assert.IsTrue(hasBeenCalled);

                ChiselBrushMaterialManager.OnBrushMaterialChanged -= localDelegate;
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterial_ChangeRenderMaterial_BrushMaterialChangeEventIsCalled()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial = new Material(Shader.Find("Specular"));

                var hasBeenCalled = false;
                OnBrushMaterialDelegate localDelegate = delegate (ChiselBrushMaterial brushMaterial)
                { hasBeenCalled = true; };
                newBrushMaterial.RenderMaterial = null;
                ChiselBrushMaterialManager.OnBrushMaterialChanged -= localDelegate;
                ChiselBrushMaterialManager.OnBrushMaterialChanged += localDelegate;
                yield return null;

                newBrushMaterial.RenderMaterial = newRenderMaterial;
                Assert.IsTrue(hasBeenCalled);

                ChiselBrushMaterialManager.OnBrushMaterialChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterial_ChangePhysicsMaterial_BrushMaterialChangeEventIsCalled()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial = new PhysicMaterial();

                var hasBeenCalled = false;
                OnBrushMaterialDelegate localDelegate = delegate (ChiselBrushMaterial brushMaterial)
                { hasBeenCalled = true; };
                newBrushMaterial.PhysicsMaterial = null;
                ChiselBrushMaterialManager.OnBrushMaterialChanged -= localDelegate;
                ChiselBrushMaterialManager.OnBrushMaterialChanged += localDelegate;
                yield return null;

                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial;
                Assert.IsTrue(hasBeenCalled);

                ChiselBrushMaterialManager.OnBrushMaterialChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterial_Destroy_BrushMaterialRemovedEventIsCalled()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial = new PhysicMaterial();

                var hasBeenCalled = false;
                OnBrushMaterialDelegate localDelegate = delegate (ChiselBrushMaterial brushMaterial)
                { hasBeenCalled = true; };
                newBrushMaterial.PhysicsMaterial = null;
                ChiselBrushMaterialManager.OnBrushMaterialRemoved -= localDelegate;
                ChiselBrushMaterialManager.OnBrushMaterialRemoved += localDelegate;
                yield return null;
                newBrushMaterial.Dispose();

                Assert.IsTrue(hasBeenCalled);

                ChiselBrushMaterialManager.OnBrushMaterialRemoved -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMaterial_BrushMaterialAddedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushMaterialDelegate localDelegate = delegate (ChiselBrushMaterial brushMaterial)
            { hasBeenCalled = true; };
            ChiselBrushMaterialManager.OnBrushMaterialAdded -= localDelegate;
            ChiselBrushMaterialManager.OnBrushMaterialAdded += localDelegate;
            yield return null;

            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                Assert.IsTrue(hasBeenCalled);

                ChiselBrushMaterialManager.OnBrushMaterialAdded -= localDelegate;
            }
        }


        [UnityTest]
        public IEnumerator CreateTwoBrushMaterialsWithSameRenderMaterial_RefCountIsTwo()
        {
            using (ChiselBrushMaterial newBrushMaterial1 = ChiselBrushMaterial.CreateInstance(), newBrushMaterial2 = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial = new Material(Shader.Find("Specular"));

                newBrushMaterial1.RenderMaterial = newRenderMaterial;
                newBrushMaterial2.RenderMaterial = newRenderMaterial;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.AreEqual(newRenderMaterial, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));
                Assert.AreEqual(2, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }

        [UnityTest]
        public IEnumerator CreateTwoBrushMaterialsWithSamePhysicsMaterial_RefCountIsTwo()
        {
            using (ChiselBrushMaterial newBrushMaterial1 = ChiselBrushMaterial.CreateInstance(), newBrushMaterial2 = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial = new PhysicMaterial();

                newBrushMaterial1.PhysicsMaterial = newPhysicsMaterial;
                newBrushMaterial2.PhysicsMaterial = newPhysicsMaterial;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.AreEqual(newPhysicsMaterial, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));
                Assert.AreEqual(2, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }


        [UnityTest]
        public IEnumerator RemoveAndAddRenderMaterialToAnotherBrushMaterial_MaterialStillRegistered()
        {
            using (ChiselBrushMaterial newBrushMaterial1 = ChiselBrushMaterial.CreateInstance(), newBrushMaterial2 = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial1 = new Material(Shader.Find("Specular"));
                var newRenderMaterial2 = new Material(Shader.Find("Specular"));

                newBrushMaterial1.RenderMaterial = newRenderMaterial1;
                newBrushMaterial2.RenderMaterial = newRenderMaterial2;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.AreEqual(newRenderMaterial1, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial1.GetInstanceID(), false));
                Assert.AreEqual(newRenderMaterial2, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial2.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial1.GetInstanceID()));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial2.GetInstanceID()));
                newBrushMaterial1.RenderMaterial = newRenderMaterial2;
                newBrushMaterial2.RenderMaterial = newRenderMaterial1;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.AreEqual(newRenderMaterial1, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial1.GetInstanceID(), false));
                Assert.AreEqual(newRenderMaterial2, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial2.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial1.GetInstanceID()));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial2.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newRenderMaterial1);
                UnityEngine.Object.DestroyImmediate(newRenderMaterial2);
            }
        }

        [UnityTest]
        public IEnumerator RemoveAndAddPhysicsMaterialToAnotherBrushMaterial_MaterialStillRegistered()
        {
            using (ChiselBrushMaterial newBrushMaterial1 = ChiselBrushMaterial.CreateInstance(), newBrushMaterial2 = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial1 = new PhysicMaterial();
                var newPhysicsMaterial2 = new PhysicMaterial();

                newBrushMaterial1.PhysicsMaterial = newPhysicsMaterial1;
                newBrushMaterial2.PhysicsMaterial = newPhysicsMaterial2;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.AreEqual(newPhysicsMaterial1, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial1.GetInstanceID(), false));
                Assert.AreEqual(newPhysicsMaterial2, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial2.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial1.GetInstanceID()));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial2.GetInstanceID()));
                newBrushMaterial1.PhysicsMaterial = newPhysicsMaterial2;
                newBrushMaterial2.PhysicsMaterial = newPhysicsMaterial1;
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.AreEqual(newPhysicsMaterial1, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial1.GetInstanceID(), false));
                Assert.AreEqual(newPhysicsMaterial2, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial2.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial1.GetInstanceID()));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial2.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial1);
                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial2);
            }
        }


        [UnityTest]
        public IEnumerator CreateTwoBrushMaterialsWithSameRenderMaterial_DestroyOneBrushMaterial_ManagerKnowsMaterial()
        {
            using (ChiselBrushMaterial newBrushMaterial1 = ChiselBrushMaterial.CreateInstance(), newBrushMaterial2 = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial = new Material(Shader.Find("Specular"));

                newBrushMaterial1.RenderMaterial = newRenderMaterial;
                newBrushMaterial2.RenderMaterial = newRenderMaterial;
                newBrushMaterial1.Dispose();
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.AreEqual(newRenderMaterial, ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetRenderMaterialRefCountByInstanceID(newRenderMaterial.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }

        [UnityTest]
        public IEnumerator CreateTwoBrushMaterialsWithSamePhysicsMaterial_DestroyOneBrushMaterial_ManagerKnowsMaterial()
        {
            using (ChiselBrushMaterial newBrushMaterial1 = ChiselBrushMaterial.CreateInstance(), newBrushMaterial2 = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial = new PhysicMaterial();

                newBrushMaterial1.PhysicsMaterial = newPhysicsMaterial;
                newBrushMaterial2.PhysicsMaterial = newPhysicsMaterial;
                newBrushMaterial1.Dispose();
                yield return null;
                ChiselBrushMaterialManager.Update();

                Assert.AreEqual(newPhysicsMaterial, ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));
                Assert.AreEqual(1, ChiselBrushMaterialManager.GetPhysicsMaterialRefCountByInstanceID(newPhysicsMaterial.GetInstanceID()));

                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }



        [UnityTest]
        public IEnumerator CreateTwoBrushMaterialsWithSameRenderMaterial_DestroyBothBrushMaterials_ManagerDoesNotKnowMaterial()
        {
            using (ChiselBrushMaterial newBrushMaterial1 = ChiselBrushMaterial.CreateInstance(), newBrushMaterial2 = ChiselBrushMaterial.CreateInstance())
            {
                var newRenderMaterial = new Material(Shader.Find("Specular"));

                newBrushMaterial1.RenderMaterial = newRenderMaterial;
                newBrushMaterial2.RenderMaterial = newRenderMaterial;
                newBrushMaterial1.Dispose();
                newBrushMaterial2.Dispose();
                yield return null;
                ChiselBrushMaterialManager.Update();

                LogAssert.Expect(LogType.Error, new Regex("Could not find"));
                Assert.IsNull(ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(newRenderMaterial.GetInstanceID(), false));

                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }

        [UnityTest]
        public IEnumerator CreateTwoBrushMaterialsWithSamePhysicsMaterial_DestroyBothBrushMaterials_ManagerDoesNotKnowMaterial()
        {
            using (ChiselBrushMaterial newBrushMaterial1 = ChiselBrushMaterial.CreateInstance(), newBrushMaterial2 = ChiselBrushMaterial.CreateInstance())
            {
                var newPhysicsMaterial = new PhysicMaterial();

                newBrushMaterial1.PhysicsMaterial = newPhysicsMaterial;
                newBrushMaterial2.PhysicsMaterial = newPhysicsMaterial;
                newBrushMaterial1.Dispose();
                newBrushMaterial2.Dispose();
                yield return null;
                ChiselBrushMaterialManager.Update();

                LogAssert.Expect(LogType.Error, new Regex("Could not find"));
                Assert.IsNull(ChiselBrushMaterialManager.GetPhysicsMaterialByInstanceID(newPhysicsMaterial.GetInstanceID(), false));

                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }
    }
}
