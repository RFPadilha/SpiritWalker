using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SoulTether : MonoBehaviour
{
    [SerializeField] int pointCount = 20;
    [SerializeField] float noiseAmplitude = 0.5f;
    [SerializeField] float noiseFrequency = 2f;
    [SerializeField] float noiseScrollSpeed = 3f;

    private LineRenderer lr;
    private Transform endpointA;
    private Transform endpointB;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = pointCount;
        lr.useWorldSpace = true;
        gameObject.SetActive(false);
    }

    public void Activate(Transform a, Transform b)
    {
        endpointA = a;
        endpointB = b;
        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (endpointA == null || endpointB == null) return;

        Vector3 start = endpointA.position;
        Vector3 end = endpointB.position;
        Vector3 forward = end - start;
        float length = forward.magnitude;

        if (length < 0.001f)
        {
            for (int i = 0; i < pointCount; i++)
                lr.SetPosition(i, start);
            return;
        }

        forward /= length;

        // Build a perpendicular basis for displacement
        Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(forward, Vector3.right).normalized;
        Vector3 up = Vector3.Cross(right, forward).normalized;

        float time = Time.unscaledTime * noiseScrollSpeed;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 basePos = Vector3.Lerp(start, end, t);

            // Sine envelope tapers noise to zero at both endpoints
            float envelope = Mathf.Sin(t * Mathf.PI);

            float noiseX = (Mathf.PerlinNoise(t * noiseFrequency + time, 0.0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0.0f, t * noiseFrequency + time + 31.7f) - 0.5f) * 2f;

            Vector3 offset = (right * noiseX + up * noiseY) * noiseAmplitude * envelope;
            lr.SetPosition(i, basePos + offset);
        }
    }
}
