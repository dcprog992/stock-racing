using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2.65f, -6.3f);
    public float followSpeed = 5f;
    public float rotationSpeed = 5f;
    public float orbitSensitivity = 3f;
    public float returnSpeed = 4f;
    public float pitchLimit = 60f;

    public enum View { Fixed, Dynamic, Cockpit, Hood }
    public View CurrentView { get; private set; } = View.Dynamic;
    public Vector3 cockpitOffset = new Vector3(-.37762f, 1.00058f, -.50727f);
    public Quaternion cockpitRotation = new Quaternion(.037490f, .007456f, .000490f, -.999269f);
    public float cockpitFov = 60f;
    public Vector3 hoodOffset = new Vector3(0f, 1.05f, 1.45f);

    public void CycleView()
    {
        CurrentView = (View)(((int)CurrentView + 1) % 4);
        orbitYaw = orbitPitch = 0f;
    }

    private float orbitYaw;
    private float orbitPitch;
    private Camera chaseCamera;
    private Rigidbody targetBody;
    private Vector3 lastTargetPosition;
    private float baseFov;
    private Vector3 previousVelocity;
    private Vector3 cockpitAcceleration;
    private Vector3 chaseForward;
    private float chaseHeight, heightVelocity, chaseSway, swayVelocity;

    void Start()
    {
        chaseCamera = GetComponent<Camera>();
        baseFov = chaseCamera.fieldOfView;
        if (target == null) return;
        targetBody = target.GetComponentInParent<Rigidbody>();
        if (target.TryGetComponent<CarController>(out var car))
            gameObject.AddComponent<CockpitInstruments>().Initialize(this, car);
        lastTargetPosition = target.position;
        chaseForward=Vector3.ProjectOnPlane(target.forward,Vector3.up).normalized;chaseHeight=target.position.y;
    }

    void FixedUpdate()
    {
        if (targetBody == null) return;
        Vector3 velocity = targetBody.linearVelocity;
        Vector3 acceleration = target.InverseTransformDirection((velocity - previousVelocity) / Time.fixedDeltaTime);
        previousVelocity = velocity;
        cockpitAcceleration = Vector3.Lerp(cockpitAcceleration, Vector3.ClampMagnitude(acceleration, 18f),
            1f - Mathf.Exp(-8f * Time.fixedDeltaTime));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C)) CycleView();
    }

    void LateUpdate()
    {
        if (target == null || Time.deltaTime <= 0f) return;

        float dt = Time.deltaTime;
        Vector3 travel = target.position - lastTargetPosition;
        lastTargetPosition = target.position;
        if (CurrentView == View.Cockpit || CurrentView == View.Hood)
        {
            bool cockpit = CurrentView == View.Cockpit;
            Vector3 local = cockpit ? cockpitOffset : hoodOffset;
            Quaternion rotation = cockpit ? cockpitRotation.normalized : Quaternion.Euler(2f, 0f, 0f);
            if (cockpit)
            {
                float sway = Mathf.Clamp(-cockpitAcceleration.x * .0027f, -.027f, .027f);
                float surge = Mathf.Clamp(-cockpitAcceleration.z * .0062f, -.060f, .042f);
                local += new Vector3(sway, Mathf.Min(0f, surge) * .25f, surge);
                rotation *= Quaternion.Euler(surge * 23f, -sway * 14f, sway * 39f);
            }
            transform.SetPositionAndRotation(target.TransformPoint(local), target.rotation * rotation);
            chaseCamera.nearClipPlane = .055f;
            chaseCamera.fieldOfView = cockpit ? cockpitFov : 65f;
            return;
        }

        chaseCamera.nearClipPlane = .2f;
        if (CurrentView == View.Fixed)
        {
            Vector3 flat = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (flat.sqrMagnitude < .001f) return;
            Quaternion rotation = Quaternion.LookRotation(flat);
            Vector3 fixedPosition = target.position + rotation * offset;
            transform.SetPositionAndRotation(fixedPosition, Quaternion.LookRotation(target.position + Vector3.up * .6f - fixedPosition));
            chaseCamera.fieldOfView = baseFov;
            return;
        }
        // Touches on driving controls must never become mouse-orbit gestures.
#if UNITY_ANDROID
        bool orbiting=false;
#else
        bool overUi=UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        bool orbiting=Input.touchCount==0 && !overUi && Input.GetMouseButton(1);
#endif
        if (orbiting)
        {
            orbitYaw += Input.GetAxis("Mouse X") * orbitSensitivity;
            orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxis("Mouse Y") * orbitSensitivity,
                -pitchLimit, pitchLimit);
        }
        else
        {
            float recenter = 1f - Mathf.Exp(-returnSpeed * dt);
            orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, recenter);
            orbitPitch = Mathf.Lerp(orbitPitch, 0f, recenter);
        }

        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .001f) return;
        if(chaseForward.sqrMagnitude<.01f)chaseForward=forward;
        chaseForward=Vector3.Slerp(chaseForward,forward,1f-Mathf.Exp(-5f*dt)).normalized;
        chaseHeight=Mathf.SmoothDamp(chaseHeight,target.position.y,ref heightVelocity,.16f,30f,dt);
        Quaternion heading = Quaternion.LookRotation(chaseForward, Vector3.up);
        Vector3 velocity = targetBody == null ? Vector3.zero : targetBody.linearVelocity;
        float speed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
        float highSpeed = Mathf.InverseLerp(0f, 78f, speed);
        float sidewaysSpeed = Vector3.Dot(velocity, heading * Vector3.right);
        chaseSway=Mathf.SmoothDamp(chaseSway,Mathf.Clamp(-sidewaysSpeed*.05f,-.35f,.35f),ref swayVelocity,.22f,2f,dt);
        Vector3 localOffset = offset + new Vector3(chaseSway, -.10f * highSpeed, -1.0f * highSpeed);
        Quaternion orbit = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        Vector3 anchor=new Vector3(target.position.x,chaseHeight,target.position.z);
        Vector3 aim = anchor + Vector3.up * .65f;
        Vector3 desired = anchor + heading * (orbit * localOffset);

        // Переносим камеру вместе с машиной. Сглаживается только относительная позиция.
        Vector3 carried = transform.position + Vector3.ProjectOnPlane(travel,Vector3.up);
        float follow = 1f - Mathf.Exp(-(followSpeed + highSpeed * 3f) * dt);
        Vector3 position = Vector3.Lerp(carried, desired, follow);
        float maxDistance = (desired - aim).magnitude + .35f;
        transform.position = aim + Vector3.ClampMagnitude(position - aim, maxDistance);

        Vector3 lookAhead = orbiting ? Vector3.zero : chaseForward * Mathf.Lerp(.8f, 3.4f, highSpeed);
        Vector3 look = aim + lookAhead - transform.position;
        if (look.sqrMagnitude > .001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look),
                1f - Mathf.Exp(-(rotationSpeed + 3f) * dt));

        chaseCamera.fieldOfView = Mathf.Lerp(chaseCamera.fieldOfView, baseFov + 6f * highSpeed,
            1f - Mathf.Exp(-4f * dt));
    }
}
