using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using System.Reflection;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselNode), isFallback = true)]
    [CanEditMultipleObjects]
    public sealed class CSGNodeEditor : ChiselNodeEditor<CSGModel>
    {
    }
}
