using UnityEngine;

public sealed class SpeedwayTireSmoke : MonoBehaviour
{
    public CarController car;
    private readonly ParticleSystem[] emitters = new ParticleSystem[2];
    private Texture2D texture;
    private Material material;
    private readonly float[] slipTime = new float[4];
    private AudioSource squeal;
    private CameraFollow cameraView;
    public AudioClip squealClip;

    void Start()
    {
        cameraView = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (squealClip != null)
        {
            squeal = gameObject.AddComponent<AudioSource>();
            squeal.clip = squealClip;
            squeal.loop = true;
            squeal.playOnAwake = false;
            squeal.dopplerLevel = 0f;
            squeal.volume = 0f;
            if (car.IsExternallyDriven)
            {
                squeal.spatialBlend = 1f;
                squeal.rolloffMode = AudioRolloffMode.Logarithmic;
                squeal.minDistance = 3f;
                squeal.maxDistance = 80f;
            }
            squeal.Play();
        }

        texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            var uv = new Vector2(x, y) / 63f;
            float radius = (uv - Vector2.one * .5f).magnitude * 2f;
            float noise = Mathf.PerlinNoise(uv.x * 6f + 11f, uv.y * 6f + 8f);
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius + (noise - .5f) * .34f), 1.6f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);

        var shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
        {
            return;
        }
        material = new Material(shader);
        material.mainTexture = texture;
        material.SetFloat("_Mode", 2);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        for (int i = 0; i < emitters.Length; i++)
        {
            var emitter = new GameObject("Rear tire smoke");
            emitter.transform.SetParent(transform, false);
            var particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 140;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(.38f, .73f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.30f, .85f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(.83f, .86f, .87f, .45f);
            main.gravityModifier = -.08f;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .17f;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(3.2f, AnimationCurve.EaseInOut(0f, .27f, 1f, 1f));
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.35f, 0f), new GradientAlphaKey(1f, .18f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            emitters[i] = particles;
            particles.Play();
        }
    }

    void LateUpdate()
    {
        float strongestSlip = 0f;
        for (int i = 0; i < slipTime.Length; i++)
        {
            var wheel = car.wheelColliders[i];
            var smoke = i >= 2 ? emitters[i - 2] : null;
            float amount = 0f;
            if (wheel.enabled && wheel.GetGroundHit(out WheelHit hit) && hit.force > 150f)
            {
                Vector3 velocity = wheel.attachedRigidbody.GetPointVelocity(hit.point);
                float roadSpeed = Mathf.Abs(Vector3.Dot(velocity, hit.forwardDir));
                float lateralSpeed = Mathf.Abs(Vector3.Dot(velocity, hit.sidewaysDir));
                float treadSpeed = Mathf.Abs(wheel.rpm) * wheel.radius * Mathf.PI / 30f;
                float overspeed = treadSpeed - roadSpeed;

                bool lowSpeed = roadSpeed < 11f;
                float longitudinal = i >= 2 && car.GetThrottle() > .55f && car.GetBrakePressure() < .1f &&
                    Mathf.Abs(hit.forwardSlip) > wheel.forwardFriction.extremumSlip * (lowSpeed ? .70f : 1.12f) &&
                    overspeed > (lowSpeed ? 1.5f : Mathf.Max(3.2f, roadSpeed * .15f))
                    ? Mathf.InverseLerp(1.5f, 6f, overspeed) : 0f;
                float lateral = Mathf.Abs(hit.sidewaysSlip) > wheel.sidewaysFriction.extremumSlip * 1.25f &&
                    lateralSpeed > Mathf.Max(2.3f, roadSpeed * .115f)
                    ? Mathf.InverseLerp(1.5f, 6.5f, lateralSpeed) : 0f;
                float locked = i >= 2 && car.IsHandbrakeActive() &&
                    roadSpeed - treadSpeed > Mathf.Max(2f, roadSpeed * .28f)
                    ? Mathf.InverseLerp(2f, 9f, roadSpeed - treadSpeed) : 0f;
                amount = Mathf.Max(longitudinal, Mathf.Max(lateral, locked));
                if (smoke != null) smoke.transform.position = hit.point + hit.normal * .12f;
            }

            slipTime[i] = amount > 0f ? slipTime[i] + Time.deltaTime : 0f;
            if (slipTime[i] < .035f) amount = 0f;
            strongestSlip = Mathf.Max(strongestSlip, amount);
            if (smoke == null) continue;
            var emission = smoke.emission;
            emission.rateOverTime = amount * 85f;
        }

        if (squeal == null) return;
        float smoothing = 1f - Mathf.Exp(-18f * Time.deltaTime);
        float perspective = cameraView != null && cameraView.CurrentView == CameraFollow.View.Cockpit ? .57f : 1f;
        squeal.volume = Mathf.Lerp(squeal.volume, strongestSlip * .62f * perspective, smoothing);
        squeal.pitch = Mathf.Lerp(squeal.pitch, Mathf.Lerp(.83f, 1.06f, strongestSlip), smoothing);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (texture != null) Destroy(texture);
    }
}
