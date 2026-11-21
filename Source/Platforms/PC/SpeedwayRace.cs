using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SpeedwayRace : MonoBehaviour
{
    [Serializable]
    public struct Gate
    {
        public Vector3 position;
        public Vector3 forward;
        public float halfWidth;

        public bool Crossed(Vector3 from, Vector3 to)
        {
            float a = Vector3.Dot(from - position, forward);
            float b = Vector3.Dot(to - position, forward);
            if (a >= 0f || b < 0f) return false;

            Vector3 crossing = Vector3.Lerp(from, to, -a / (b - a)) - position;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Mathf.Abs(Vector3.Dot(crossing, right)) <= halfWidth && Mathf.Abs(crossing.y) <= 5f;
        }
    }

    public CarController car;
    public Gate[] gates;
    public Transform opponentStart;

    private enum RaceMode { Standard, Training }
    private static (RaceMode Mode, int Laps)? restartSettings;
    private int lapLimit = 1;
    private double raceStart;
    private double raceResult;
    private bool crossedStart;
    private Text distanceLabel;
    private Button distanceButton;
    private Button primaryButton;
    private RaceMode mode;
    private RaceDifficulty.Level selectedDifficulty;
    private TrainingGhost ghost;
    private SpeedwayOpponent opponent;
    private bool startCancelled;
    private string StandardBestKey => $"{gameObject.scene.name}.StandingStart.{lapLimit}.Best";

    private enum State { Ready, Staging, Countdown, Driving, Paused, Finished }
    private State state;
    private State beforePause;
    private Vector3 previousPosition;
    private int nextGate;
    private int laps;
    private float countdown;
    private float greenUntil;
    private double lapStart;
    private double bestLap;
    private double lastLap;

    private Text speed, gear, rpm, timing, status, heading, subheading, primaryLabel;
    private Text assist, gearboxMode, lastTime, bestTime;
    private RectTransform throttleFill, brakeFill;
    private readonly Image[] tachSegments = new Image[34];
    private readonly Image[] shiftLights = new Image[5];
    private static readonly Color PanelColor = new Color32(16, 19, 24, 237);
    private static readonly Color Recess = new Color32(31, 36, 43, 255);
    private static readonly Color Red = new Color32(255, 86, 63, 255);
    private static readonly Color Amber = new Color32(255, 181, 69, 255);
    private Rigidbody body;
    private GameObject menu;
    private Text forgivenessLabel;
    private Text cameraLabel;
    private CameraFollow cameraView;
    private CanvasGroup drivingHud;
    private bool hideHud;
    private const string ForgivenessKey = "Speedway.ForgiveMistakes";
    private Button trainingButton;
    private Button resetButton;
    private Button modeMenuButton;
    private Font font;
    private Text speedUnitLabel;
    private StartSignal startSignal;
    private readonly Color ink = new Color32(13, 19, 29, 241);
    private readonly Color muted = new Color32(162, 177, 199, 255);
    private readonly Color accent = new Color32(124, 223, 244, 255);

    void Start()
    {
        body = car.GetComponent<Rigidbody>();
        previousPosition = body.position;

        cameraView = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        car.ForgiveMistakes = PlayerPrefs.GetInt(ForgivenessKey, 1) != 0;
        car.automaticTransmission=PlayerPrefs.GetInt("Speedway.Mobile.Automatic",1)!=0;
        gameObject.AddComponent<SpeedwayFinishOverlay>();
        BuildHud();

        SetState(State.Ready);
        var requested = restartSettings;
        restartSettings = null;
        if (requested.HasValue)
        {
            lapLimit = requested.Value.Laps;
            StartRun(requested.Value.Mode);
        }
        else if(GameFlow.Pending)
        {
            lapLimit=GameFlow.Laps;GameFlow.Pending=false;
            StartRun(GameFlow.Training?RaceMode.Training:RaceMode.Standard);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I)) hideHud = !hideHud;
        if (Input.GetKeyDown(KeyCode.Escape) && !GameMenu.EscapeHandled && state != State.Ready)
        {
            if (state == State.Paused) SetState(beforePause);
            else { beforePause = state; SetState(State.Paused); }
        }

        if (Input.GetKeyDown(KeyCode.Return)) PrimaryAction();
        if (Input.GetKeyDown(KeyCode.Backspace) && state != State.Ready) Restart();

        if (state == State.Countdown)
        {
            if (!IsStaged())
            {
                startCancelled = true;
                SetState(State.Staging);
            }
            else countdown -= Time.deltaTime;
            if (state == State.Countdown && countdown <= 0f)
            {
                if (IsInStartArea() && car.DetachedWheelCount == 0) StartFromRest();
                else SetState(State.Staging);
            }
        }

        startSignal.Show(state==State.Staging,state==State.Countdown,countdown,state==State.Driving && Time.time<greenUntil,state==State.Paused);

        speed.text = Mathf.RoundToInt(SpeedUnits.Display(car.GetSpeedKmh())).ToString();
        speedUnitLabel.text=SpeedUnits.Label;
        gear.text = car.GetGearLabel();
        if (cameraView != null)
        {
            cameraLabel.text = cameraView.CurrentView switch
            {
                CameraFollow.View.Fixed => "КАМЕРА · ФИКС.",
                CameraFollow.View.Cockpit => "КАМЕРА · САЛОН",
                CameraFollow.View.Hood => "КАМЕРА · КАПОТ",
                _ => "КАМЕРА · ДИНАМИКА"
            };
        }
        float rpmValue = car.GetRPM();
        rpm.text = Mathf.RoundToInt(rpmValue).ToString("0000") + "  RPM";
        gearboxMode.text = car.IsAutomatic() ? "AUTO" : "MANUAL";
        throttleFill.localScale = new Vector3(Mathf.Clamp01(car.GetThrottle()), 1f, 1f);
        brakeFill.localScale = new Vector3(Mathf.Clamp01(car.GetBrakePressure()), 1f, 1f);
        float fraction = Mathf.Clamp01(rpmValue / car.maxEngineRPM);
        for (int i = 0; i < tachSegments.Length; i++)
        {
            float step = (i + 1f) / tachSegments.Length;
            var color = step >= .91f ? Red : step >= .78f ? Amber : accent;
            tachSegments[i].color = fraction >= step ? color : Recess;
        }
        for (int i = 0; i < shiftLights.Length; i++)
            shiftLights[i].color = fraction > .77f + i * .045f ? (i > 2 ? Red : Amber) : Recess;

        assist.text = car.DetachedWheelCount > 0 ? $"КОЛЁСА {4 - car.DetachedWheelCount}/4" : car.IsRevLimiterActive() ? "LIMITER" : car.IsLaunchStaged() ? "LAUNCH" : car.IsAbsActive() ? "ABS" : car.IsShifting() ? "SHIFT" : car.IsClutchPedalPressed() ? "CLUTCH" : "";
        assist.color = car.IsAbsActive() ? Amber : accent;

        double elapsed = HasFinished ? lastLap :
            state == State.Driving || (state == State.Paused && beforePause == State.Driving)
                ? Time.timeAsDouble - lapStart : 0;
        timing.text = FormatTime(elapsed);
        lastTime.text = FormatTime(lastLap);
        bestTime.text = FormatTime(bestLap);
        status.text = state switch
        {
            State.Staging when opponent != null && !opponent.ReadyToStart => "СОПЕРНИКИ ПОДЪЕЗЖАЮТ К ЛИНИИ",
            State.Staging => startCancelled ? "ВСТАНЬТЕ ПЕРЕД ЛИНИЕЙ · ENTER" :
                IsStaged() ? "ENTER · НАЧАТЬ ОТСЧЁТ" : "ОСТАНОВИСЬ ПЕРЕД ЛИНИЕЙ СТАРТА",
            State.Countdown => Mathf.CeilToInt(countdown) + "\nW + ПРОБЕЛ · ЛАУНЧ",
            State.Finished => "ФИНИШ",
            State.Driving when Time.time < greenUntil => "СТАРТ",
            State.Driving => $"КРУГ {laps + 1} / {lapLimit}   ·   СЕКТОР {Mathf.Max(1, nextGate)} / {gates.Length}",
            _ => ""
        };
    }

    void LateUpdate()
    {
        bool cockpit = cameraView != null && cameraView.CurrentView == CameraFollow.View.Cockpit;
        bool hidden = hideHud || cockpit || !GameplayVisible;
        drivingHud.alpha = hidden ? 0f : 1f;
        status.enabled = GameplayVisible && (!hidden || state == State.Staging || state == State.Countdown || Time.time < greenUntil);
        if (state == State.Driving)
            ghost?.UpdateVisual((float)(Time.timeAsDouble - raceStart));
    }

    bool IsStaged() => (opponent == null || opponent.ReadyToStart) && IsInStartArea() && car.DetachedWheelCount == 0 &&
        body.linearVelocity.sqrMagnitude <= .12f * .12f &&
        body.angularVelocity.sqrMagnitude <= .15f * .15f;

    bool IsInStartArea()
    {
        if (gates == null || gates.Length < 2) return false;

        Gate start = gates[0];
        Vector3 offset = body.position - start.position;
        float distance = Vector3.Dot(offset, start.forward);
        Vector3 right = Vector3.Cross(Vector3.up, start.forward);
        return distance >= -9f && distance <= -2.6f && Mathf.Abs(offset.y) < 4f &&
            Mathf.Abs(Vector3.Dot(offset, right)) <= start.halfWidth &&
            Vector3.Dot(car.transform.forward, start.forward) >= .75f;
    }

    void StartFromRest()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        previousPosition = body.position;
        raceStart = lapStart = Time.timeAsDouble;
        greenUntil = Time.time + 1f;
        ghost?.BeginRace(body.position, body.rotation);
        opponent?.BeginRace(lapLimit);
        SetState(State.Driving);
    }

    void FixedUpdate()
    {
        Vector3 position = body.position;
        if (state == State.Driving)
        {
            double now = Time.fixedTimeAsDouble;
            if (gates[nextGate].Crossed(previousPosition, position))
            {
                if (nextGate == 0)
                {
                    if (crossedStart)
                    {
                        lastLap = now - lapStart;
                        lapStart = now;
                        laps++;
                        if (laps == lapLimit)
                        {
                            FinishRun(now);
                            return;
                        }
                    }
                    crossedStart = true;
                }
                nextGate = (nextGate + 1) % gates.Length;
            }
            ghost?.Record((float)(now - raceStart), position, body.rotation);
        }
        previousPosition = position;
    }

    void FinishRun(double now)
    {
        raceResult = now - raceStart;
        if (ghost != null)
        {
            if (ghost.FinishRace((float)raceResult, body.position, body.rotation))
                bestLap = ghost.BestTime;
            ghost.HideVisual();
        }
        else if (bestLap <= 0 || raceResult < bestLap)
        {
            bestLap = raceResult;
            PlayerPrefs.SetString(StandardBestKey, bestLap.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        SetState(State.Finished);
    }


    public bool GameplayVisible => state==State.Staging || state==State.Countdown || state==State.Driving || state==State.Finished;
    public bool IsReady => state==State.Ready;
    public bool HasFinished => state==State.Finished || (state==State.Paused && beforePause==State.Finished);
    public double PlayerFinishTime=>raceResult;
    public int CompletedPlayerLaps=>laps;
    public bool IsFinished => state==State.Finished;
    public bool IsPaused => state==State.Paused;
    public string SessionDescription => (mode==RaceMode.Training?"ТРЕНИРОВКА С ПРИЗРАКОМ":"ОБЫЧНЫЙ ЗАЕЗД")+"   /   "+(lapLimit==1?"1 КРУГ":"3 КРУГА")+(mode==RaceMode.Standard?" / "+RaceDifficulty.Name(selectedDifficulty):"");
    public string ResultTime => FormatTime(raceResult);
    public string BestTime => FormatTime(bestLap);
    public void ResumeRace(){if(state==State.Paused)SetState(beforePause);}
    public void RestartRace()=>Restart();
    public void PauseRace(){if(state==State.Paused)ResumeRace();else if(GameplayVisible){beforePause=state;SetState(State.Paused);}}

    void PrimaryAction()
    {
        if (state == State.Staging && IsStaged()) BeginCountdown();
        else if (state == State.Paused) SetState(beforePause);
    }

    void StartRun(RaceMode selected)
    {
        if (state != State.Ready) return;

        mode = selected;selectedDifficulty=RaceDifficulty.Current;
        if (mode == RaceMode.Training)
        {
            string replayPath = System.IO.Path.Combine(Application.persistentDataPath,
                $"{gameObject.scene.name}.{lapLimit}L.standing-start.ghost");
            ghost = new TrainingGhost(car, replayPath);
            bestLap = ghost.BestTime;
        }
        else double.TryParse(PlayerPrefs.GetString(StandardBestKey),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bestLap);
        if (mode == RaceMode.Standard)
        {
            opponent = gameObject.AddComponent<SpeedwayOpponent>();
            opponent.Initialize(car, gates[0], opponentStart);
        }
        SetState(State.Staging);
    }

    void BeginCountdown()
    {
        startCancelled = false;
        countdown = 3f;
        SetState(State.Countdown);
    }

    void Restart()
    {
        restartSettings = (mode, lapLimit);
        LoadRaceScene();
    }

    void LoadRaceScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameObject.scene.name);
    }

    void OnDestroy()
    {
        ghost?.Dispose();
        if (state == State.Paused) Time.timeScale = 1f;
    }

    void SetState(State next)
    {
        state = next;
        Time.timeScale = state == State.Paused ? 0f : 1f;
        car.SetRaceControls(GameplayVisible);
        car.SetLaunchStaged(false);

        AudioListener.pause=state==State.Paused;
        var front=FindAnyObjectByType<GameMenu>();
        if(front){if(GameplayVisible)front.Hide();else front.ShowRace();}
    }

    static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "--:--.---";
        var time = TimeSpan.FromSeconds(seconds);
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    void BuildHud()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("Race HUD", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 10;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;

        var readouts = new GameObject("Driving readouts", typeof(RectTransform), typeof(CanvasGroup));
        var readoutRect = (RectTransform)readouts.transform;
        readoutRect.SetParent(root.transform, false);
        readoutRect.anchorMin = Vector2.zero;
        readoutRect.anchorMax = Vector2.one;
        readoutRect.offsetMin = readoutRect.offsetMax = Vector2.zero;
        drivingHud = readouts.GetComponent<CanvasGroup>();
        drivingHud.blocksRaycasts = false;
        drivingHud.interactable = false;

        var topLeft = new Vector2(0, 1);
        var bottomRight = new Vector2(1, 0);
        var dash = Panel(readoutRect, "Instrument cluster", bottomRight, new Vector2(-30, 29), new Vector2(470, 220), PanelColor);
        Panel(dash, "Top trim", topLeft, Vector2.zero, new Vector2(470, 3), Red);
        Panel(dash, "Gear background", topLeft, new Vector2(344, -93), new Vector2(104, 107), Recess);
        Label(dash, "ПЕРЕДАЧА", 13, muted, topLeft, new Vector2(355, -102), new Vector2(89, 19));
        gear = Label(dash, "1", 56, Color.white, topLeft, new Vector2(351, -112), new Vector2(90, 74));
        gear.horizontalOverflow = HorizontalWrapMode.Overflow;
        gear.verticalOverflow = VerticalWrapMode.Overflow;
        gear.alignment = TextAnchor.MiddleCenter;
        gearboxMode = Label(dash, "", 13, accent, topLeft, new Vector2(352, -181), new Vector2(90, 17));
        gearboxMode.alignment = TextAnchor.MiddleCenter;
        speed = Label(dash, "0", 64, Color.white, topLeft, new Vector2(22, -89), new Vector2(192, 79));
        speed.horizontalOverflow = HorizontalWrapMode.Overflow;
        speed.verticalOverflow = VerticalWrapMode.Overflow;
        speedUnitLabel=Label(dash, "КМ/Ч", 14, muted, topLeft, new Vector2(215, -135), new Vector2(70, 21));
        rpm = Label(dash, "", 15, muted, topLeft, new Vector2(22, -13), new Vector2(120, 19));
        assist = Label(dash, "", 14, accent, topLeft, new Vector2(350, -13), new Vector2(98, 19));
        assist.alignment = TextAnchor.UpperRight;
        for (int i = 0; i < shiftLights.Length; i++)
            shiftLights[i] = Panel(dash, "Shift light", topLeft, new Vector2(175 + 25 * i, -16), new Vector2(18, 8), Recess).GetComponent<Image>();
        for (int i = 0; i < tachSegments.Length; i++)
        {
            float height = Mathf.Lerp(13, 27, i / 33f);
            var segment = Panel(dash, "RPM segment", topLeft, new Vector2(22 + 12.55f * i, -62 + height), new Vector2(9, height), Recess);
            tachSegments[i] = segment.GetComponent<Image>();
        }
        for (int i = 0; i <= 8; i++)
        {
            float x = 22 + 424 * i * 1000f / car.maxEngineRPM;
            Panel(dash, "RPM tick", topLeft, new Vector2(x, -65), new Vector2(1, 6), muted);
            Label(dash, i.ToString(), 12, muted, topLeft, new Vector2(x - 2, -74), new Vector2(15, 15));
        }
        Panel(dash, "Divider", topLeft, new Vector2(22, -165), new Vector2(300, 1), Recess);
        throttleFill = Pedal(dash, "ГАЗ", 22, accent);
        brakeFill = Pedal(dash, "ТОРМОЗ", 178, Red);

        var timer = Panel(readoutRect, "Timing tower", topLeft, new Vector2(30, -29), new Vector2(319, 166), PanelColor);
        Panel(timer, "Timing accent", topLeft, Vector2.zero, new Vector2(4, 166), Red);
        Label(timer, "ТЕКУЩИЙ КРУГ", 15, muted, topLeft, new Vector2(18, -12), new Vector2(280, 24));
        timing = Label(timer, "", 36, Color.white, topLeft, new Vector2(18, -34), new Vector2(282, 43));
        Panel(timer, "Timing divider", topLeft, new Vector2(18, -86), new Vector2(282, 1), Recess);
        Label(timer, "ПОСЛЕДНИЙ", 14, muted, topLeft, new Vector2(18, -99), new Vector2(140, 20));
        Label(timer, "РЕКОРД ЗАЕЗДА", 14, muted, topLeft, new Vector2(171, -99), new Vector2(130, 20));
        lastTime = Label(timer, "", 21, Color.white, topLeft, new Vector2(18, -121), new Vector2(145, 26));
        bestTime = Label(timer, "", 21, accent, topLeft, new Vector2(171, -121), new Vector2(130, 26));
        startSignal=new GameObject("Start lights").AddComponent<StartSignal>();startSignal.Build(root.transform);
        status = Label(root.transform, "", 23, Color.white, new Vector2(.5f, 1), new Vector2(0, -34), new Vector2(450, 67));
        status.alignment = TextAnchor.MiddleCenter;
        cameraLabel = Label(readoutRect, "", 15, Color.white, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(233, 24));
        cameraLabel.alignment = TextAnchor.UpperRight;
        cameraLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
        status.gameObject.AddComponent<Outline>().effectColor = Color.black;

    }

    RectTransform Pedal(Transform parent, string caption, float x, Color color)
    {
        Label(parent, caption, 12, muted, new Vector2(0, 1), new Vector2(x, -176), new Vector2(130, 16));
        var rail = Panel(parent, caption + " rail", new Vector2(0, 1), new Vector2(x, -197), new Vector2(143, 6), Recess);
        return Panel(rail, caption + " fill", Vector2.zero, Vector2.zero, rail.sizeDelta, color);
    }

    Button MakeButton(Transform parent, string caption, Vector2 position, Color color)
    {
        var rect = Panel(parent, caption, new Vector2(0, 1), position, new Vector2(246, 49), color);
        rect.GetComponent<Image>().raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var label = Label(rect, caption, 18, ink, new Vector2(.5f, .5f), Vector2.zero, rect.sizeDelta);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    static RectTransform Place(GameObject item, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)item.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        var image = item.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return Place(item, parent, anchor, position, size);
    }

    Text Label(Transform parent, string value, int size, Color color, Vector2 anchor, Vector2 position, Vector2 bounds)
    {
        var item = new GameObject(string.IsNullOrEmpty(value) ? "Readout" : value, typeof(RectTransform), typeof(Text));
        Place(item, parent, anchor, position, bounds);
        var text = item.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }
}
