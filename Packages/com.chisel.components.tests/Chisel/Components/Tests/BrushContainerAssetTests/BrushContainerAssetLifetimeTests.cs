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
using System.Collections.Generic;

namespace BrushContainerAssetTests
{
    public sealed class BrushContainerAssetLifetimeTests
    {
        [SetUp] public void Setup() {  }


        public static ChiselBrushContainerAsset CreateBox(Vector3 size, ChiselBrushMaterial material)
        {
            var boxDefinition = new ChiselBoxDefinition()
            {
                bounds = new Bounds(Vector3.zero, size),
                surfaceDefinition = new ChiselSurfaceDefinition()
                {
                    surfaces = new ChiselSurface[]
                    {
                        new ChiselSurface(){ brushMaterial = material, surfaceDescription = SurfaceDescription.Default },
                        new ChiselSurface(){ brushMaterial = material, surfaceDescription = SurfaceDescription.Default },
                        new ChiselSurface(){ brushMaterial = material, surfaceDescription = SurfaceDescription.Default },

                        new ChiselSurface(){ brushMaterial = material, surfaceDescription = SurfaceDescription.Default },
                        new ChiselSurface(){ brushMaterial = material, surfaceDescription = SurfaceDescription.Default },
                        new ChiselSurface(){ brushMaterial = material, surfaceDescription = SurfaceDescription.Default }
                    }
                }
            };
            boxDefinition.Validate();

            var brushContainerAsset = ChiselBrushContainerAsset.Create("Box");
            brushContainerAsset.Generate(boxDefinition);
            return brushContainerAsset;
        }


        [UnityTest]
        public IEnumerator CreateBrushContainerAsset_IsPartOfManager()
        {
            var newBrushContainerAsset = ScriptableObject.CreateInstance<ChiselBrushContainerAsset>();
            yield return null;
            ChiselBrushContainerAssetManager.Update();

            //Assert.False(ChiselBrushContainerAssetManager.IsInUpdateQueue(newBrushContainerAsset)); // should already be done
            Assert.True(ChiselBrushContainerAssetManager.IsRegistered(newBrushContainerAsset));
            UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
        }


        [UnityTest]
        public IEnumerator CreateBrushContainerAsset_Destroy_IsNotPartOfManager()
        {
            var newBrushContainerAsset = ScriptableObject.CreateInstance<ChiselBrushContainerAsset>();
            yield return null;
            ChiselBrushMaterialManager.Update();

            UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
            yield return null;
            ChiselBrushContainerAssetManager.Update();

            //Assert.False(ChiselBrushContainerAssetManager.IsInUnregisterQueue(newBrushContainerAsset));	// should already be done
            Assert.False(ChiselBrushContainerAssetManager.IsRegistered(newBrushContainerAsset));
            UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
        }

        [UnityTest]
        public IEnumerator CreateBrushContainerAsset_SetDirty_IsDirty()
        {
            var newBrushContainerAsset = ScriptableObject.CreateInstance<ChiselBrushContainerAsset>();
            yield return null;
            ChiselBrushContainerAssetManager.Update();

            //Assert.False(ChiselBrushContainerAssetManager.IsInUpdateQueue(newBrushContainerAsset));
            //Assert.False(ChiselBrushContainerAssetManager.IsInUnregisterQueue(newBrushContainerAsset));
            Assert.True(ChiselBrushContainerAssetManager.IsRegistered(newBrushContainerAsset));
            ChiselBrushContainerAssetManager.SetDirty(newBrushContainerAsset);
            //Assert.True(ChiselBrushContainerAssetManager.IsInUpdateQueue(newBrushContainerAsset));
            Assert.True(ChiselBrushContainerAssetManager.IsDirty(newBrushContainerAsset));

            UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
        }
        

