﻿using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using UnityEditor.SceneManagement;
using Chisel.Core;
/*
namespace FoundationTests
{ 
    [TestFixture]
    public partial class SetGetBrushLocalToTreeTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        static Matrix4x4 testMatrix1 = Matrix4x4.TRS(Vector3.one,     Quaternion.Euler(30,45,60), Vector3.one * 2);
        static Matrix4x4 testMatrix2 = Matrix4x4.TRS(Vector3.one * 2, Quaternion.Euler(60,90,30), Vector3.one * 5);

        [Test]
        public void Brush_SetLocalToTree_GetLocalToTreeIsSame()
        {
            var brush1 = CSGTreeBrush.Create();
            var brush2 = CSGTreeBrush.Create();
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);

            brush1.LocalTransformation = testMatrix1;
            brush2.LocalTransformation = testMatrix2;

            Assert.AreEqual(testMatrix1, brush1.LocalTransformation);
            Assert.AreEqual(testMatrix2, brush2.LocalTransformation);
            Assert.IsTrue(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
        }

        [Test]
        public void Brush_SetLocalToTreeToMultipleValues_GetLocalToTreeIsLastSetValue()
        {
            var brush = CSGTreeBrush.Create();
            CompactHierarchyManager.ClearDirty(brush);

            brush.LocalTransformation = testMatrix1;
            brush.LocalTransformation = testMatrix2;

            Assert.AreEqual(testMatrix2, brush.LocalTransformation);
            Assert.IsTrue(brush.Dirty);
        }

        [Test]
        public void Brush_DefaultLocalToTree_IsIdentity()
        {
            var brush = CSGTreeBrush.Create();

            Assert.AreEqual(Matrix4x4.identity, brush.LocalTransformation);
        }
    }
}*/