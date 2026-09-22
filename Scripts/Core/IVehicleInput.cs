namespace RacingSim.Core
{
    /// <summary>
    /// Abstraction layer for vehicle input.
    /// Decouples game logic from Unity's input API.
    /// Implementations: LegacyInputProvider, NewInputProvider,
    /// AIInputProvider, ReplayInputProvider.
    /// </summary>
    public interface IVehicleInput
    {
        float Steering { get; }
        float Throttle { get; }
        float Brake { get; }
        float Clutch { get; }
        bool ShiftUp { get; }
        bool ShiftDown { get; }
        bool DRS { get; }
        bool PitLimiter { get; }
    }
}
