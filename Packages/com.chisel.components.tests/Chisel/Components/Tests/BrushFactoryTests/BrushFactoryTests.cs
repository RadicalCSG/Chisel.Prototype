﻿using UnityEngine;
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
            var surfaceDefinition = new ChiselSurfaceDefinition();
            surfaceDefinition.EnsureSize(6);

            BrushMeshFactory.CreateBox(Vector3.one, 0, out BrushMesh box);
            yield return null;

            var instance = BrushMeshInstance.Create(box, in surfaceDefinition);
            Assert.IsTrue(instance.Valid);
            instance.Destroy();
        }
    }
}
