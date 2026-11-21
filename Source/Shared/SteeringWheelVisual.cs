using UnityEngine;

public sealed class SteeringWheelVisual : MonoBehaviour
{
    public CarController car;
    public Vector3 rotationAxis = new Vector3(0f, -.237f, .972f);
    [Min(0f)] public float steeringRatio = 10f;

    private Quaternion restRotation;
    private float angle;

    private void Awake()
    {
        restRotation = transform.localRotation;
    }

    private void LateUpdate()
    {
        float roadAngle = (car.wheelColliders[0].steerAngle + car.wheelColliders[1].steerAngle) * .5f;
        float target = -roadAngle * steeringRatio;
        angle = Mathf.Lerp(angle, target, 1f - Mathf.Exp(-17f * Time.deltaTime));
        transform.localRotation = restRotation * Quaternion.AngleAxis(angle, rotationAxis.normalized);
    }
}
