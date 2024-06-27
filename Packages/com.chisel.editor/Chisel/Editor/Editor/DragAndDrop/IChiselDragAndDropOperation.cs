namespace Chisel.Editors
{
    public interface IChiselDragAndDropOperation
    {
        void UpdateDrag();
        void PerformDrag();
        void CancelDrag();
    }
}
