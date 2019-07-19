using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chisel.Editors
{
    public interface IChiselDragAndDropOperation
    {
        void UpdateDrag();
        void PerformDrag();
        void CancelDrag();
    }
}
