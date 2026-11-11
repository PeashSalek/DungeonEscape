using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central, rebindable input for the whole game.
/// Actions are created in code with default keyboard/mouse bindings,
/// and any player rebinds are saved to PlayerPrefs as JSON overrides.
/// </summary>
public static class GameInput
{
    const string PrefsKey = "input_binding_overrides";
    static InputActionMap map;

    public static InputActionMap Map { get { Init(); return map; } }

    public static InputAction Move => Get("Move");
    public static InputAction Look => Get("Look");
    public static InputAction Sprint => Get("Sprint");
    public static InputAction Jump => Get("Jump");
    public static InputAction Dodge => Get("Dodge");
    public static InputAction LightAttack => Get("LightAttack");
    public static InputAction HeavyAttack => Get("HeavyAttack");
    public static InputAction LockOn => Get("LockOn");
    public static InputAction Interact => Get("Interact");
    public static InputAction UsePotion => Get("UsePotion");
    public static InputAction Pause => Get("Pause");

    /// <summary>Actions the player may rebind in the Settings menu (Move is rebound per direction).</summary>
    public static readonly string[] RebindableActions =
        { "Move", "Sprint", "Jump", "Dodge", "LightAttack", "HeavyAttack", "LockOn", "Interact", "UsePotion" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (map != null) map.Disable();
        map = null;
    }

    static InputAction Get(string name)
    {
        Init();
        return map.FindAction(name, true);
    }

    public static void Init()
    {
        if (map != null) return;
        map = new InputActionMap("Gameplay");

        var move = map.AddAction("Move", InputActionType.Value);
        move.expectedControlType = "Vector2";
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        var look = map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
        look.expectedControlType = "Vector2";

        map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
        map.AddAction("Dodge", InputActionType.Button, "<Keyboard>/leftCtrl");
        map.AddAction("LightAttack", InputActionType.Button, "<Mouse>/leftButton");
        map.AddAction("HeavyAttack", InputActionType.Button, "<Mouse>/rightButton");
        var lockOn = map.AddAction("LockOn", InputActionType.Button, "<Mouse>/middleButton");
        lockOn.AddBinding("<Keyboard>/tab");
        map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
        map.AddAction("UsePotion", InputActionType.Button, "<Keyboard>/q");
        map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");

        string saved = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            try { map.LoadBindingOverridesFromJson(saved); }
            catch { PlayerPrefs.DeleteKey(PrefsKey); }
        }
        map.Enable();
    }

    public static void SaveBindings()
    {
        PlayerPrefs.SetString(PrefsKey, Map.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void ResetBindings()
    {
        Map.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PrefsKey);
    }

    /// <summary>Human-readable key name for UI, e.g. "E" or "LMB".</summary>
    public static string DisplayName(InputAction action, int bindingIndex = -1)
    {
        if (bindingIndex < 0)
            return action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
        return action.GetBindingDisplayString(bindingIndex);
    }
}
