using UnityEngine.InputSystem;

// Menus remain usable with either device. Gameplay follows the selected mode,
// falling back to keyboard/mouse if a controller is disconnected.
public static class CroustyInput
{
    public static bool UsesController => CroustySettings.Get(CroustySettings.Option.Input) == 1 && Gamepad.current != null;
    public static Gamepad Pad => UsesController ? Gamepad.current : null;
    public static Keyboard Keyboard => UsesController ? null : UnityEngine.InputSystem.Keyboard.current;
}
