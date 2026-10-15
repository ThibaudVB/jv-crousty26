using UnityEngine.InputSystem;

// Menus remain usable with either device. Gameplay follows the selected mode,
// falling back to keyboard/mouse if a controller is disconnected.
public static class CroustyInput
{
    private static bool menuController;
    public static bool MenuUsesController => menuController && Gamepad.current != null;

    public static void UpdateMenuDevice()
    {
        var mouse = Mouse.current;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 1f || mouse.leftButton.wasPressedThisFrame) ||
            keyboard != null && keyboard.anyKey.wasPressedThisFrame) menuController = false;
        var pad = Gamepad.current;
        if (pad != null && (pad.dpad.ReadValue().sqrMagnitude > .1f ||
            pad.leftStick.ReadValue().sqrMagnitude > .2f || pad.buttonSouth.wasPressedThisFrame ||
            pad.buttonEast.wasPressedThisFrame)) menuController = true;
    }
    public static bool UsesController => CroustySettings.Get(CroustySettings.Option.Input) == 1 && Gamepad.current != null;
    public static Gamepad Pad => UsesController ? Gamepad.current : null;
    public static Keyboard Keyboard => UsesController ? null : UnityEngine.InputSystem.Keyboard.current;
}
