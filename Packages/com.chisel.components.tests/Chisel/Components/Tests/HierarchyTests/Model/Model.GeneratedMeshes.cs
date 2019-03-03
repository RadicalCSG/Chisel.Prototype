using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HierarchyTests
{
	public partial class Model_GeneratedMeshes
	{
		[SetUp] public void Setup() { TestUtility.ClearScene(); }

		// TODO: create generated meshes tests (CSGGeneratedMeshManager / CSGSharedUnityMeshManager)
		// TODO: test if setting a brushMesh on a brush will get the brushMesh/node combo registered in CSGNodeHierarchyManager
		// TODO: test brushmeshasset changes changing brushes etc.
		// TODO: test surfaceasset changes changing brushes/brushmeshes etc.
	}
}