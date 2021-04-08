using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    // TODO: merge with IChiselGenerator / rename
    public interface IBrushGenerator
    {
        bool Generate(ref CSGTreeNode node, int userID, CSGOperationType operation);
    }
}
