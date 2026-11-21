using UnityEngine;

// Стабилизатор поперечной устойчивости.
// Добавь ОТДЕЛЬНЫЙ экземпляр этого компонента на CarRoot для передней оси
// (WheelL = WheelFL, WheelR = WheelFR) и ещё один — для задней оси
// (WheelL = WheelRL, WheelR = WheelRR).
public class AntiRollBar : MonoBehaviour
{
    public WheelCollider WheelL;
    public WheelCollider WheelR;

    [Tooltip("Сила стабилизатора. Больше значение = меньше крен в повороте, но подвеска жёстче реагирует на неровности.")]
    public float AntiRoll = 5000f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponentInParent<Rigidbody>();
    }

    void FixedUpdate()
    {
        WheelHit hit;
        float travelL = 1f;
        float travelR = 1f;

        bool groundedL = WheelL.GetGroundHit(out hit);
        if (groundedL)
            travelL = (-WheelL.transform.InverseTransformPoint(hit.point).y - WheelL.radius) / WheelL.suspensionDistance;

        bool groundedR = WheelR.GetGroundHit(out hit);
        if (groundedR)
            travelR = (-WheelR.transform.InverseTransformPoint(hit.point).y - WheelR.radius) / WheelR.suspensionDistance;

        float antiRollForce = (travelL - travelR) * AntiRoll;

        if (groundedL)
            rb.AddForceAtPosition(WheelL.transform.up * -antiRollForce, WheelL.transform.position);
        if (groundedR)
            rb.AddForceAtPosition(WheelR.transform.up * antiRollForce, WheelR.transform.position);
    }
}