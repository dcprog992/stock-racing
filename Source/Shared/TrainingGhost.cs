using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class TrainingGhost : IDisposable
{
    private const int FileVersion = 1;
    private const int MaxSamples = 18001;
    private const float SampleInterval = .1f;
    private const int SampleBytes = 8 * sizeof(float);

    private readonly struct Sample
    {
        public readonly float Time;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public Sample(float time, Vector3 position, Quaternion rotation)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
        }
    }

    private readonly string path;
    private readonly CarController car;
    private readonly GameObject visual;
    private readonly Material material;
    private readonly List<Sample> recording = new();
    private Sample[] best;
    private Sample[] playback;
    private int cursor;
    private float nextSample;

    public float BestTime => best != null ? best[best.Length - 1].Time : 0f;

    public TrainingGhost(CarController car, string path)
    {
        this.car = car;
        this.path = path;
        best = Load();
        (visual, material) = CreateVisual(car.transform);
    }

    public void BeginRace(Vector3 position, Quaternion rotation)
    {
        recording.Clear();
        recording.Add(new Sample(0f, position, rotation));
        nextSample = SampleInterval;
        playback = best;
        cursor = 0;
        UpdateVisual(0f);
    }

    public void Record(float elapsed, Vector3 position, Quaternion rotation)
    {
        if (recording.Count >= MaxSamples - 1 || elapsed < nextSample) return;

        recording.Add(new Sample(elapsed, position, rotation));
        nextSample = elapsed + SampleInterval;
    }

    public bool FinishRace(float elapsed, Vector3 position, Quaternion rotation)
    {
        if (recording.Count < 2 || recording.Count >= MaxSamples - 1 ||
            elapsed <= recording[recording.Count - 1].Time || (best != null && elapsed >= BestTime))
            return false;

        recording.Add(new Sample(elapsed, position, rotation));
        best = recording.ToArray();
        Save();
        return true;
    }

    public void UpdateVisual(float elapsed)
    {
        if (visual == null) return;
        bool visible = playback != null && elapsed <= playback[playback.Length - 1].Time;
        if (visual.activeSelf != visible) visual.SetActive(visible);
        if (!visible) return;

        while (cursor + 1 < playback.Length - 1 && playback[cursor + 1].Time < elapsed) cursor++;
        Sample from = playback[cursor];
        Sample to = playback[cursor + 1];
        float t = Mathf.InverseLerp(from.Time, to.Time, elapsed);
        Vector3 position = Vector3.Lerp(from.Position, to.Position, t);
        material.color = new Color(.19f, .84f, 1f, .19f);
        visual.transform.SetPositionAndRotation(position,
            Quaternion.Slerp(from.Rotation, to.Rotation, t));
    }

    private Sample[] Load()
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadInt32() != FileVersion) return null;
            int count = reader.ReadInt32();
            if (count < 3 || count > MaxSamples || reader.BaseStream.Length != 8L + SampleBytes * count)
                return null;

            var samples = new Sample[count];
            float previous = -1f;
            for (int i = 0; i < count; i++)
            {
                float time = reader.ReadSingle();
                var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                float magnitude = Quaternion.Dot(rotation, rotation);
                if (!float.IsFinite(time) || time <= previous || (i == 0 && time != 0f) ||
                    !float.IsFinite(position.sqrMagnitude) || !float.IsFinite(magnitude) || magnitude < .9f || magnitude > 1.1f)
                    return null;

                samples[i] = new Sample(time, position, rotation);
                previous = time;
            }
            return samples;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            Debug.LogWarning("Could not load the practice ghost: " + error.Message, car);
            return null;
        }
    }

    private void Save()
    {
        string temporary = path + ".tmp";
        try
        {
            using (var writer = new BinaryWriter(File.Create(temporary)))
            {
                writer.Write(FileVersion);
                writer.Write(best.Length);
                foreach (var sample in best)
                {
                    writer.Write(sample.Time);
                    writer.Write(sample.Position.x);
                    writer.Write(sample.Position.y);
                    writer.Write(sample.Position.z);
                    writer.Write(sample.Rotation.x);
                    writer.Write(sample.Rotation.y);
                    writer.Write(sample.Rotation.z);
                    writer.Write(sample.Rotation.w);
                }
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            Debug.LogWarning("Could not save the practice ghost: " + error.Message, car);
        }
    }

    private static (GameObject, Material) CreateVisual(Transform source)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) return (null, null);

        var material = new Material(shader) { name = "Training ghost", color = new Color(.19f, .84f, 1f, .19f) };
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_Mode", 2);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(.03f, .28f, .38f));
        material.renderQueue = (int)RenderQueue.Transparent;

        var root = new GameObject("Призрак лучшего круга");
        root.SetActive(false);
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out var original) || !original.enabled) continue;

            var part = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
            Transform pose = filter.transform;
            part.transform.SetParent(root.transform, false);
            part.transform.SetLocalPositionAndRotation(source.InverseTransformPoint(pose.position),
                Quaternion.Inverse(source.rotation) * pose.rotation);
            part.transform.localScale = pose.lossyScale;
            part.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = part.GetComponent<MeshRenderer>();
            var materials = new Material[filter.sharedMesh.subMeshCount];
            Array.Fill(materials, material);
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return (root, material);
    }

    public void HideVisual()
    {
        if (visual != null) visual.SetActive(false);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(visual);
        UnityEngine.Object.Destroy(material);
    }
}
