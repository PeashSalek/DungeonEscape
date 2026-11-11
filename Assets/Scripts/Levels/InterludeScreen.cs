using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Between-level screen: a dungeon map showing progress, one line of the Ghost
/// Merchant's story, and the merchant's shop (coins buy potions and upgrades).
/// </summary>
public class InterludeScreen : MonoBehaviour
{
    static readonly string[] storyLines =
    {
        "\"The King traded a thousand lives for a crown that could never be taken. I sold him the ritual. Now I sell trinkets to those who would undo it.\"",
        "\"Beyond this crypt waits his throne. He will not kneel, knight. Make him.\"",
    };

    class Offer
    {
        public string name, desc;
        public int price;
        public System.Func<bool> canBuy;
        public System.Action buy;
    }

    Text coinsText, storyText, infoText;
    readonly List<(Offer offer, Button button)> offers = new List<(Offer, Button)>();

    void Start()
    {
        Time.timeScale = 1f;
        ThirdPersonCamera.SetCursorLocked(false);
        if (!PlayerCarryOver.HasData) { PlayerCarryOver.Coins = 40; PlayerCarryOver.Potions = 2; PlayerCarryOver.MaxPotions = 5; PlayerCarryOver.Lockpicks = 1; }
        Build();
        Refresh();
    }

    int StageIndex => GameFlow.NextLevel == GameFlow.Crypt ? 1 : GameFlow.NextLevel == GameFlow.KingsChamber ? 2 : 0;