        [UnityTest]
        public IEnumerator CreateBrushContainerAssetAndSetDirty_AfterUpdate_IsNotDirty()
        {
            var newBrushContainerAsset = ScriptableObject.CreateInstance<ChiselBrushContainerAsset>();
            yield return null;
            ChiselBrushContainerAssetManager.Update();

            //Assert.False(ChiselBrushContainerAssetManager.IsInUpdateQueue(newBrushContainerAsset));
            //Assert.False(ChiselBrushContainerAssetManager.IsInUnregisterQueue(newBrushContainerAsset));
            Assert.True(ChiselBrushContainerAssetManager.IsRegistered(newBrushContainerAsset));
            ChiselBrushContainerAssetManager.SetDirty(newBrushContainerAsset);
            ChiselBrushContainerAssetManager.Update();
            //Assert.True(ChiselBrushContainerAssetManager.IsInUpdateQueue(newBrushContainerAsset));
            Assert.False(ChiselBrushContainerAssetManager.IsDirty(newBrushContainerAsset));

            UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
        }

        [UnityTest]
        public IEnumerator CreateBrushContainerAsset_HasValidInstance()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushContainerAsset = CreateBox(Vector3.one, newBrushMaterial);
                Assert.IsFalse(newBrushContainerAsset.Instances != null && newBrushContainerAsset.Instances[0].Valid);
                yield return null;
                ChiselBrushContainerAssetManager.Update();

