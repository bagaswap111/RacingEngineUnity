using UnityEngine;

namespace RacingSim.Core
{
    /// <summary>
    /// Legacy Input Manager implementation of IVehicleInput.
    /// Wraps UnityEngine.Input for keyboard/gamepad.
    /// Replace with NewInputProvider for steering wheel + FFB.
    /// </summary>
    public class LegacyInputProvider : IVehicleInput
    {
        public float Steering => Input.GetAxis("Horizontal");
        public float Throttle => Mathf.Max(0f, Input.GetAxis("Vertical"));
        public float Brake => Mathf.Max(0f, -Input.GetAxis("Vertical"));
        public float Clutch => Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
        public bool ShiftUp => Input.GetKeyDown(KeyCode.UpArrow);
        public bool ShiftDown => Input.GetKeyDown(KeyCode.DownArrow);
        public bool DRS => Input.GetKey(KeyCode.D);
        public bool PitLimiter => Input.GetKey(KeyCode.L);
    }
}
