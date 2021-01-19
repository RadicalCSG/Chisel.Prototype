using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chisel.Core
{
    public class Test : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            var allNodes = CSGManager.AllTreeNodes;
            print(allNodes.Length);

            foreach (var node in allNodes)
                print(node);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}