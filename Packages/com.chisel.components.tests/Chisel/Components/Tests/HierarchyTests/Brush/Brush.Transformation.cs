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
	public partial class Brush_Transformation
	{
		[SetUp] public void Setup() { TestUtility.ClearScene(); }

		// TODO: create transformation tests
		// Move brush, generated box is also moved
		// brush in model, move brush, generated box is also moved
		// brush in model, move model, generated box is not moved in mesh, but meshrenderer is moved in worldspace
	}
}