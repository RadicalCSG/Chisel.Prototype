using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Chisel;
using Chisel.Core;
using Chisel.Components;

namespace BrushMeshAssetTests
{
    public sealed class BrushMeshAssetLifetimeTests
    {
        [SetUp] public void Setup() {  }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_IsPartOfManager()
        {
            var newBrushMeshAsset = ScriptableObject.CreateInstance<ChiselGeneratedBrushes>();
            yield return null;
            ChiselGeneratedBrushesManager.Update();

            //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset)); // should already be done
            Assert.True(ChiselGeneratedBrushesManager.IsRegistered(newBrushMeshAsset));
            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_Destroy_IsNotPartOfManager()
        {
            var newBrushMeshAsset = ScriptableObject.CreateInstance<ChiselGeneratedBrushes>();
            yield return null;
            ChiselBrushMaterialManager.Update();

            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            yield return null;
            ChiselGeneratedBrushesManager.Update();
            
            //Assert.False(CSGBrushMeshAssetManager.IsInUnregisterQueue(newBrushMeshAsset));	// should already be done
            Assert.False(ChiselGeneratedBrushesManager.IsRegistered(newBrushMeshAsset));
            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
        }

        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_SetDirty_IsDirty()
        {
            var newBrushMeshAsset = ScriptableObject.CreateInstance<ChiselGeneratedBrushes>();
            yield return null;
            ChiselGeneratedBrushesManager.Update();

            //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset));
            //Assert.False(CSGBrushMeshAssetManager.IsInUnregisterQueue(newBrushMeshAsset));
            Assert.True(ChiselGeneratedBrushesManager.IsRegistered(newBrushMeshAsset));
            ChiselGeneratedBrushesManager.SetDirty(newBrushMeshAsset);
            //Assert.True(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset));
            Assert.True(ChiselGeneratedBrushesManager.IsDirty(newBrushMeshAsset));

            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
        }
        

        [UnityTest]
        public IEnumerator CreateBrushMeshAssetAndSetDirty_AfterUpdate_IsNotDirty()
        {
            var newBrushMeshAsset = ScriptableObject.CreateInstance<ChiselGeneratedBrushes>();
            yield return null;
            ChiselGeneratedBrushesManager.Update();

            //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset));
            //Assert.False(CSGBrushMeshAssetManager.IsInUnregisterQueue(newBrushMeshAsset));
            Assert.True(ChiselGeneratedBrushesManager.IsRegistered(newBrushMeshAsset));
            ChiselGeneratedBrushesManager.SetDirty(newBrushMeshAsset);
            ChiselGeneratedBrushesManager.Update();
            //Assert.True(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset));
            Assert.False(ChiselGeneratedBrushesManager.IsDirty(newBrushMeshAsset));

            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
        }

        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_HasValidInstance()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                Assert.IsFalse(newBrushMeshAsset.Instances != null && newBrushMeshAsset.Instances[0].Valid);
                yield return null;
                ChiselGeneratedBrushesManager.Update();

                //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset)); // should already be done
                Assert.IsTrue(newBrushMeshAsset.Instances != null && newBrushMeshAsset.Instances[0].Valid);
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }

        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_Unregister_DoesNotHaveValidInstance()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                Assert.IsFalse(newBrushMeshAsset.Instances != null && newBrushMeshAsset.Instances[0].Valid);
                yield return null;
                ChiselGeneratedBrushesManager.Update();

                ChiselGeneratedBrushesManager.Unregister(newBrushMeshAsset);
                yield return null;
                ChiselGeneratedBrushesManager.Update();