                //Assert.False(ChiselBrushContainerAssetManager.IsInUpdateQueue(newBrushContainerAsset)); // should already be done
                Assert.IsTrue(newBrushContainerAsset.Instances != null && newBrushContainerAsset.Instances[0].Valid);
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
            }
        }

        [UnityTest]
        public IEnumerator CreateBrushContainerAsset_Unregister_DoesNotHaveValidInstance()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushContainerAsset = CreateBox(Vector3.one, newBrushMaterial);
                Assert.IsFalse(newBrushContainerAsset.Instances != null && newBrushContainerAsset.Instances[0].Valid);
                yield return null;
                ChiselBrushContainerAssetManager.Update();

                ChiselBrushContainerAssetManager.Unregister(newBrushContainerAsset);
                yield return null;
                ChiselBrushContainerAssetManager.Update();

                //Assert.False(ChiselBrushContainerAssetManager.IsInUpdateQueue(newBrushContainerAsset)); // should already be done
                Assert.IsFalse(newBrushContainerAsset.Instances != null && newBrushContainerAsset.Instances[0].Valid);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushContainerAsset_Destroy_InstanceDestroyedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushContainerAssetDelegate localDelegate = delegate (ChiselBrushContainerAsset brushContainerAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushContainerAsset = CreateBox(Vector3.one, newBrushMaterial);
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceDestroyed -= localDelegate;
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceDestroyed += localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceDestroyed -= localDelegate;
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushContainerAssetWithBrushMaterial_ModifyBrushMaterialLayerUsage_InstanceChangedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushContainerAssetDelegate localDelegate = delegate (ChiselBrushContainerAsset brushContainerAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.LayerUsage = LayerUsageFlags.None;
                var newBrushContainerAsset = CreateBox(Vector3.one, newBrushMaterial);
                yield return null;

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.LayerUsage = LayerUsageFlags.Renderable;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushContainerAssetWithBrushMaterial_ModifyBrushMaterialRenderMaterial_InstanceChangedEventIsCalled()
        {
            var newRenderMaterial = new Material(Shader.Find("Specular"));

            var hasBeenCalled = false;
            OnBrushContainerAssetDelegate localDelegate = delegate (ChiselBrushContainerAsset brushContainerAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.RenderMaterial = null;
                var newBrushContainerAsset = CreateBox(Vector3.one, newBrushMaterial);
                yield return null;

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.RenderMaterial = newRenderMaterial;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
                UnityEngine.Object.DestroyImmediate(newRenderMaterial);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushContainerAssetWithBrushMaterial_ModifyBrushMaterialPhysicsMaterial_InstanceChangedEventIsCalled()
        {
            var newPhysicsMaterial = new PhysicMaterial();

            var hasBeenCalled = false;
            OnBrushContainerAssetDelegate localDelegate = delegate (ChiselBrushContainerAsset brushContainerAsset)
            { hasBeenCalled = true; };


            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.PhysicsMaterial = null;
                var newBrushContainerAsset = CreateBox(Vector3.one, newBrushMaterial);
                yield return null;

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                newBrushMaterial.PhysicsMaterial = newPhysicsMaterial;
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
                UnityEngine.Object.DestroyImmediate(newPhysicsMaterial);
            }
        }

        
        [UnityTest]
        public IEnumerator CreateBrushWithBrushContainerAsset_ModifyBrushContainerAsset_BrushIsDirty()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushContainerAsset	= CreateBox(Vector3.one, newBrushMaterial);
                var brushGameObject		= EditorUtility.CreateGameObjectWithHideFlags("Brush", HideFlags.None);
                var brush				= brushGameObject.AddComponent<ChiselBrush>();
                brush.BrushContainerAsset = newBrushContainerAsset;
            
                yield return null;
                ChiselBrushContainerAssetManager.Update();
                ChiselNodeHierarchyManager.Update();

                newBrushContainerAsset.SetDirty();
                ChiselBrushContainerAssetManager.Update();

                Assert.IsTrue(brush.Dirty);
                yield return null;
                UnityEngine.Object.DestroyImmediate(brushGameObject);
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
            }
        }

        
        [UnityTest]
        public IEnumerator CreateBrushWithBrushContainerAssetWithBrushMaterial_ModifyBrushMaterial_BrushIsDirty()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                newBrushMaterial.LayerUsage = LayerUsageFlags.None;
                var newBrushContainerAsset	= CreateBox(Vector3.one, newBrushMaterial);
                var brushGameObject		= EditorUtility.CreateGameObjectWithHideFlags("Brush", HideFlags.None);
                var brush				= brushGameObject.AddComponent<ChiselBrush>();
                brush.BrushContainerAsset = newBrushContainerAsset;
            
                yield return null;
                ChiselBrushContainerAssetManager.Update();
                ChiselNodeHierarchyManager.Update();

                newBrushMaterial.LayerUsage = LayerUsageFlags.Renderable;
                ChiselBrushContainerAssetManager.Update();

                Assert.IsTrue(brush.Dirty);
                yield return null;
                UnityEngine.Object.DestroyImmediate(brushGameObject);
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
            }
        }


        
        [UnityTest]
        public IEnumerator CreateBrushWithBrushContainerAsset_GetUsedBrushContainerAssets_IsNotNull()
        {
            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushContainerAsset	= CreateBox(Vector3.one, newBrushMaterial);
                var brushGameObject		= EditorUtility.CreateGameObjectWithHideFlags("Brush", HideFlags.None);
                var brush				= brushGameObject.AddComponent<ChiselBrush>();
                brush.BrushContainerAsset = newBrushContainerAsset;
            
                yield return null;
                ChiselBrushContainerAssetManager.Update();
                ChiselNodeHierarchyManager.Update();
                var usedBrushes = new List<ChiselBrushContainerAsset>();
                
                Assert.IsTrue(brush.GetUsedGeneratedBrushes(usedBrushes));
                Assert.AreNotEqual(0, usedBrushes.Count);
                yield return null;
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushContainerAsset_Created_InstanceChangedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushContainerAssetDelegate localDelegate = delegate (ChiselBrushContainerAsset brushContainerAsset)
            { hasBeenCalled = true; };

            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                var newBrushContainerAsset = CreateBox(Vector3.one, newBrushMaterial);
                yield return null;


                Assert.IsTrue(hasBeenCalled);

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
            }
        }


        [UnityTest]
        public IEnumerator CreateBrushContainerAsset_SetDirty_InstanceChangedEventIsCalled()
        {
            var hasBeenCalled = false;
            OnBrushContainerAssetDelegate localDelegate = delegate (ChiselBrushContainerAsset brushContainerAsset)
            { hasBeenCalled = true; };

            using (var newBrushMaterial = ChiselBrushMaterial.CreateInstance())
            {
                var newBrushContainerAsset = CreateBox(Vector3.one, newBrushMaterial);
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged += localDelegate;
                yield return null;

                newBrushContainerAsset.SetDirty();
                yield return null;

                Assert.IsTrue(hasBeenCalled);

                ChiselBrushContainerAssetManager.OnBrushMeshInstanceChanged -= localDelegate;
                UnityEngine.Object.DestroyImmediate(newBrushContainerAsset);
            }
        }
    }
}
