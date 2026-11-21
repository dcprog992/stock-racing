using UnityEngine;
using UnityEngine.UI;

public sealed class CockpitInstruments : MonoBehaviour
{
    private CameraFollow chase;
    private CarController car;
    private GameObject instruments;
    private Camera rearCamera;
    private RenderTexture mirrorTexture;
    private Material mirrorMaterial;
    private Mesh mirrorMesh;
    private Text speed, gear, rpm, speedUnit;
    private RectTransform rpmFill;
    private Image rpmColor;

    public void Initialize(CameraFollow cameraFollow, CarController controller)
    {
        chase = cameraFollow;
        car = controller;
        instruments = new GameObject("Instruments inside the Mustang");
        instruments.transform.SetParent(car.transform, false);
        CreateMirror();
        CreateDisplay();
        instruments.SetActive(false);
    }

    void LateUpdate()
    {
        if (instruments == null) return;
        bool visible = chase.CurrentView == CameraFollow.View.Cockpit;
        if (instruments.activeSelf != visible) instruments.SetActive(visible);
        if (!visible) return;

        speed.text = Mathf.RoundToInt(SpeedUnits.Display(car.GetSpeedKmh())).ToString();
        speedUnit.text=SpeedUnits.Label;
        gear.text = car.GetGearLabel();
        float revolutions = car.GetRPM();
        rpm.text = Mathf.RoundToInt(revolutions) + " RPM";
        float fraction = Mathf.Clamp01(revolutions / car.maxEngineRPM);
        rpmFill.anchorMax = new Vector2(fraction, 1f);
        rpmColor.color = fraction < .9f ? new Color32(117, 236, 255, 255) : new Color32(255, 94, 76, 255);
    }

    void CreateMirror()
    {
        mirrorTexture = new RenderTexture(384, 84, 16) { name = "Rear view", antiAliasing = 1 };
        mirrorMaterial = new Material(Shader.Find("Unlit/Texture")) { mainTexture = mirrorTexture };
        var panel = new GameObject("Rear-view mirror glass", typeof(MeshFilter), typeof(MeshRenderer));
        panel.transform.SetParent(instruments.transform, false);
        // Измеренные углы отражающей поверхности в координатах кузова.
        mirrorMesh = new Mesh { name = "Mustang mirror glass" };
        mirrorMesh.vertices = new[]
        {
            new Vector3(-.161f, 1.215f, .181f), new Vector3(.051f, 1.212f, .1245f),
            new Vector3(-.148f, 1.171f, .1815f), new Vector3(.048f, 1.166f, .1293f)
        };
        mirrorMesh.uv = new[] { new Vector2(1, 1), new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, 0) };
        mirrorMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        mirrorMesh.RecalculateBounds();
        panel.GetComponent<MeshFilter>().sharedMesh = mirrorMesh;
        var renderer = panel.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mirrorMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var source = new GameObject("Rear-view camera");
        source.transform.SetParent(instruments.transform, false);
        source.transform.localPosition = new Vector3(0f, 1.10f, -2.56f);
        source.transform.localRotation = Quaternion.Euler(3f, 180f, 0f);
        rearCamera = source.AddComponent<Camera>();
        rearCamera.targetTexture = mirrorTexture;
        rearCamera.fieldOfView = 25f;
        rearCamera.nearClipPlane = .08f;
        rearCamera.farClipPlane = 850f;
        rearCamera.depth = -10;
        rearCamera.allowHDR = false;
        rearCamera.allowMSAA = false;
        rearCamera.useOcclusionCulling = false;
    }

    void CreateDisplay()
    {
        var screen = new GameObject("Digital dash", typeof(RectTransform), typeof(Canvas));
        var rect = screen.GetComponent<RectTransform>();
        rect.SetParent(instruments.transform, false);
        rect.localPosition = new Vector3(-.330f, .842f, .693f);
        rect.localRotation = Quaternion.Euler(7.35f, 0f, 0f);
        rect.localScale = Vector3.one * .00028f;
        rect.sizeDelta = new Vector2(880f, 210f);
        screen.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var background = Block(rect, Vector2.zero, rect.sizeDelta, new Color32(6, 13, 19, 255));
        background.rectTransform.pivot = new Vector2(.5f, .5f);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var white = new Color32(224, 244, 255, 255);
        var muted = new Color32(126, 165, 182, 255);
        speed = Label(rect, font, new Vector2(-244, 26), new Vector2(295, 108), 91, white);
        gear = Label(rect, font, new Vector2(334, 25), new Vector2(156, 108), 94, new Color32(255, 203, 94, 255));
        rpm = Label(rect, font, new Vector2(50, 34), new Vector2(290, 72), 48, white);
        speedUnit=Label(rect, font, new Vector2(-244, -26), new Vector2(190, 36), 29, muted);speedUnit.text = SpeedUnits.Label;
        Label(rect, font, new Vector2(334, -28), new Vector2(150, 35), 27, muted).text = "GEAR";
        var rail = Block(rect, new Vector2(-370, 79), new Vector2(740, 12), new Color32(31, 52, 64, 255));
        rpmColor = Block(rail.rectTransform, Vector2.zero, rail.rectTransform.sizeDelta, white);
        rpmFill = rpmColor.rectTransform;
        rpmFill.anchorMin = Vector2.zero;
        rpmFill.anchorMax = new Vector2(0f, 1f);
        rpmFill.offsetMin = rpmFill.offsetMax = Vector2.zero;
    }

    static Image Block(Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var item = new GameObject("Display panel", typeof(RectTransform), typeof(Image));
        var image = item.GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.pivot = new Vector2(0f, .5f);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text Label(Transform parent, Font font, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var item = new GameObject("Display readout", typeof(RectTransform), typeof(Text));
        var text = item.GetComponent<Text>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    void OnDestroy()
    {
        if (instruments != null) Destroy(instruments);
        if (mirrorTexture != null) Destroy(mirrorTexture);
        if (mirrorMaterial != null) Destroy(mirrorMaterial);
        if (mirrorMesh != null) Destroy(mirrorMesh);
    }
}