    void Build()
    {
        var canvas = UIFactory.CreateCanvas("Interlude Canvas", 10);
        var root = canvas.transform;
        var c = new Vector2(0.5f, 0.5f);

        // --- Map (left) ---
        var mapPanel = UIFactory.CreateImage(root, "Map", new Color(0.12f, 0.1f, 0.08f, 0.92f), c, c, c, new Vector2(-470, 60), new Vector2(760, 760));
        mapPanel.gameObject.AddComponent<Outline>().effectColor = new Color(0.6f, 0.45f, 0.25f);
        UIFactory.CreateText(mapPanel.transform, "Title", "THE DUNGEON OF MALGRATH", 34, TextAnchor.MiddleCenter, UIFactory.Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(700, 50));
        string[] names = { "The Prison", "The Crypt", "The King's Chamber" };
        Vector2[] pos = { new Vector2(-170, -230), new Vector2(150, -20), new Vector2(-60, 220) };
        for (int i = 0; i < 3; i++)
        {
            bool done = i < StageIndex;
            bool next = i == StageIndex;
            if (i < 2)
            {
                // Path line to the next area.
                Vector2 a = pos[i], b = pos[i + 1];
                var line = UIFactory.CreateImage(mapPanel.transform, "Path" + i, i + 1 < StageIndex + 1 && done ? UIFactory.Gold : new Color(0.5f, 0.4f, 0.3f, 0.6f), c, c, c, (a + b) * 0.5f, new Vector2(Vector2.Distance(a, b), 6));
                line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            }
            var node = UIFactory.CreateImage(mapPanel.transform, "Node" + i, done ? new Color(0.35f, 0.3f, 0.25f) : next ? new Color(0.75f, 0.15f, 0.1f) : new Color(0.2f, 0.18f, 0.16f), c, c, c, pos[i], new Vector2(60, 60));
            node.gameObject.AddComponent<Outline>().effectColor = UIFactory.Gold;
            UIFactory.CreateText(mapPanel.transform, "Label" + i, names[i] + (done ? "  (cleared)" : next ? "  (next)" : ""), 26, TextAnchor.MiddleCenter, done ? UIFactory.Parchment : next ? UIFactory.Gold : new Color(0.6f, 0.55f, 0.5f), c, c, c, pos[i] + new Vector2(0, -55), new Vector2(420, 40));
        }

        // --- Story (bottom of map) ---
        storyText = UIFactory.CreateText(root, "Story", storyLines[Mathf.Clamp(StageIndex - 1, 0, storyLines.Length - 1)], 28, TextAnchor.MiddleCenter, UIFactory.Parchment, c, c, c, new Vector2(-470, -400), new Vector2(820, 120));

        // --- Shop (right) ---
        var shop = UIFactory.CreateImage(root, "Shop", new Color(0.05f, 0.06f, 0.08f, 0.9f), c, c, c, new Vector2(430, 30), new Vector2(820, 900));
        shop.gameObject.AddComponent<Outline>().effectColor = new Color(0.4f, 0.6f, 0.7f);
        UIFactory.CreateText(shop.transform, "Title", "THE GHOST MERCHANT", 40, TextAnchor.MiddleCenter, new Color(0.65f, 0.9f, 1f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(760, 56));
        coinsText = UIFactory.CreateText(shop.transform, "Coins", "", 32, TextAnchor.MiddleCenter, UIFactory.Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(760, 44));

        var list = new List<Offer>
        {
            new Offer { name = "Healing Potion", desc = "Restores half your health.", price = 30,
                canBuy = () => PlayerCarryOver.Potions < PlayerCarryOver.MaxPotions, buy = () => PlayerCarryOver.Potions++ },
            new Offer { name = "Bigger Potion Bag", desc = "+1 potion slot.", price = 60,
                canBuy = () => PlayerCarryOver.MaxPotions < 8, buy = () => PlayerCarryOver.MaxPotions++ },
            new Offer { name = "Sharpen Sword", desc = "+15% damage (stacks).", price = 80 + 40 * RunUpgrades.swordLevel,
                canBuy = () => RunUpgrades.swordLevel < 3, buy = () => RunUpgrades.swordLevel++ },
            new Offer { name = "Reinforce Armor", desc = "Take 10% less damage (stacks).", price = 80 + 40 * RunUpgrades.armorLevel,
                canBuy = () => RunUpgrades.armorLevel < 3, buy = () => RunUpgrades.armorLevel++ },
            new Offer { name = "Heart of the Brave", desc = "+20 maximum health (stacks).", price = 70 + 30 * RunUpgrades.vitalityLevel,
                canBuy = () => RunUpgrades.vitalityLevel < 3, buy = () => RunUpgrades.vitalityLevel++ },
            new Offer { name = "Lockpicks x2", desc = "For the chests the King locked away.", price = 25,
                canBuy = () => true, buy = () => PlayerCarryOver.Lockpicks += 2 },
        };

        float y = -200;
        foreach (var o in list)
        {
            var offer = o;
            var btn = UIFactory.CreateButton(shop.transform, o.name, "", new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(740, 96), () => Buy(offer), 28);
            var label = btn.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(24, 0);
            offers.Add((offer, btn));
            y -= 104;
        }
        infoText = UIFactory.CreateText(shop.transform, "Info", "", 26, TextAnchor.MiddleCenter, UIFactory.Parchment, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(760, 40));

        var go = UIFactory.CreateButton(root, "Continue", "Descend  >", new Vector2(1, 0), new Vector2(-200, 70), new Vector2(320, 70), Continue, 32);
        go.Select();
    }

    void Refresh()
    {
        coinsText.text = PlayerCarryOver.Coins + " coins     Potions " + PlayerCarryOver.Potions + "/" + PlayerCarryOver.MaxPotions + "     Lockpicks " + PlayerCarryOver.Lockpicks;
        foreach (var (o, b) in offers)
        {
            bool can = o.canBuy() && PlayerCarryOver.Coins >= o.price;
            b.interactable = o.canBuy();
            b.GetComponentInChildren<Text>().text = o.name + "   -   " + o.price + " coins\n<size=22>" + o.desc + (o.canBuy() ? "" : "  (maxed)") + "</size>";
            b.GetComponentInChildren<Text>().color = can ? UIFactory.Parchment : new Color(0.55f, 0.5f, 0.45f);
        }
    }

    void Buy(Offer o)
    {
        if (!o.canBuy()) return;
        if (PlayerCarryOver.Coins < o.price) { infoText.text = "\"Not enough coin, knight.\""; AudioManager.PlayUI("ui_select"); return; }
        PlayerCarryOver.Coins -= o.price;
        o.buy();
        infoText.text = "\"A fine choice.\"  Bought: " + o.name;
        AudioManager.PlayUI("coin", 0.8f);
        // Prices of stacking upgrades go up.
        if (o.name == "Sharpen Sword") o.price = 80 + 40 * RunUpgrades.swordLevel;
        if (o.name == "Reinforce Armor") o.price = 80 + 40 * RunUpgrades.armorLevel;
        if (o.name == "Heart of the Brave") o.price = 70 + 30 * RunUpgrades.vitalityLevel;
        Refresh();
    }

    void Continue() => GameFlow.ContinueFromInterlude();
}
