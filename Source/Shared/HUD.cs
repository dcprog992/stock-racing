using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public CarController car;
    public EngineAudioController engineAudio; // необязательно — для диагностики
    public Text speedText;
    public Text gearText;
    public Text rpmText;

    [Header("Диагностика (можно отключить, когда всё починим)")]
    public bool showDebug = true;
    public Text debugText; // отдельный Text (Legacy) элемент под служебные цифры

    private CameraFollow cameraView;
    private CanvasGroup readouts;
    private bool hideHud;

    void Start()
    {
        cameraView = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        readouts = GetComponent<CanvasGroup>();
        if (readouts == null) readouts = gameObject.AddComponent<CanvasGroup>();
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.I)) hideHud = !hideHud;
        bool cockpit = cameraView != null && cameraView.CurrentView == CameraFollow.View.Cockpit;
        readouts.alpha = hideHud || cockpit ? 0f : 1f;
    }

    void Update()
    {
        if (car == null) return;
        speedText.text = Mathf.RoundToInt(SpeedUnits.Display(car.GetSpeedKmh())) + " "+SpeedUnits.Label;
        gearText.text = "Gear: " + car.GetGearLabel();
        rpmText.text = "RPM: " + Mathf.RoundToInt(car.GetRPM());

        if (showDebug && debugText != null)
        {
            string s = $"raw RPM: {car.GetRawRPM():F0}\nsmooth RPM: {car.GetRPM():F0}\nthrottle: {car.GetThrottle():F2}";

            if (engineAudio != null)
            {
                s += $"\nactive clip: {engineAudio.GetActiveIndex()}";
                s += $"\nactive vol: {engineAudio.GetActiveVolume():F2}";
                s += $"\nfading vol: {engineAudio.GetFadingVolume():F2}";
                s += $"\ncrossfading: {engineAudio.IsCrossfading()}";
            }

            debugText.text = s;
        }
    }
}