using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Curse shrine: offers three random blessings (each with a curse). The player picks one.
/// Used once; it then goes dark.
/// </summary>
public class CurseShrine : MonoBehaviour, IInteractable
{
    public Light shrineLight;
    bool used;
    GameObject ui;

    public string Prompt(PlayerInteractor p) => used ? "" : "Pray at the curse shrine";
    public bool CanInteract(PlayerInteractor p) => !used && BlessingSystem.Instance != null;

    public void Interact(PlayerInteractor p)
    {
        if (!CanInteract(p)) return;
        AudioManager.PlayUI("shrine", 0.8f);
        var choices = BlessingSystem.Instance.RollChoices(3);
        if (choices.Count == 0) { used = true; return; }
        Show(choices, p.Stats);
    }

    void Show(List<BlessingSystem.Blessing> choices, PlayerStats stats)
    {
        Time.timeScale = 0f;
        ThirdPersonCamera.SetCursorLocked(false);
        var canvas = UIFactory.CreateCanvas("Shrine Canvas", 40);
        ui = canvas.gameObject;
        var root = canvas.transform;
        var c = new Vector2(0.5f, 0.5f);
        UIFactory.CreateImage(root, "Dim", new Color(0.03f, 0f, 0.05f, 0.85f), Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        UIFactory.CreateText(root, "Title", "THE CURSE OFFERS A GIFT", 54, TextAnchor.MiddleCenter, UIFactory.Gold, c, c, c, new Vector2(0, 360), new Vector2(1600, 80));
        UIFactory.CreateText(root, "Sub", "Every blessing has a price. Choose one.", 28, TextAnchor.MiddleCenter, UIFactory.Parchment, c, c, c, new Vector2(0, 300), new Vector2(1600, 50));

        for (int i = 0; i < choices.Count; i++)
        {
            var b = choices[i];
            float x = (i - (choices.Count - 1) * 0.5f) * 520f;
            var card = UIFactory.CreateButton(root, "Card" + i, "", c, new Vector2(x, -20), new Vector2(460, 480), () => Pick(b, stats), 30);
            var t = card.transform;
            UIFactory.CreateText(t, "Name", b.title, 40, TextAnchor.UpperCenter, UIFactory.Gold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(-40, 60));
            UIFactory.CreateText(t, "Boon", b.boon, 28, TextAnchor.UpperCenter, new Color(0.7f, 0.95f, 0.7f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(-50, 140));
            UIFactory.CreateText(t, "Curse", "Curse: " + b.curse, 26, TextAnchor.UpperCenter, new Color(0.95f, 0.55f, 0.55f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(-50, 140));
            if (i == 0) card.Select();
        }
    }

    void Pick(BlessingSystem.Blessing b, PlayerStats stats)
    {
        BlessingSystem.Instance.Choose(b, stats);
        used = true;
        if (shrineLight != null) shrineLight.enabled = false;
        Destroy(ui);
        Time.timeScale = 1f;
        ThirdPersonCamera.SetCursorLocked(true);
    }
}
