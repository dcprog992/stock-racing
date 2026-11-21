using UnityEngine;

// Процедурный звук двигателя без аудиофайлов: генерирует "рычание" двигателя
// напрямую в коде, складывая несколько гармоник (упрощённая имитация пилообразной волны).
// Частота растёт вместе с оборотами (RPM) из CarController.
[RequireComponent(typeof(AudioSource))]
public class EngineAudio : MonoBehaviour
{
    [Tooltip("Ссылка на машину, чьи обороты озвучиваем")]
    public CarController car;

    [Header("Частоты")]
    [Tooltip("Частота звука на холостых оборотах, Гц")]
    public float idleFrequency = 45f;
    [Tooltip("Частота звука на максимальных оборотах, Гц")]
    public float maxFrequency = 220f;

    [Header("Звук")]
    [Range(0f, 1f)] public float volume = 0.4f;
    [Tooltip("Число гармоник — больше = 'богаче' и грубее звук двигателя, но чуть дороже по CPU")]
    [Range(1, 8)] public int harmonics = 4;
    [Tooltip("Немного шума добавляет 'рокот' выхлопа")]
    [Range(0f, 0.3f)] public float noiseAmount = 0.05f;

    private double phase = 0.0;
    private float sampleRate;
    private float smoothedFreq;
    private uint noiseState = 123456789u; // свой генератор шума — Random.value нельзя вызывать из аудио-потока

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        smoothedFreq = idleFrequency;
    }

    // Простой потокобезопасный псевдослучайный генератор (xorshift), безопасный для аудио-потока
    private float NextNoise()
    {
        noiseState ^= noiseState << 13;
        noiseState ^= noiseState >> 17;
        noiseState ^= noiseState << 5;
        return (noiseState / (float)uint.MaxValue) * 2f - 1f;
    }

    // Вызывается аудио-движком Unity напрямую из аудио-потока — здесь генерируем сэмплы сами
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (car == null) return;

        float rpm = car.GetRPM();
        float t = Mathf.InverseLerp(car.idleRPM, car.maxEngineRPM, rpm);
        float targetFreq = Mathf.Lerp(idleFrequency, maxFrequency, t);

        // Плавно подтягиваем частоту, чтобы не было "щелчков" при резких изменениях оборотов
        smoothedFreq = Mathf.Lerp(smoothedFreq, targetFreq, 0.05f);

        double increment = smoothedFreq * 2.0 * Mathf.PI / sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;

            // Складываем гармоники с затуханием — грубая имитация пилообразного "рычания" двигателя
            for (int h = 1; h <= harmonics; h++)
            {
                sample += Mathf.Sin((float)(phase * h)) / h;
            }

            // Немного шума для рокота выхлопа (свой генератор — Random.value тут запрещён движком)
            sample += NextNoise() * noiseAmount;

            sample *= volume * 0.5f;

            for (int c = 0; c < channels; c++)
                data[i + c] = sample;

            phase += increment;
            if (phase > 2.0 * Mathf.PI) phase -= 2.0 * Mathf.PI;
        }
    }
}