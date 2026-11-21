using UnityEngine;

[System.Serializable]
public class EngineRpmSample
{
    public AudioClip clip;
    [Tooltip("На каких реальных оборотах был записан этот клип")]
    public float rpm;
    [Tooltip("Компенсация громкости, если конкретная запись тише/громче остальных (1 = без изменений)")]
    public float gainMultiplier = 1f;
}

public class EngineAudioController : MonoBehaviour
{
    public CarController car;
    public AudioSource sourceA;
    public AudioSource sourceB;
    public EngineRpmSample[] samples;

    [Header("Двигатель")]
    public float minVolume = 0.25f;
    public float maxVolume = 0.70f;
    [Range(0.3f, 1f)] public float offThrottleVolume = 0.65f;
    [Range(0f, 1f)] public float pitchBendAmount = 0.85f;
    public float volumeSmoothing = 18f;

    [Header("Прямозубая коробка")]
    [Range(0f, 1f)] public float gearboxVolume = 0.46f;

    public AudioClip limiterClip;
    private AudioSource limiterVoice;
    private float limiterHold;
    private AudioSource[] voices;
    private AudioSource gearboxVoice;
    public AudioClip gearboxClip;
    public AudioClip[] shiftClips;
    [Range(0f, 1f)] public float shiftVolume = .65f;
    private AudioSource shiftVoice;
    private int nextShiftClip;
    private CameraFollow cameraView;
    private float enginePerspective = 1f;
    private float gearboxPerspective = 1f;
    private int lower;
    private int upper;
    private float blend;

    void Start()
    {
        if (car == null) car = GetComponent<CarController>();
        cameraView = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;

        int count = samples?.Length ?? 0;
        voices = new AudioSource[count];
        for (int i = 0; i < count; i++)
        {
            AudioSource source = i == 0 ? sourceA : i == 1 && sourceB != sourceA ? sourceB : null;
            voices[i] = StartLoop(source, samples[i].clip);
        }

        gearboxVoice = StartLoop(null, gearboxClip);
        if (shiftClips != null && shiftClips.Length > 0)
        {
            shiftVoice = gameObject.AddComponent<AudioSource>();
            shiftVoice.playOnAwake = false;
            shiftVoice.dopplerLevel = 0f;
            ConfigureSpatial(shiftVoice);
            if (car != null) car.GearEngaged += PlayShift;
        }
        if (limiterClip != null) limiterVoice = StartLoop(null, limiterClip);
    }

    AudioSource StartLoop(AudioSource source, AudioClip clip)
    {
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        ConfigureSpatial(source);
        if (clip != null) source.Play();
        return source;
    }

    void ConfigureSpatial(AudioSource voice)
    {
        if (car == null || !car.IsExternallyDriven) return;
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Logarithmic;
        voice.minDistance = 4f;
        voice.maxDistance = 130f;
        voice.dopplerLevel = .45f;
    }

    void Update()
    {
        if (car == null) return;

        float smoothing = 1f - Mathf.Exp(-volumeSmoothing * Time.deltaTime);
        var view = cameraView == null ? CameraFollow.View.Dynamic : cameraView.CurrentView;
        float engineTarget = view == CameraFollow.View.Cockpit ? .60f : view == CameraFollow.View.Hood ? 1.12f : .88f;
        float gearboxTarget = view == CameraFollow.View.Cockpit ? 1.22f : view == CameraFollow.View.Hood ? .69f : .92f;
        if (car.IsExternallyDriven)
        {
            engineTarget = view == CameraFollow.View.Cockpit ? .42f : .72f;
            gearboxTarget = view == CameraFollow.View.Cockpit ? .38f : .60f;
        }
        enginePerspective = Mathf.Lerp(enginePerspective, engineTarget, smoothing);
        gearboxPerspective = Mathf.Lerp(gearboxPerspective, gearboxTarget, smoothing);
        limiterHold = car.IsRevLimiterActive() ? .14f : Mathf.Max(0f, limiterHold - Time.deltaTime);
        UpdateEngine(smoothing);
        if (limiterVoice != null)
        {
            float target = limiterHold > 0f ? (car.IsLaunchStaged() ? .36f : .62f) : 0f;
            limiterVoice.volume = Mathf.Lerp(limiterVoice.volume, target * enginePerspective, smoothing);
            limiterVoice.pitch = Mathf.Clamp(car.GetRPM() / 7000f, .5f, 1.3f);
        }
        UpdateGearbox(smoothing);
    }

    void UpdateEngine(float smoothing)
    {
        if (voices.Length == 0) return;

        float rpm = car.GetRPM();
        lower = 0;
        while (lower < voices.Length - 2 && rpm >= samples[lower + 1].rpm) lower++;
        upper = Mathf.Min(lower + 1, voices.Length - 1);
        blend = Mathf.InverseLerp(samples[lower].rpm, samples[upper].rpm, rpm);
        float load = Mathf.Lerp(offThrottleVolume, 1f, car.GetThrottle());
        float gain = load * Mathf.Lerp(minVolume, maxVolume, Mathf.InverseLerp(car.idleRPM, car.maxEngineRPM, rpm));

        if (limiterVoice != null && limiterHold > 0f) gain *= .68f;

        // Все петли продолжают играть: переходы не сбрасывают позицию клипов.
        for (int i = 0; i < voices.Length; i++)
        {
            float weight = i == lower ? Mathf.Cos(blend * Mathf.PI * 0.5f) :
                i == upper ? Mathf.Sin(blend * Mathf.PI * 0.5f) : 0f;
            AudioSource source = voices[i];
            source.volume = Mathf.Lerp(source.volume, weight * gain * samples[i].gainMultiplier * enginePerspective, smoothing);
            float pitch = Mathf.Lerp(1f, rpm / Mathf.Max(1f, samples[i].rpm), pitchBendAmount);
            source.pitch = Mathf.Lerp(source.pitch, Mathf.Clamp(pitch, 0.55f, 1.65f), smoothing);
        }
    }

    void UpdateGearbox(float smoothing)
    {
        float shaftRPM = car.GetGearboxInputRPM();
        float audible = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(100f, 1400f, shaftRPM));
        float rise = Mathf.Pow(Mathf.Clamp01(shaftRPM / car.maxEngineRPM), .62f);
        float volume = gearboxVolume * audible * Mathf.Lerp(.26f, 1f, rise);
        if (car.IsShifting()) volume *= .58f;
        gearboxVoice.volume = Mathf.Lerp(gearboxVoice.volume, volume * gearboxPerspective, smoothing);

        float pitch = Mathf.Lerp(.58f, 1.40f, Mathf.Clamp01(shaftRPM / car.maxEngineRPM));
        gearboxVoice.pitch = Mathf.Lerp(gearboxVoice.pitch, pitch, smoothing);
    }

    void PlayShift()
    {
        AudioClip clip = shiftClips[nextShiftClip];
        nextShiftClip = (nextShiftClip + 1) % shiftClips.Length;
        if (isActiveAndEnabled && clip != null) shiftVoice.PlayOneShot(clip, Mathf.Clamp01(shiftVolume * gearboxPerspective));
    }

    void OnDestroy()
    {
        if (car != null) car.GearEngaged -= PlayShift;
    }

    public int GetActiveIndex() => lower;
    public float GetActiveVolume() => voices?.Length > 0 ? voices[lower].volume : 0f;
    public float GetFadingVolume() => voices?.Length > 1 ? voices[upper].volume : 0f;
    public bool IsCrossfading() => blend > 0.02f && blend < 0.98f;
}