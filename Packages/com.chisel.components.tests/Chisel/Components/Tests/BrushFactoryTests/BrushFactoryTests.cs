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
            var chiselSurface = new ChiselSurface();
            var box = BrushMeshFactory.CreateBox(Vector3.one, in chiselSurface);
            yield return null;

            var instance = BrushMeshInstance.Create(box);
            Assert.IsTrue(instance.Valid);
            instance.Destroy();
        }


    }
}
