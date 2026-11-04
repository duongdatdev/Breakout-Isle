namespace BreakoutIsle.Gameplay
{
    /// <summary>Derived power state. The owning switch is the only persisted source of truth.</summary>
    public interface IPowerConsumer
    {
        bool IsPowered { get; }
        void SetPowered(bool powered);
    }
}
