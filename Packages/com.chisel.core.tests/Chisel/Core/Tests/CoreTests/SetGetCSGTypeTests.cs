using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;

namespace FoundationTests
{ 
    [TestFixture]
    public partial class SetGetCSGTypeTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        [Test]
        public void Operation_SetCSGType_GetCSGTypeIsSame()
        {
            var branch1 = CSGTreeBranch.Create();
            var branch2 = CSGTreeBranch.Create();
            var branch3 = CSGTreeBranch.Create();
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);
            CompactHierarchyManager.ClearDirty(branch3);

            branch1.Operation = CSGOperationType.Additive;
            branch2.Operation = CSGOperationType.Subtractive;
            branch3.Operation = CSGOperationType.Intersecting;

            Assert.AreEqual(CSGOperationType.Additive, branch1.Operation);
            Assert.AreEqual(CSGOperationType.Subtractive, branch2.Operation);
            Assert.AreEqual(CSGOperationType.Intersecting, branch3.Operation);
            Assert.IsFalse(branch1.Dirty);
            Assert.IsTrue(branch2.Dirty);
            Assert.IsTrue(branch3.Dirty);
        }

        [Test]
        public void Brush_SetCSGType_GetCSGTypeIsSame()
        {
            var brush1 = CSGTreeBrush.Create();
            var brush2 = CSGTreeBrush.Create();
            var brush3 = CSGTreeBrush.Create();
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);

            brush1.Operation = CSGOperationType.Additive;
            brush2.Operation = CSGOperationType.Subtractive;
            brush3.Operation = CSGOperationType.Intersecting;

            Assert.AreEqual(CSGOperationType.Additive, brush1.Operation);
            Assert.AreEqual(CSGOperationType.Subtractive, brush2.Operation);
            Assert.AreEqual(CSGOperationType.Intersecting, brush3.Operation);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsTrue(brush3.Dirty);
        }


        [Test]
        public void Branch_DefaultCSGType_IsAdditive()
        {
            var branch = CSGTreeBranch.Create();

            Assert.AreEqual(CSGOperationType.Additive, branch.Operation);
        }

        [Test]
        public void Brush_DefaultCSGType_IsAdditive()
        {
            var brush = CSGTreeBrush.Create();

            Assert.AreEqual(CSGOperationType.Additive, brush.Operation);
        }
    }
}