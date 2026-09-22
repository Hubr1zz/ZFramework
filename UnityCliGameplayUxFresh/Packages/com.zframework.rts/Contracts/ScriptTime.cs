namespace ZFramework.RTS
{
    public readonly struct ScriptTime
    {
        public ScriptTime(float deltaTime, float unscaledDeltaTime, long frameIndex)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            FrameIndex = frameIndex;
        }

        public float DeltaTime { get; }
        public float UnscaledDeltaTime { get; }
        public long FrameIndex { get; }
    }
}
