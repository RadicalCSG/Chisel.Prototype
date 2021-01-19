using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Nodes
{
    public class CompositeNode : ChiselGraphNode
    {
        [Input] public CSGTree child2;

        protected override void Generate()
        {
            Debug.Log("generate box");
        }
    }
}