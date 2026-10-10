
using System.Collections;
using UnityEngine;

public class ScreenShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [Min(0f)]
    public float duration = 0.4f;

    [Min(0f)]
    public float intensity = 0.2f;

    [Min(0f)]
    public float frequency = 25f;

    [Header("Falloff")]
    public AnimationCurve falloff = AnimationCurve.EaseInOut(
        0f, 1f,
        1f, 0f
    );

    [Header("Options")]
    public bool useUnscaledTime = false;

    private void OnEnable()
    {
        CameraShakeManager.Shake(
            duration,
            intensity,
            frequency,
            falloff,
            useUnscaledTime
        );
    }
}

public class CameraShakeManager : MonoBehaviour
{
    private static CameraShakeManager instance;

    private Transform cameraTransform;
    private Coroutine shakeCoroutine;
    private Vector3 currentOffset;

    public static void Shake(
        float duration,
        float intensity,
        float frequency,
        AnimationCurve falloff,
        bool unscaledTime)
    {
        if (instance == null)
        {
            GameObject manager = new GameObject(
                "Camera Shake Manager"
            );

            instance = manager.AddComponent<CameraShakeManager>();
        }

        instance.StartShake(
            duration,
            intensity,
            frequency,
            falloff,
            unscaledTime
        );
    }

    private void StartShake(
        float duration,
        float intensity,
        float frequency,
        AnimationCurve falloff,
        bool unscaledTime)
    {
        Camera cam = Camera.main;

        if (cam == null)
            return;

        if (cameraTransform != null)
            cameraTransform.localPosition -= currentOffset;

        cameraTransform = cam.transform;
        currentOffset = Vector3.zero;

        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        if (duration <= 0f || intensity <= 0f)
        {
            shakeCoroutine = null;
            return;
        }

        shakeCoroutine = StartCoroutine(
            ShakeRoutine(
                duration,
                intensity,
                frequency,
                falloff,
                unscaledTime
            )
        );
    }

    private IEnumerator ShakeRoutine(
        float duration,
        float intensity,
        float frequency,
        AnimationCurve falloff,
        bool unscaledTime)
    {
        float elapsed = 0f;
        float seedX = Random.Range(0f, 1000f);
        float seedY = Random.Range(0f, 1000f);

        while (elapsed < duration)
        {
            // Remove previous shake offset.
            cameraTransform.localPosition -= currentOffset;

            float normalizedTime = Mathf.Clamp01(
                elapsed / duration
            );

            float strength = intensity *
                falloff.Evaluate(normalizedTime);

            float noiseTime = elapsed * frequency;

            float x = Mathf.PerlinNoise(
                seedX + noiseTime, 0f
            ) * 2f - 1f;

            float y = Mathf.PerlinNoise(
                0f, seedY + noiseTime
            ) * 2f - 1f;

            currentOffset = new Vector3(
                x * strength,
                y * strength,
                0f
            );

            cameraTransform.localPosition += currentOffset;

            elapsed += unscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            yield return null;
        }

        cameraTransform.localPosition -= currentOffset;
        currentOffset = Vector3.zero;
        shakeCoroutine = null;
    }

    private void OnDisable()
    {
        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        if (cameraTransform != null)
            cameraTransform.localPosition -= currentOffset;

        currentOffset = Vector3.zero;
        shakeCoroutine = null;
    }
}
