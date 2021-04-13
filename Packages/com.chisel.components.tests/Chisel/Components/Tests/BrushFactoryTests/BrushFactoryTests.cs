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
        public IEnumerator CreateBrushContainerAsset_IsPartOfManager()
        {
            var chiselSurface = new ChiselSurface();
            BrushMeshFactory.CreateBox(Vector3.one, 0, in chiselSurface, out BrushMesh box);
            yield return null;

            var instance = BrushMeshInstance.Create(box);
            Assert.IsTrue(instance.Valid);
            instance.Destroy();
        }


    }
}
