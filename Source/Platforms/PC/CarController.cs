using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    // =====================================================================
    //  ДВИГАТЕЛЬ
    // =====================================================================
    [Header("Двигатель")]
    [HideInInspector] public Vector3 centerOfMassOffset = new Vector3(0f, .25f, 0f);
    public float maxEngineRPM = 8500f;
    public float idleRPM = 800f;
    public float engineTorque = 675f;

    [Tooltip("Кривая момента (X = обороты 0..1 от maxEngineRPM, Y = множитель момента 0..1)")]
    public AnimationCurve engineTorqueCurve = new AnimationCurve(
        new Keyframe(0f, 0.4f),
        new Keyframe(0.15f, 0.65f),
        new Keyframe(0.35f, 0.85f),
        new Keyframe(0.55f, 1f),
        new Keyframe(0.75f, 0.95f),
        new Keyframe(0.9f, 0.85f),
        new Keyframe(1f, 0.75f)
    );

    [Header("Инерция двигателя и сцепление")]
    [Min(0.1f)] public float engineInertia = 0.42f;
    [Min(1f)] public float clutchCapacity = 850f;
    [Min(1f)] public float clutchStiffness = 14f;
    [Min(1f)] public float revLimiterZone = 250f;
    [Min(0f)] public float engineBrakingTorque = 105f;

    // =====================================================================
    //  ТРАНСМИССИЯ
    // =====================================================================
    [Header("Трансмиссия")]
    public float[] gearRatios = { 2.75f, 1.95f, 1.48f, 1.17f, 0.96f };
    public float finalDrive = 3.7f;
    public float reverseGearRatio = 2.9f;
    [Range(0.5f, 1f)] public float drivetrainEfficiency = 0.9f;
    public float wheelRadius = 0.35f;
    [Tooltip("Время переключения: момент отсечён, двигатель без нагрузки")]
    public float shiftTime = 0.16f;

    [Header("Режим коробки")]
    public bool automaticTransmission = false;
    public KeyCode toggleTransmissionKey = KeyCode.T;
    [Tooltip("Автомат: повышающая при этих оборотах")]
    public float autoUpshiftRPM = 8100f;
    [Tooltip("Автомат: понижающая ниже этих оборотов")]
    public float autoDownshiftRPM = 4600f;
    [Tooltip("Автомат: минимальная пауза между переключениями (защита от 'качания')")]
    public float autoShiftCooldown = 0.7f;
    [Tooltip("Понижающую передачу не пускаем, если обороты на ней превысят максимум больше чем в столько раз")]
    public float downshiftOverRevMargin = 1.05f;

    [Header("Старт с места")]
    [Tooltip("Обороты, около которых автоматическое сцепление полностью включается при старте.")]
    public float launchRPM = 2700f;

    [Header("Плавный разгон")]
    [Tooltip("Как быстро нарастает газ при нажатии W (1/с). 2.5 = полный газ за 0.4 с. Меньше = плавнее")]
    public float throttleRiseRate = 2.5f;
    [Tooltip("Контроль тяги: 0 = выкл, 1 = сильно режет момент при пробуксовке")]
    [Range(0f, 1f)] public float tractionControl = 0.7f;
    [Tooltip("Продольное проскальзывание ведущих колёс, с которого начинает резаться момент")]
    public float tractionSlipLimit = 0.3f;

    // =====================================================================
    //  ТОРМОЗА / ЗАДНИЙ ХОД / УПРАВЛЕНИЕ / АЭРОДИНАМИКА
    // =====================================================================
    [Header("Тормоза и задний ход")]
    public float brakeTorque = 3000f;
    public float handbrakeTorque = 5000f;
    [Tooltip("Ниже этой скорости (м/с) S считается газом на заднем ходу, выше — тормозом")]
    public float brakeToReverseThreshold = 0.35f;
    [Tooltip("Максимальная скорость заднего хода (м/с)")]
    public float maxReverseSpeed = 15f;

    [Header("Управление")]
    public float maxSteerAngle = 30f;
    public float steerSpeed = 5f;

    [Header("Аэродинамика")]
    public float downforce = 1.8f;
    [Tooltip("Сопротивление воздуха: F = aeroDrag * v². ~0.45 для кузова NASCAR")]
    public float aeroDrag = 0.45f;
    [Tooltip("Коэффициент сопротивления качению (× вес)")]
    public float rollingResistance = 0.015f;

    [Header("Колёса (визуал)")]
    public Transform[] wheelMeshes = new Transform[4];
    public WheelCollider[] wheelColliders = new WheelCollider[4];

    [Header("Сглаживание для HUD и звука")]
    public float displaySmoothing = 40f;

    // ---------------------------------------------------------------------
    // Состояние
    // ---------------------------------------------------------------------
    private Rigidbody rb;
    private Quaternion[] wheelMeshRestRotation;

    private float engineRPM;          // реальные обороты двигателя (физика)
    private float displayRPM;         // сглаженные для HUD/звука
    private int currentGear = 0;      // индекс: 0 = 1-я передача
    private bool inReverse = false;
    private bool isShifting = false;
    public event System.Action GearEngaged;
    public AudioClip tireSquealClip;
    private float shiftTimer = 0f;
    private float sinceLastShift = 999f;
    private bool limiterCutActive = false;
    private float forwardSpeed;       // м/с вдоль кузова (со знаком)

    // Ввод
    private float throttleInput;
    private float throttleSmoothed;
    private float brakeInput;
    private bool brakeHeld;
    private bool reverseArmed = true;
    private int pendingGear;
    private float clutchEngagement;
    private bool clutchPedalPressed;
    private float gearboxInputRPM;
    private float drivenWheelRPM;
    private float axleTorque;
    private bool clutchLocked;
    private float brakePressure;
    private readonly float[] absPressure = { 1f, 1f, 1f, 1f };
    private bool launchStaged;
    private bool launchHeld;
    private readonly bool[] detachedWheels = new bool[4];
    public bool ForgiveMistakes { get; set; } = true;
    public int DetachedWheelCount { get; private set; }
    private float launchAssistLeft;
    private bool absActive;

    private const float RpmToRadians = 2f * Mathf.PI / 60f;
    private float steerInput;
    private bool shiftUp;
    private bool shiftDown;
    private bool handbrake;
    private readonly WheelFrictionCurve[] rearGrip = new WheelFrictionCurve[2];
    private readonly float[] rearGripScale = { 1f, 1f };

    // =====================================================================
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        motionConstraints = rb.constraints;
    }

    [System.NonSerialized] public float RivalPowerMultiplier=1f;
    public VehicleDamage Damage {get;private set;}
    public float TopSpeedKmh {get;set;}=450f;
    void Start()
    {
        RacePerformance.Configure(this,externalDriver?RivalPowerMultiplier:1f);
        Damage=gameObject.AddComponent<VehicleDamage>();
        rb.centerOfMass = centerOfMassOffset;

        foreach (WheelCollider wheel in wheelColliders)
        {
            if (wheel == null) continue;
            wheel.ConfigureVehicleSubsteps(10f, 8, 6);
            break;
        }

        if (gearRatios == null || gearRatios.Length == 0) gearRatios = new[] { 1f };
        currentGear = Mathf.Clamp(currentGear, 0, gearRatios.Length - 1);

        for (int i = 0; i < rearGrip.Length; i++)
            rearGrip[i] = wheelColliders[i + 2].sidewaysFriction;
        var tireEffects = gameObject.AddComponent<SpeedwayTireSmoke>();
        tireEffects.car = this;
        tireEffects.squealClip = tireSquealClip;

        engineRPM = idleRPM;
        displayRPM = idleRPM;

        // Ориентация меша относительно оси коллайдера до начала вращения.
        int wheelCount = Mathf.Min(wheelMeshes.Length, wheelColliders.Length);
        wheelMeshRestRotation = new Quaternion[wheelCount];
        for (int i = 0; i < wheelCount; i++)
        {
            if (wheelMeshes[i] == null || wheelColliders[i] == null) continue;

            wheelMeshRestRotation[i] =
                Quaternion.Inverse(wheelColliders[i].transform.rotation) * wheelMeshes[i].rotation;
        }
    }

    void Update()
    {
        if (raceControls && !externalDriver) ReadInput();

        // Физика идёт шагами FixedUpdate — для HUD и звука сглаживаем, чтобы обороты не "ступенили"
        displayRPM = Mathf.Lerp(displayRPM, engineRPM, 1f - Mathf.Exp(-displaySmoothing * Time.deltaTime));
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        UpdateLaunch(Mathf.Abs(forwardSpeed), !externalDriver && Input.GetKey(KeyCode.W), !externalDriver && Input.GetKey(KeyCode.Space), !externalDriver && Input.GetKey(KeyCode.S));
        UpdatePedals(dt);
        if (resting && (throttleInput > .01f || handbrake || brakeHeld || clutchPedalPressed))
        {
            rb.constraints = motionConstraints;
            resting = false;
        }

        float wheelRPM = RearAxleRPM();
        drivenWheelRPM = Mathf.Lerp(drivenWheelRPM, wheelRPM, 1f - Mathf.Exp(-22f * dt));
        HandleTransmission(dt);
        UpdatePowertrain(dt);
        ApplyBrakes(dt);
        ApplySteering();
        ApplyDownforce();
    }

    private bool raceControls = true;
    private bool externalDriver;
    private float externalSteeringAngle=float.NaN;
    public void SetDriverSteeringAngle(float degrees){if(externalDriver)externalSteeringAngle=Mathf.Clamp(degrees,-maxSteerAngle,maxSteerAngle);}
    private float externalBrake;
    private bool externalReverse;
    public bool IsExternallyDriven => externalDriver;

    public void UseExternalDriver()
    {
        externalDriver = true;
        automaticTransmission = true;
        throttleInput = throttleSmoothed = steerInput = 0f;
        externalBrake = 1f;
        brakeHeld = true;
        launchHeld = launchStaged = handbrake = clutchPedalPressed = false;
    }

    public void SetDriverInputs(float throttle, float brake, float steering, bool reverse = false)
    {
        if (!externalDriver) return;
        throttleInput = Mathf.Clamp01(throttle);
        externalSteeringAngle=float.NaN;
        externalBrake = Mathf.Clamp01(brake);
        brakeHeld = externalBrake > .01f;
        steerInput = Mathf.Clamp(steering, -1f, 1f);
        externalReverse = reverse;
    }


    public void SetRaceControls(bool allowed)
    {
        raceControls = allowed;
        if (allowed) return;
        throttleInput = throttleSmoothed = steerInput = 0f;
        shiftUp = shiftDown = brakeHeld = handbrake = clutchPedalPressed = false;
    }

    public void SetLaunchStaged(bool staged) => launchStaged = staged;

    void UpdateLaunch(float speed, bool gas, bool parkingBrake, bool serviceBrake)
    {
        bool chord = gas && parkingBrake && !serviceBrake;
        bool hold = (raceControls || launchStaged) && chord && !inReverse && currentGear == 0 &&
            DetachedWheelCount == 0 && speed < 1.2f && GetSpeedKmh() < 4.5f && !isShifting;
        if (launchHeld && !hold && raceControls && gas && !parkingBrake)
            launchAssistLeft = 0.65f;

        launchHeld = hold;
        if (launchHeld) throttleSmoothed = 0f;
    }

    float RearAxleRPM()
    {
        float rpm = 0f;
        int connected = 0;
        for (int i = 2; i < 4; i++)
        {
            if (detachedWheels[i]) continue;
            rpm += wheelColliders[i].rpm;
            connected++;
        }
        return connected > 0 ? rpm / connected : 0f;
    }

    void ReadInput()
    {
        throttleInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        brakeHeld = Input.GetKey(KeyCode.S);
        steerInput = Input.GetAxis("Horizontal");
        handbrake = Input.GetKey(KeyCode.Space);
        clutchPedalPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Input.GetKeyDown(KeyCode.E)) shiftUp = true;
        if (Input.GetKeyDown(KeyCode.Q)) shiftDown = true;
        if (Input.GetKeyDown(toggleTransmissionKey)) automaticTransmission = !automaticTransmission;
    }

    void UpdatePedals(float dt)
    {
        if (externalDriver)
        {
            if (externalReverse != inReverse && Mathf.Abs(forwardSpeed) < .5f)
            {
                inReverse = externalReverse; currentGear = pendingGear = 0;
                isShifting = false; shiftTimer = 0f; clutchEngagement = 0f;
                throttleSmoothed = 0f;
            }
            brakeInput = externalBrake;
            float demand = brakeInput > .05f ? 0f : throttleInput;
            throttleSmoothed = Mathf.MoveTowards(throttleSmoothed, demand,
                dt * (demand > throttleSmoothed ? throttleRiseRate : 10f));
            return;
        }
        bool stopped = Mathf.Abs(forwardSpeed) <= brakeToReverseThreshold;
        if (!brakeHeld) reverseArmed = true;
        if (brakeHeld && !inReverse && !stopped) reverseArmed = false;

        bool nextReverse = inReverse;
        if (stopped && throttleInput > 0f) nextReverse = false;
        else if (stopped && brakeHeld && reverseArmed && throttleInput == 0f) nextReverse = true;

        if (nextReverse != inReverse)
        {
            inReverse = nextReverse;
            currentGear = 0;
            shiftTimer = 0f;
            isShifting = false;
            clutchEngagement = 0f;
            throttleSmoothed = 0f;
        }

        bool braking = inReverse ? throttleInput > 0f : brakeHeld;
        brakeInput = braking ? 1f : 0f;
        float pedal = braking ? 0f : (inReverse ? (brakeHeld ? 1f : 0f) : throttleInput);
        float rate = pedal > throttleSmoothed ? throttleRiseRate : 10f;
        throttleSmoothed = Mathf.MoveTowards(throttleSmoothed, pedal, dt * rate);
    }

    void HandleTransmission(float dt)
    {
        sinceLastShift += dt;
        if (isShifting)
        {
            shiftTimer -= dt;
            if (shiftTimer <= 0f)
            {
                currentGear = pendingGear;
                isShifting = false;
                GearEngaged?.Invoke();
            }
            shiftUp = shiftDown = false;
            return;
        }

        int requestedGear = currentGear;
        if (!inReverse)
        {
            if (shiftUp) requestedGear++;
            else if (shiftDown) requestedGear--;
            else if (automaticTransmission && !clutchPedalPressed && sinceLastShift >= autoShiftCooldown)
            {
                float shaftRPM = Mathf.Abs(GetRoadWheelRPM() * GetTotalRatio());
                if (throttleSmoothed > 0.2f && shaftRPM >= autoUpshiftRPM) requestedGear++;
                else if (shaftRPM < autoDownshiftRPM) requestedGear--;
            }
        }

        shiftUp = shiftDown = false;
        requestedGear = Mathf.Clamp(requestedGear, 0, gearRatios.Length - 1);
        if (requestedGear == currentGear) return;

        float targetRPM = PredictRPM(requestedGear);
        if (requestedGear < currentGear && targetRPM > maxEngineRPM * downshiftOverRevMargin) return;

        pendingGear = requestedGear;
        shiftTimer = Mathf.Max(shiftTime, dt);
        sinceLastShift = 0f;
        isShifting = true;
        clutchEngagement = 0f;
    }

    float GetTotalRatio() => (inReverse ? -reverseGearRatio : gearRatios[currentGear]) * finalDrive;

    float GetRoadWheelRPM() => forwardSpeed / (Mathf.Max(wheelRadius, 0.01f) * RpmToRadians);

    float PredictRPM(int gear)
    {
        // Используем и дорожную скорость, чтобы заблокированное колесо не разрешило опасное понижение.
        float roadRPM = Mathf.Abs(GetRoadWheelRPM());
        return Mathf.Max(roadRPM, Mathf.Abs(drivenWheelRPM)) * gearRatios[gear] * finalDrive;
    }

    void UpdatePowertrain(float dt)
    {
        float ratio = GetTotalRatio();
        float shaftRPM = drivenWheelRPM * ratio;
        // Передача связывает входной вал с колёсами даже при выжатом сцеплении.
        // В промежутке между передачами вал свободно замедляется.
        gearboxInputRPM = isShifting ? gearboxInputRPM * Mathf.Exp(-2.5f * dt) : Mathf.Abs(shaftRPM);
        float throttle = brakeInput > 0f || handbrake ? 0f : throttleSmoothed;
        if(!inReverse)throttle*=Mathf.InverseLerp(TopSpeedKmh+2f,TopSpeedKmh-1f,GetSpeedKmh());
        if (inReverse) throttle *= Mathf.InverseLerp(maxReverseSpeed, maxReverseSpeed - 2f, -forwardSpeed);
        bool launching = !inReverse && currentGear == 0 && throttle > .8f && forwardSpeed < 12f;
        if (launching && launchAssistLeft > 0f) launchAssistLeft -= dt;
        else if (forwardSpeed >= 12f || brakeInput > 0f || currentGear != 0) launchAssistLeft = 0f;
        float traction = GetTractionFactor();
        if (launching) traction = Mathf.Lerp(traction, 1f, launchAssistLeft > 0f ? 1f : .68f);
        bool launchRequested = launchHeld;
        float limit = launchRequested ? 3650f : maxEngineRPM;
        float hysteresis = launchRequested ? 100f : revLimiterZone;
        bool limitEnabled = (!isShifting || clutchPedalPressed) && (throttle > 0.1f || launchRequested);
        limiterCutActive = limitEnabled && engineRPM >= (limiterCutActive ? limit - hysteresis : limit);
        float limiter = limiterCutActive ? 0f : 1f;

        float rpmFraction = Mathf.Clamp01(engineRPM / maxEngineRPM);
        float idleTorque = Mathf.Clamp((idleRPM - engineRPM) * 0.8f, 0f, 140f);
        float combustion = (Damage?Damage.TorqueFactor:1f) * engineTorque * engineTorqueCurve.Evaluate(rpmFraction) * throttle * traction * limiter;
        float friction = engineBrakingTorque * rpmFraction;
        float netTorque = combustion + idleTorque - friction;

        if (launchStaged || launchHeld)
        {
            netTorque = launchRequested ? (limiterCutActive ? -85f : 175f) : idleTorque - friction;
        }
        else if (isShifting && !clutchPedalPressed)
        {
            float targetRPM = Mathf.Clamp(PredictRPM(pendingGear), idleRPM, maxEngineRPM);
            // Электронное согласование оборотов при разомкнутом сцеплении.
            netTorque = Mathf.Clamp((targetRPM - engineRPM) * 0.55f, -250f, 380f);
        }

        float rollingClutch = Mathf.InverseLerp(idleRPM * 0.85f, idleRPM * 1.45f, shaftRPM);
        float launchClutch = Mathf.InverseLerp(idleRPM + 250f, launchRPM, engineRPM) * Mathf.Sqrt(throttle);
        float targetClutch = isShifting || launchStaged || launchHeld || handbrake ? 0f : Mathf.Max(rollingClutch, launchClutch);
        clutchEngagement = clutchPedalPressed ? 0f :
            Mathf.MoveTowards(clutchEngagement, targetClutch, dt * (launching ? 12f : 7f));

        float inertia = Mathf.Max(0.1f, engineInertia);
        float freeOmega = engineRPM * RpmToRadians + netTorque * dt / inertia;
        float coupling = clutchStiffness * clutchEngagement;
        // Неявный шаг сцепления ограничивает отдачу момента при большом перепаде оборотов.
        float slip = freeOmega - shaftRPM * RpmToRadians;
        float capacity = clutchCapacity * clutchEngagement;
        float wheelInertiaTorque = Mathf.Clamp((engineRPM - shaftRPM) * RpmToRadians * inertia / .09f, -180f, 180f);
        float lockingTorque = netTorque + wheelInertiaTorque;
        bool synchronized = Mathf.Abs(engineRPM - shaftRPM) < (clutchLocked ? 850f : 520f);
        clutchLocked = !isShifting && !launchStaged && !launchHeld && clutchEngagement >= .98f &&
            shaftRPM > idleRPM && brakeInput <= 0f && !handbrake && synchronized && Mathf.Abs(lockingTorque) <= capacity;
        float slippingTorque = slip * coupling / (1f + coupling * dt / inertia);
        float clutchTorque = clutchLocked ? lockingTorque : Mathf.Clamp(slippingTorque, -capacity, capacity);
        engineRPM = clutchLocked ? Mathf.Lerp(engineRPM, shaftRPM, 1f - Mathf.Exp(-28f * dt)) :
            Mathf.Clamp((freeOmega - clutchTorque * dt / inertia) / RpmToRadians,
            idleRPM * 0.65f, maxEngineRPM * 1.08f);

        axleTorque = clutchTorque * ratio * drivetrainEfficiency;
        if (handbrake || launchHeld) axleTorque = 0f;
        else if (brakeInput > 0f) axleTorque *= 0.18f;
        // Небольшая блокировка пластинчатого дифференциала при разнице скоростей колёс.
        float differential = handbrake || brakeInput > 0f || detachedWheels[2] || detachedWheels[3] ? 0f : Mathf.Clamp((wheelColliders[2].rpm - wheelColliders[3].rpm) * 0.30f, -95f, 95f);
        if (!detachedWheels[0]) wheelColliders[0].motorTorque = 0f;
        if (!detachedWheels[1]) wheelColliders[1].motorTorque = 0f;
        if (!detachedWheels[2]) wheelColliders[2].motorTorque = 0.5f * axleTorque - differential;
        if (!detachedWheels[3]) wheelColliders[3].motorTorque = 0.5f * axleTorque + differential;
    }

    private float tractionFactorSmoothed = 1f;

    // Множитель момента 0.2..1 по проскальзыванию задних колёс (WheelHit.forwardSlip)
    float GetTractionFactor()
    {
        float target = 1f;

        if (tractionControl > 0.01f && wheelColliders.Length >= 4)
        {
            float slip = 0f;
            int counted = 0;
            for (int i = 2; i <= 3; i++)
            {
                WheelHit hit;
                if (wheelColliders[i] != null && wheelColliders[i].GetGroundHit(out hit))
                {
                    slip += Mathf.Abs(hit.forwardSlip);
                    counted++;
                }
            }

            if (counted > 0)
            {
                slip /= counted;
                float excess = Mathf.Clamp01((slip - tractionSlipLimit) / 0.3f);
                target = 1f - excess * tractionControl * 0.8f;
            }
        }

        // Режем быстро, возвращаем мягче — чтобы момент не "дребезжал"
        float rate = target < tractionFactorSmoothed ? 12f : 4f;
        tractionFactorSmoothed = Mathf.MoveTowards(tractionFactorSmoothed, target, rate * Time.fixedDeltaTime);
        return tractionFactorSmoothed;
    }

    private bool resting;
    private RigidbodyConstraints motionConstraints;

    void ApplyBrakes(float dt)
    {
        bool noPedal = throttleInput < .01f && !brakeHeld && !handbrake && !clutchPedalPressed && !launchHeld;
        bool allGrounded = wheelColliders[0].isGrounded && wheelColliders[1].isGrounded &&
            wheelColliders[2].isGrounded && wheelColliders[3].isGrounded;
        bool canHold = !externalDriver && noPedal && allGrounded && DetachedWheelCount == 0 &&
            Vector3.Dot(transform.up, Vector3.up) > .85f;
        if (resting && (!canHold || rb.linearVelocity.sqrMagnitude > .25f))
        {
            rb.constraints = motionConstraints;
            resting = false;
        }
        if (!resting && canHold && rb.linearVelocity.sqrMagnitude < .035f * .035f &&
            rb.angularVelocity.sqrMagnitude < .04f * .04f)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = motionConstraints | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
            resting = true;
        }
        float target = !raceControls || launchHeld || (canHold && GetSpeedKmh() < .7f) ? 1f : brakeInput;
        brakePressure = Mathf.MoveTowards(brakePressure, target, dt * (target > brakePressure ? 7f : 18f));
        absActive = false;
        int grounded = 0;
        for (int i = 0; i < 4; i++)
        {
            if (detachedWheels[i]) continue;
            WheelCollider wheel = wheelColliders[i];
            bool contact = wheel.GetGroundHit(out WheelHit hit);
            if (contact) grounded++;
            float roadSpeed = contact ? Mathf.Abs(Vector3.Dot(rb.GetPointVelocity(hit.point), hit.forwardDir)) : 0f;
            float treadSpeed = Mathf.Abs(wheel.rpm) * RpmToRadians * wheel.radius;
            float lockup = (roadSpeed - treadSpeed) / Mathf.Max(roadSpeed, 1f);
            bool release = ForgiveMistakes && brakePressure > .05f && roadSpeed > 5f && lockup > (i < 2 ? .20f : .15f);
            absPressure[i] = Mathf.MoveTowards(absPressure[i], release ? .16f : 1f, dt * (release ? 42f : 7f));
            absActive |= brakePressure > .05f && absPressure[i] < .96f;
            float service = brakeTorque * brakePressure * (i < 2 ? 1f : .44f) * absPressure[i];
            wheel.brakeTorque = service + (i >= 2 && handbrake ? handbrakeTorque : 0f);
            if (i < 2) continue;

            // Заблокированная задняя шина тратит сцепление на продольное скольжение.
            float slide = handbrake && contact ? Mathf.InverseLerp(.15f, .85f, lockup) *
                Mathf.InverseLerp(2f, 8f, roadSpeed) : 0f;
            int rear = i - 2;
            float targetGrip = Mathf.Lerp(1f, .32f, slide);
            rearGripScale[rear] = Mathf.MoveTowards(rearGripScale[rear], targetGrip, dt * (handbrake ? 14f : 4f));
            var friction = rearGrip[rear];
            friction.stiffness *= rearGripScale[rear];
            wheel.sidewaysFriction = friction;
        }

        if (!ForgiveMistakes || brakePressure < .1f || handbrake || grounded < 2 || Mathf.Abs(forwardSpeed) < 5f) return;

        // Подавляем избыточное вращение только при торможении, сохраняя реакцию на руль.
        float lateral = Vector3.Dot(rb.linearVelocity, transform.right);
        float sideslip = Mathf.Atan2(lateral, Mathf.Abs(forwardSpeed));
        float yaw = Vector3.Dot(rb.angularVelocity, transform.up);
        float desiredYaw = forwardSpeed * Mathf.Tan(wheelColliders[0].steerAngle * Mathf.Deg2Rad) / 2.78f;
        float correction = Mathf.Clamp((desiredYaw - yaw) * 3f + sideslip * 5f, -3f, 3f);
        rb.AddTorque(transform.up * (correction * brakePressure), ForceMode.Acceleration);
    }

    void ApplySteering()
    {
        // Разворот на малой скорости и ограничение бокового ускорения на прямой.
        float speedSquared = rb.linearVelocity.sqrMagnitude;
        float wheelbase = Mathf.Abs(transform.InverseTransformPoint(wheelColliders[0].transform.position).z -
            transform.InverseTransformPoint(wheelColliders[2].transform.position).z);
        float speedLimit = Mathf.Atan2((ForgiveMistakes ? 14f : 23f) * wheelbase, speedSquared) * Mathf.Rad2Deg;
        float angle = externalDriver && float.IsFinite(externalSteeringAngle) ? externalSteeringAngle : DrivingSettings.SteeringAngle(steerInput, maxSteerAngle, speedLimit, externalDriver);
        float response = externalDriver ? 1f : Mathf.Sqrt(DrivingSettings.Sensitivity);
        float smoothing = 1f - Mathf.Exp(-steerSpeed * response * Time.fixedDeltaTime);

        wheelColliders[0].steerAngle = Mathf.Lerp(wheelColliders[0].steerAngle, angle, smoothing);
        wheelColliders[1].steerAngle = Mathf.Lerp(wheelColliders[1].steerAngle, angle, smoothing);
    }

    void ApplyDownforce()
    {
        float speed = rb.linearVelocity.magnitude;
        Vector3 airVelocity = rb.linearVelocity;
        Vector3 underside = transform.up;
        float highSpeed = Mathf.InverseLerp(43f, 78f, speed);
        float yawSlip = Mathf.InverseLerp(.44f, .94f, Mathf.Abs(Vector3.Dot(airVelocity.normalized, transform.right)));
        float noseUp = Mathf.InverseLerp(.10f, .46f, Vector3.Dot(transform.forward, Vector3.up));
        bool wheelsLight = !wheelColliders[0].isGrounded || !wheelColliders[1].isGrounded;
        float liftExposure = ForgiveMistakes ? 0f : highSpeed * Mathf.Max(yawSlip, wheelsLight ? noseUp : 0f);
        rb.AddForce(-Vector3.up * downforce * speed * speed *
            Mathf.Clamp01(Vector3.Dot(underside, Vector3.up)) * (1f - liftExposure));
        if (liftExposure > 0f)
        {
            // Угол к потоку и открывшееся днище смещают центр аэродинамической нагрузки.
            Vector3 pressureCenter = transform.TransformPoint(new Vector3(.52f *
                Mathf.Sign(Vector3.Dot(airVelocity, transform.right)), .14f, .72f));
            rb.AddForceAtPosition(Vector3.up * (speed * speed * 3.5f * liftExposure), pressureCenter);
        }
        if (!ForgiveMistakes)
        {
            float slides = Mathf.InverseLerp(.14f, .58f, Mathf.Abs(Vector3.Dot(airVelocity.normalized, transform.right)));
            for (int i = 0; i < rearGrip.Length; i++)
            {
                var wheel = wheelColliders[i + 2];
                if (detachedWheels[i + 2]) continue;
                var friction = wheel.sidewaysFriction;
                friction.stiffness *= 1f - highSpeed * slides * .27f;
                wheel.sidewaysFriction = friction;
            }
        }

        // Настоящее сопротивление: воздух растёт с квадратом скорости + сопротивление качению.
        // Раньше сопротивление было ничтожным, и машина по инерции почти не теряла скорость на отсечке.
        if (speed > 0.05f)
        {
            Vector3 dir = rb.linearVelocity / speed;
            float airDrag = aeroDrag * speed * speed;
            float rolling = rollingResistance * rb.mass * 9.81f * Mathf.Clamp01(speed);
            rb.AddForce(-dir * (airDrag + rolling));
        }
    }

    void OnCollisionEnter(Collision collision){Damage?.Impact(collision);}

    public void BreakImpactWheel(Vector3 point, Vector3 normal, float impact)
    {
        int nearest = -1;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < detachedWheels.Length; i++)
        {
            if (detachedWheels[i] || wheelMeshes[i] == null) continue;
            float candidate = (wheelMeshes[i].position - point).sqrMagnitude;
            if (candidate >= distance) continue;
            nearest = i;
            distance = candidate;
        }
        if (nearest < 0) return;

        DetachWheel(nearest, normal, impact);
        int sameSide = nearest ^ 2;
        if (impact >= (ForgiveMistakes ? 33f : 26f) &&
            !detachedWheels[sameSide] && wheelMeshes[sameSide] != null)
            DetachWheel(sameSide, normal, impact * .72f);
    }

    void DetachWheel(int index, Vector3 normal, float impact)
    {
        var wheel = wheelColliders[index];
        var mesh = wheelMeshes[index];
        float spin = wheel.rpm;
        Vector3 velocity = rb.GetPointVelocity(mesh.position);
        detachedWheels[index] = true;
        DetachedWheelCount++;
        wheel.motorTorque = wheel.brakeTorque = 0f;
        wheel.enabled = false;
        wheelMeshes[index] = null;

        mesh.SetParent(null, true);
        var collider = mesh.gameObject.AddComponent<SphereCollider>();
        collider.radius = wheel.radius * .86f;
        var debris = mesh.gameObject.AddComponent<Rigidbody>();
        debris.mass = wheel.mass;
        debris.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        debris.interpolation = RigidbodyInterpolation.Interpolate;
        foreach (var chassis in GetComponentsInChildren<Collider>())
            if (chassis.enabled) Physics.IgnoreCollision(chassis, collider);
        debris.linearVelocity = velocity + normal * (impact * .14f) + Vector3.up * 1.4f;
        debris.angularVelocity = mesh.right * Mathf.Clamp(spin * RpmToRadians, -55f, 55f);
        Destroy(mesh.gameObject, 45f);
    }

    void LateUpdate()
    {
        for (int i = 0; i < wheelMeshRestRotation.Length; i++)
        {
            Transform mesh = wheelMeshes[i];
            if (detachedWheels[i]) continue;
            WheelCollider wheel = wheelColliders[i];
            if (mesh == null || wheel == null) continue;

            wheel.GetWorldPose(out Vector3 position, out Quaternion pose);
            Quaternion steering = wheel.transform.rotation * Quaternion.AngleAxis(wheel.steerAngle, Vector3.up);
            Quaternion spin = Quaternion.Inverse(steering) * pose;
            Quaternion rest = wheelMeshRestRotation[i];

            // Правые колёса модели развёрнуты на 180°, поэтому для них знак вращения обратный.
            float direction = Mathf.Sign(Vector3.Dot(rest * Vector3.right, Vector3.right));
            Quaternion meshSpin = new Quaternion(spin.x * direction, 0f, 0f, spin.w).normalized;
            mesh.SetPositionAndRotation(position, steering * rest * meshSpin);
        }
    }

    // =====================================================================
    //  Публичные геттеры для UI и звука
    // =====================================================================
    public float GetSpeedKmh() => Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude * 3.6f;
    public float GetGearboxInputRPM() => gearboxInputRPM;
    public bool IsClutchPedalPressed() => clutchPedalPressed;
    public float GetDrivetrainLoad() => Mathf.Clamp01(Mathf.Abs(axleTorque) / 3000f);
    public float GetRPM() => displayRPM;
    public float GetRawRPM() => engineRPM; // для диагностики — несглаженные "реальные" обороты
    public int GetGear() => currentGear + 1;
    public string GetGearLabel() => inReverse ? "R" : (currentGear + 1).ToString();
    public bool IsAutomatic() => automaticTransmission;
    public float GetThrottle() => throttleSmoothed;
    public float GetBrakePressure() => raceControls ? brakePressure : 0f;
    public bool IsHandbrakeActive() => handbrake;
    public bool IsAbsActive() => absActive;
    public bool IsLaunchStaged() => launchHeld && engineRPM > 2100f;
    public bool IsRevLimiterActive() => limiterCutActive;
    public bool IsShifting() => isShifting;
}
