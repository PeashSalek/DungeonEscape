using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Small helpers for building uGUI elements from code.</summary>
public static class UIFactory
{
    static Font font;
    public static Font DefaultFont
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static readonly Color Gold = new Color(0.95f, 0.78f, 0.35f);
    public static readonly Color Parchment = new Color(0.92f, 0.88f, 0.78f);
    public static readonly Color PanelDark = new Color(0.05f, 0.04f, 0.05f, 0.85f);

    public static Canvas CreateCanvas(string name, int sortOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Object.DontDestroyOnLoad(es);
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return rt;
    }

    public static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = Rect(name, parent, anchorMin, anchorMax, pivot, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text CreateText(Transform parent, string name, string text, int size, TextAnchor align, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 rectSize)
    {
        var rt = Rect(name, parent, anchorMin, anchorMax, pivot, pos, rectSize);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = DefaultFont;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(2, -2);
        return t;
    }

    /// <summary>A horizontal fill bar (background + fill). Returns the fill image.</summary>
    public static Image Bar(Transform parent, string name, Color fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var bg = CreateImage(parent, name, new Color(0, 0, 0, 0.6f), anchor, anchor, pivot, pos, size);
        var outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.6f, 0.5f, 0.3f, 0.8f);
        var f = CreateImage(bg.transform, "Fill", fill, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        var frt = f.rectTransform;
        frt.offsetMin = new Vector2(3, 3);
        frt.offsetMax = new Vector2(-3, -3);
        f.type = UnityEngine.UI.Image.Type.Filled;
        f.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        f.sprite = WhiteSprite;
        return f;
    }

    static Sprite white;
    public static Sprite WhiteSprite
    {
        get
        {
            if (white == null)
                white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return white;
        }
    }

    public static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick, int fontSize = 30)
    {
        var img = CreateImage(parent, name, new Color(0.12f, 0.1f, 0.1f, 0.9f), anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);
        img.raycastTarget = true;
        var outline = img.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.55f, 0.3f, 0.9f);
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.6f, 1.3f, 0.9f);
        colors.selectedColor = new Color(1.4f, 1.2f, 0.9f);
        colors.pressedColor = new Color(0.7f, 0.6f, 0.5f);
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(onClick);
        var t = CreateText(img.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, Parchment,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return btn;
    }
}
