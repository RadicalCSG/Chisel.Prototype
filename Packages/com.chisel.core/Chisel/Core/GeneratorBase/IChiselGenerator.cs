using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Core
{
    public interface IChiselGenerator
    {
        void Reset();
        void Validate();
        //bool Generate(ref ChiselBrushContainer brushContainer);
        void OnEdit(IChiselHandles handles);
        void OnMessages(IChiselMessages messages);
    }
}
