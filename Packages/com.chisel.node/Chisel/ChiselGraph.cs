using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    [CreateAssetMenu(fileName = "New Chisel Graph", menuName = "Chisel Graph")]
    public class ChiselGraph : NodeGraph
    {
        public ChiselNode active;
        public ChiselGraphInstance instance;
    }
}