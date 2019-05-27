using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Chisel;
using Chisel.Core;
using Chisel.Assets;
using Chisel.Components;

namespace BrushMeshAssetTests
{
    public sealed class BrushMeshAssetLifetimeTests
    {
        [SetUp] public void Setup() {  }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_IsPartOfManager()
        {
            var newBrushMeshAsset = ScriptableObject.CreateInstance<CSGBrushMeshAsset>();
            yield return null;
            CSGBrushMeshAssetManager.Update();

            //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset)); // should already be done
            Assert.True(CSGBrushMeshAssetManager.IsRegistered(newBrushMeshAsset));
            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_Destroy_IsNotPartOfManager()
        {
            var newBrushMeshAsset = ScriptableObject.CreateInstance<CSGBrushMeshAsset>();
            yield return null;
            ChiselBrushMaterialManager.Update();

            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            yield return null;
            CSGBrushMeshAssetManager.Update();
            
            //Assert.False(CSGBrushMeshAssetManager.IsInUnregisterQueue(newBrushMeshAsset));	// should already be done
            Assert.False(CSGBrushMeshAssetManager.IsRegistered(newBrushMeshAsset));
            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
        }

        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_SetDirty_IsDirty()
        {
            var newBrushMeshAsset = ScriptableObject.CreateInstance<CSGBrushMeshAsset>();
            yield return null;
            CSGBrushMeshAssetManager.Update();

            //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset));
            //Assert.False(CSGBrushMeshAssetManager.IsInUnregisterQueue(newBrushMeshAsset));
            Assert.True(CSGBrushMeshAssetManager.IsRegistered(newBrushMeshAsset));
            CSGBrushMeshAssetManager.SetDirty(newBrushMeshAsset);
            //Assert.True(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset));
            Assert.True(CSGBrushMeshAssetManager.IsDirty(newBrushMeshAsset));

            UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
        }
        

        [UnityTest]
        public IEnumerator CreateBrushMeshAssetAndSetDirty_AfterUpdate_IsNotDirty()
        {
            var newBrushMeshAsset = ScriptableObject.CreateInstance<CSGBrushMeshAsset>();
            yield return null;
            CSGBrushMeshAssetManager.Update();

            //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset));
            //Assert.False(CSGBrushMeshAssetManager.IsInUnregisterQueue(newBrushMeshAsset));
            Assert.True(CSGBrushMeshAssetManager.IsRegistered(newBrushMeshAsset));
            CSGBrushMeshAssetManager.SetDirty(newBrushMeshAsset);
            CSGBrushMeshAssetManager.Update();
            //Assert.True(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset));
            Assert.False(CSGBrushMeshAssetManager.IsDirty(newBrushMeshAsset));

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
                CSGBrushMeshAssetManager.Update();

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
                CSGBrushMeshAssetManager.Update();

                CSGBrushMeshAssetManager.Unregister(newBrushMeshAsset);
                yield return null;
                CSGBrushMeshAssetManager.Update();

                //Assert.False(CSGBrushMeshAssetManager.IsInUpdateQueue(newBrushMeshAsset)); // should already be done
                Assert.IsFalse(newBrushMeshAsset.Instances != null && newBrushMeshAsset.Instances[0].Valid);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_Destroy_InstanceDestroyedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (CSGBrushMeshAsset brushMeshAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                CSGBrushMeshAssetManager.OnBrushMeshInstanceDestroyed -= localDelegate;
                CSGBrushMeshAssetManager.OnBrushMeshInstanceDestroyed += localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                CSGBrushMeshAssetManager.OnBrushMeshInstanceDestroyed -= localDelegate;
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAssetWithBrushMaterial_ModifyBrushMaterialLayerUsage_InstanceChangedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (CSGBrushMeshAsset brushMeshAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.LayerUsage = LayerUsageFlags.None;
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                yield return null;

                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.LayerUsage = LayerUsageFlags.Renderable;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAssetWithBrushMaterial_ModifyBrushMaterialRenderMaterial_InstanceChangedEventIsCalled()
        {
            var newRenderMaterial = new Material(Shader.Find("Specular"));

            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (CSGBrushMeshAsset brushMeshAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.RenderMaterial = null;
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                yield return null;

                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.RenderMaterial = newRenderMaterial;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAssetWithBrushMaterial_ModifyBrushMaterialPhysicsMaterial_InstanceChangedEventIsCalled()
        {
            var newPhysicsMaterial = new PhysicMaterial();

            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (CSGBrushMeshAsset brushMeshAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.PhysicsMaterial = null;
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                yield return null;

                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
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
                CSGBrushMeshAssetManager.Update();
                CSGNodeHierarchyManager.Update();

                newBrushMeshAsset.SetDirty();
                CSGBrushMeshAssetManager.Update();

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
                CSGBrushMeshAssetManager.Update();
                CSGNodeHierarchyManager.Update();

                newBrushMaterial.LayerUsage = LayerUsageFlags.Renderable;
                CSGBrushMeshAssetManager.Update();

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
                CSGBrushMeshAssetManager.Update();
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
            OnBrushMeshAssetDelegate localDelegate = delegate (CSGBrushMeshAsset brushMeshAsset)
            { hasBeenCalled = true; };

            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                yield return null;


                Assert.IsTrue(hasBeenCalled);

                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushMeshAsset_SetDirty_InstanceChangedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushMeshAssetDelegate localDelegate = delegate (CSGBrushMeshAsset brushMeshAsset)
            { hasBeenCalled = true; };

            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushMeshAsset = BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, newBrushMaterial);
                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                yield return null;

                newBrushMeshAsset.SetDirty();
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                CSGBrushMeshAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushMeshAsset);
            }
        }
    }
}
