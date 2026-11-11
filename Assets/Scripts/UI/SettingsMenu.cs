using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Settings panel: rebindable controls (saved between sessions), mouse sensitivity
/// and master volume. Opened from the main menu and the pause menu.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    GameObject root;
    readonly List<(Text label, Func<string> value)> rows = new List<(Text, Func<string>)>();
    InputActionRebindingExtensions.RebindingOperation rebind;
    Text statusText;
    Action onClose;

    public bool IsOpen => root != null && root.activeSelf;
    public static int ClosedFrame = -1;

    static readonly (string action, int part, string label)[] Bindings =
    {
        ("Move", 1, "Move forward"), ("Move", 2, "Move back"), ("Move", 3, "Move left"), ("Move", 4, "Move right"),
        ("Sprint", 0, "Sprint"), ("Jump", 0, "Jump"), ("Dodge", 0, "Dodge roll"),
        ("LightAttack", 0, "Light attack"), ("HeavyAttack", 0, "Heavy attack"), ("LockOn", 0, "Lock on"),
        ("Interact", 0, "Interact"), ("UsePotion", 0, "Use potion"),
    };

    public static SettingsMenu Create(Transform parent, int sortOrder)
    {
        var go = new GameObject("SettingsMenu");
        go.transform.SetParent(parent, false);
        var m = go.AddComponent<SettingsMenu>();
        m.Build(sortOrder);
        return m;
    }

    void Build(int sortOrder)
    {
        var canvas = UIFactory.CreateCanvas("Settings Canvas", sortOrder);
        canvas.transform.SetParent(transform, false);
        root = canvas.gameObject;
        var c = new Vector2(0.5f, 0.5f);
        UIFactory.CreateImage(root.transform, "Dim", new Color(0, 0, 0, 0.88f), Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        UIFactory.CreateText(root.transform, "Title", "SETTINGS", 60, TextAnchor.MiddleCenter, UIFactory.Gold, c, c, c, new Vector2(0, 450), new Vector2(1000, 80));
        UIFactory.CreateText(root.transform, "Hint", "Click a control, then press the new key or mouse button. Esc cancels.", 24,
            TextAnchor.MiddleCenter, UIFactory.Parchment, c, c, c, new Vector2(0, 395), new Vector2(1400, 40));

        float y = 330;
        foreach (var b in Bindings)
        {
            var binding = b;
            UIFactory.CreateText(root.transform, "L_" + b.label, b.label, 28, TextAnchor.MiddleRight, UIFactory.Parchment, c, c, new Vector2(1, 0.5f), new Vector2(-30, y), new Vector2(400, 44));
            var btn = UIFactory.CreateButton(root.transform, "B_" + b.label, "", c, new Vector2(140, y), new Vector2(300, 46), null, 26);
            var label = btn.GetComponentInChildren<Text>();
            btn.onClick.AddListener(() => StartRebind(binding.action, binding.part, label));
            rows.Add((label, () => KeyName(binding.action, binding.part)));
            y -= 52;
        }

        y -= 10;
        UIFactory.CreateText(root.transform, "SensLabel", "Mouse sensitivity", 28, TextAnchor.MiddleRight, UIFactory.Parchment, c, c, new Vector2(1, 0.5f), new Vector2(-30, y), new Vector2(400, 44));
        Slider(new Vector2(140, y), 0.3f, 3f, PlayerPrefs.GetFloat("mouse_sensitivity", 1f), v => { PlayerPrefs.SetFloat("mouse_sensitivity", v); });
        y -= 56;
        UIFactory.CreateText(root.transform, "VolLabel", "Master volume", 28, TextAnchor.MiddleRight, UIFactory.Parchment, c, c, new Vector2(1, 0.5f), new Vector2(-30, y), new Vector2(400, 44));
        Slider(new Vector2(140, y), 0f, 1f, PlayerPrefs.GetFloat("master_volume", 0.8f), v => { PlayerPrefs.SetFloat("master_volume", v); AudioListener.volume = v; });

        statusText = UIFactory.CreateText(root.transform, "Status", "", 26, TextAnchor.MiddleCenter, UIFactory.Gold, c, c, c, new Vector2(0, -430), new Vector2(1200, 40));
        UIFactory.CreateButton(root.transform, "Reset", "Reset controls", c, new Vector2(-180, -490), new Vector2(320, 60), () => { GameInput.ResetBindings(); Refresh(); });
        UIFactory.CreateButton(root.transform, "Back", "Back", c, new Vector2(180, -490), new Vector2(320, 60), Close);
        root.SetActive(false);
    }

    void Slider(Vector2 pos, float min, float max, float value, Action<float> onChange)
    {
        var c = new Vector2(0.5f, 0.5f);
        var bg = UIFactory.CreateImage(root.transform, "Slider", new Color(0, 0, 0, 0.6f), c, c, c, pos, new Vector2(300, 20));
        var slider = bg.gameObject.AddComponent<Slider>();
        var fillArea = UIFactory.Rect("Fill Area", bg.transform, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        var fill = UIFactory.CreateImage(fillArea, "Fill", UIFactory.Gold, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        var handleArea = UIFactory.Rect("Handle Area", bg.transform, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        var handle = UIFactory.CreateImage(handleArea, "Handle", UIFactory.Parchment, new Vector2(0, 0), new Vector2(0, 1), c, Vector2.zero, new Vector2(20, 10));
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(v => { onChange(v); PlayerPrefs.Save(); });
    }

    static string KeyName(string action, int part)
    {
        var a = GameInput.Map.FindAction(action);
        return a.GetBindingDisplayString(part);
    }

    void Refresh()
    {
        foreach (var r in rows) r.label.text = r.value();
    }

    void StartRebind(string actionName, int bindingIndex, Text label)
    {
        if (rebind != null) return;
        var action = GameInput.Map.FindAction(actionName);
        GameInput.Map.Disable();
        label.text = "Press a key...";
        statusText.text = "";
        rebind = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op => FinishRebind(action, bindingIndex))
            .OnCancel(op => FinishRebind(action, -1))
            .Start();
    }

    void FinishRebind(InputAction action, int bindingIndex)
    {
        rebind?.Dispose();
        rebind = null;
        if (bindingIndex >= 0)
        {
            // Warn about a key already used elsewhere.
            string path = action.bindings[bindingIndex].effectivePath;
            foreach (var other in GameInput.Map.actions)
                for (int i = 0; i < other.bindings.Count; i++)
                    if ((other != action || i != bindingIndex) && other.bindings[i].effectivePath == path && !other.bindings[i].isComposite)
                        statusText.text = "Note: this key is also used for " + other.name;
            GameInput.SaveBindings();
        }
        GameInput.Map.Enable();
        Refresh();
    }

    public void Open(Action closed)
    {
        onClose = closed;
        Refresh();
        statusText.text = "";
        root.SetActive(true);
    }

    public void Close()
    {
        if (rebind != null) return;
        root.SetActive(false);
        ClosedFrame = Time.frameCount;
        GameInput.SaveBindings();
        var cb = onClose;
        onClose = null;
        cb?.Invoke();
    }

    void Update()
    {
        if (IsOpen && rebind == null && GameInput.Pause.WasPressedThisFrame()) Close();
    }
}
