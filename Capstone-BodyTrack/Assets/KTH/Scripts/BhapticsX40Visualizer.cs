using UnityEngine;
using UnityEngine.UI;
using Bhaptics.SDK2;

public class BhapticsX40Visualizer : MonoBehaviour
{
    private const int RowCount = 5;
    private const int ColCount = 8;
    private const float TopCellOffset = 24f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (Object.FindObjectOfType<BhapticsX40Visualizer>() != null)
        {
            return;
        }

        GameObject go = new GameObject("BhapticsX40Visualizer");
        DontDestroyOnLoad(go);
        go.AddComponent<BhapticsX40Visualizer>();
    }

    [Header("UI Settings")]
    public bool autoCreateCanvas = true;
    public Vector2 anchorOffset = new Vector2(-20f, -20f);
    public Vector2 gridSize = new Vector2(280f, 160f);
    public float cellSize = 18f;
    public float cellGap = 4f;
    public float fadeSeconds = 0.6f;
    public bool showBackground = true;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.35f);
    public bool showCellOutline = true;

    [Header("Colors")]
    public Color offColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
    public Color onColor = new Color(0.2f, 0.8f, 1f, 1f);
    public Color outlineColor = new Color(1f, 1f, 1f, 0.25f);

    private Image[] motorImages;
    private float[] motorValues = new float[40];
    private float lastUpdateTime;
    private RectTransform gridRoot;
    private static Sprite solidSprite;

    private void OnEnable()
    {
        HapticsDebugBus.OnPlayMotors += HandlePlayMotors;
    }

    private void OnDisable()
    {
        HapticsDebugBus.OnPlayMotors -= HandlePlayMotors;
    }

    private void Start()
    {
        EnsureUI();
        UpdateGridColors(0f);
        Debug.Log("BhapticsX40Visualizer: UI ready");
    }

    private void Update()
    {
        float age = Time.time - lastUpdateTime;
        float fade = fadeSeconds > 0f ? Mathf.Clamp01(1f - (age / fadeSeconds)) : 1f;
        UpdateGridColors(fade);
    }

    private void HandlePlayMotors(PositionType position, int[] motors, int durationMs)
    {
        if (position != PositionType.Vest) return;
        if (motors == null || motors.Length < 40) return;

        for (int i = 0; i < 40; i++)
        {
            motorValues[i] = Mathf.Clamp01(motors[i] / 100f);
        }
        lastUpdateTime = Time.time;
    }

    private void EnsureUI()
    {
        if (gridRoot != null) return;
        NormalizeSettings();

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null && autoCreateCanvas)
        {
            GameObject canvasObj = new GameObject("BhapticsX40Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (canvas == null) return;

        GameObject gridObj = new GameObject("BhapticsX40Grid", typeof(RectTransform));
        gridObj.transform.SetParent(canvas.transform, false);
        gridRoot = gridObj.GetComponent<RectTransform>();
        gridRoot.anchorMin = new Vector2(1f, 1f);
        gridRoot.anchorMax = new Vector2(1f, 1f);
        gridRoot.pivot = new Vector2(1f, 1f);
        gridRoot.anchoredPosition = anchorOffset;
        gridRoot.sizeDelta = gridSize;

        if (showBackground)
        {
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(gridRoot, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bgObj.GetComponent<Image>();
            bgImage.color = backgroundColor;
            bgImage.raycastTarget = false;
        }

        CreateLabel("Front", new Vector2(0f, 0f), TextAnchor.UpperLeft);
        CreateLabel("Back", new Vector2(gridSize.x * 0.55f, 0f), TextAnchor.UpperLeft);

        motorImages = new Image[40];
        for (int i = 0; i < 40; i++)
        {
            int row;
            int col;
            if (i < 20)
            {
                row = i / 4;
                col = i % 4;
            }
            else
            {
                row = (i - 20) / 4;
                col = (i - 20) % 4 + 4;
            }

            Vector2 pos = new Vector2(
                (col * (cellSize + cellGap)),
                -TopCellOffset - (row * (cellSize + cellGap))
            );

            Image cell = CreateCell(pos);
            motorImages[i] = cell;
        }
    }

    private void NormalizeSettings()
    {
        if (cellSize <= 0f) cellSize = 18f;
        if (cellGap < 0f) cellGap = 4f;
        if (fadeSeconds < 0f) fadeSeconds = 0f;

        float minWidth = (ColCount * cellSize) + ((ColCount - 1) * cellGap);
        float minHeight = TopCellOffset + (RowCount * cellSize) + ((RowCount - 1) * cellGap);

        if (gridSize.x < minWidth) gridSize.x = minWidth;
        if (gridSize.y < minHeight) gridSize.y = minHeight;
    }

    private Image CreateCell(Vector2 anchoredPos)
    {
        GameObject cellObj = new GameObject("MotorCell", typeof(RectTransform), typeof(Image));
        cellObj.transform.SetParent(gridRoot, false);
        RectTransform rect = cellObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(cellSize, cellSize);

        Image image = cellObj.GetComponent<Image>();
        image.color = offColor;
        image.raycastTarget = false;
        image.sprite = GetSolidSprite();
        image.type = Image.Type.Simple;

        if (showCellOutline)
        {
            Outline outline = cellObj.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }
        return image;
    }

    private void UpdateGridColors(float fade)
    {
        if (motorImages == null) return;
        for (int i = 0; i < motorImages.Length; i++)
        {
            float intensity = motorValues[i] * fade;
            Color target = Color.Lerp(offColor, onColor, intensity);
            motorImages[i].color = target;
        }
    }

    private void CreateLabel(string text, Vector2 anchoredPos, TextAnchor anchor)
    {
        GameObject labelObj = new GameObject($"{text}Label", typeof(RectTransform), typeof(Text));
        labelObj.transform.SetParent(gridRoot, false);
        RectTransform rect = labelObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(120f, 20f);

        Text label = labelObj.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 14;
        label.color = Color.white;
        label.alignment = anchor;
        label.raycastTarget = false;
    }

    private static Sprite GetSolidSprite()
    {
        if (solidSprite != null) return solidSprite;

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }
        tex.Apply();
        solidSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        return solidSprite;
    }
}
