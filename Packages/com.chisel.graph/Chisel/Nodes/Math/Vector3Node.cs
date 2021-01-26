using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    public class Vector3Node : Node
    {
        [Input] public float x;
        [Input] public float y;
        [Input] public float z;
        [Output] public Vector3 output;

        public ChiselGraph chiselGraph => graph as ChiselGraph;

        public override object GetValue(NodePort port)
        {
            var x = GetInputValue("x", this.x);
            var y = GetInputValue("y", this.y);
            var z = GetInputValue("z", this.z);

            return new Vector3(x, y, z);
        }

        void OnValidate()
        {
            chiselGraph.UpdateProperties();
        }
    }
}