                //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset)); // should already be done
                Assert.IsFalse(newBrushMeshAsset.Instances != null && newBrushMeshAsset.Instances[0].Valid);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_Destroy_InstanceDestroyedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (ChiselGeneratedBrushes brushMeshAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceDestroyed -= localDelegate;
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceDestroyed += localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceDestroyed -= localDelegate;
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAssetWithBrushMaterial_ModifyBrushMaterialLayerUsage_InstanceChangedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (ChiselGeneratedBrushes brushMeshAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.LayerUsage = LayerUsageFlags.None;
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                yield return null;

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.LayerUsage = LayerUsageFlags.Renderable;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAssetWithBrushMaterial_ModifyBrushMaterialRenderMaterial_InstanceChangedEventIsCalled()
        {
            var newRenderMaterial = new Material(Shader.Find("Specular"));

            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (ChiselGeneratedBrushes brushMeshAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.RenderMaterial = null;
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                yield return null;

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.RenderMaterial = newRenderMaterial;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAssetWithBrushMaterial_ModifyBrushMaterialPhysicsMaterial_InstanceChangedEventIsCalled()
        {
            var newPhysicsMaterial = new PhysicMaterial();

            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (ChiselGeneratedBrushes brushMeshAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.PhysicsMaterial = null;
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                yield return null;

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }

        
        [UnityTest]
        public IEnumerator CreateBrushWithBrushMeshAsset_ModifyBrushMeshAsset_BrushIsDirty()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushMeshAsset	= BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                var brushGameObject		= EditorUtility.CreateGameObjectWithHideFlags("Brush", HideFlags.None);
                var brush				= brushGameObject.AddComponent<CSGBrush>();
                brush.BrushMeshAsset = newBrushMeshAsset;
            
                yield return null;
                ChiselGeneratedBrushesManager.Update();
                CSGNodeHierarchyManager.Update();

                newBrushMeshAsset.SetDirty();
                ChiselGeneratedBrushesManager.Update();

                Assert.IsTrue(brush.Dirty);
                yield return null;
                UnityEngine.Object.DestroyImmediate(brushGameObject);
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }

        
        [UnityTest]
        public IEnumerator CreateBrushWithBrushMeshAssetWithBrushMaterial_ModifyBrushMaterial_BrushIsDirty()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.LayerUsage = LayerUsageFlags.None;
                var newBrushMeshAsset	= BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                var brushGameObject		= EditorUtility.CreateGameObjectWithHideFlags("Brush", HideFlags.None);
                var brush				= brushGameObject.AddComponent<CSGBrush>();
                brush.BrushMeshAsset = newBrushMeshAsset;
            
                yield return null;
                ChiselGeneratedBrushesManager.Update();
                CSGNodeHierarchyManager.Update();

                newBrushMaterial.LayerUsage = LayerUsageFlags.Renderable;
                ChiselGeneratedBrushesManager.Update();

                Assert.IsTrue(brush.Dirty);
                yield return null;
                UnityEngine.Object.DestroyImmediate(brushGameObject);
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }


        
        [UnityTest]
        public IEnumerator CreateBrushWithBrushMeshAsset_GetUsedBrushMeshAssets_IsNotNull()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushMeshAsset	= BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                var brushGameObject		= EditorUtility.CreateGameObjectWithHideFlags("Brush", HideFlags.None);
                var brush				= brushGameObject.AddComponent<CSGBrush>();
                brush.BrushMeshAsset = newBrushMeshAsset;
            
                yield return null;
                ChiselGeneratedBrushesManager.Update();
                CSGNodeHierarchyManager.Update();
            
                Assert.IsNotNull(brush.GetUsedBrushMeshAssets());
                Assert.AreNotEqual(0, brush.GetUsedBrushMeshAssets());
                yield return null;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_Created_InstanceChangedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (ChiselGeneratedBrushes brushMeshAsset)
            { hasBeenCalled = true; };

            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged += localDelegate;
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                yield return null;


                Assert.IsTrue(hasBeenCalled);

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_SetDirty_InstanceChangedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (ChiselGeneratedBrushes brushMeshAsset)
            { hasBeenCalled = true; };

            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged += localDelegate;
                yield return null;

                newBrushMeshAsset.SetDirty();
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselGeneratedBrushesManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }
    }
}
