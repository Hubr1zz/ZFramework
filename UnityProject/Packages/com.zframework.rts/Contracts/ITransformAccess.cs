namespace ZFramework.RTS
{
    public interface ITransformAccess
    {
        float PositionX { get; set; }
        float PositionY { get; set; }
        float PositionZ { get; set; }
        void Translate(float x, float y, float z);
    }
}
