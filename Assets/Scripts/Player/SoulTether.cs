using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SoulTether : MonoBehaviour
{
    [Tooltip("Minimum distance the moving object must travel before a new waypoint is recorded")]
    [SerializeField] float sampleDistance = 0.3f;
    [Tooltip("Local-space offset applied to each endpoint (e.g. Y=1.4 for neck height)")]
    [SerializeField] Vector3 endpointOffset = new Vector3(0f, 1.4f, 0f);

    [Header("Noise")]
    [SerializeField] float noiseAmplitude = 0.3f;
    [SerializeField] float noiseFrequency = 2f;
    [SerializeField] float noiseScrollSpeed = 3f;

    /// <summary>
    /// The recorded path in world space (without endpoint offset).
    /// First element is the anchor point, last is the most recent position of the mover.
    /// </summary>
    public IReadOnlyList<Vector3> Path => path;

    private LineRenderer lr;
    private readonly List<Vector3> path = new List<Vector3>();
    private Transform anchor;
    private Transform mover;
    private float sqrSampleDistance;
    private bool isTraversing;
    private int traversalHeadIndex;
    private Vector3 traversalHeadPosition;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Begin recording. The anchor is the stationary object; the mover is the one being controlled.
    /// The path starts at the anchor and grows as the mover travels.
    /// </summary>
    public void Activate(Transform anchorTransform, Transform moverTransform)
    {
        anchor = anchorTransform;
        mover = moverTransform;
        sqrSampleDistance = sampleDistance * sampleDistance;
        isTraversing = false;

        path.Clear();
        path.Add(anchor.position);
        path.Add(mover.position);

        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        isTraversing = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Switch from recording mode to traversal mode.
    /// The line will shrink as the traversing object progresses along the path.
    /// </summary>
    public void BeginTraversal(bool reversePath)
    {
        if (reversePath)
            path.Reverse();
        isTraversing = true;
    }

    /// <summary>
    /// Update the visual during traversal. Call each frame with the current
    /// waypoint index and the traversing object's position.
    /// The line renders from the current position to the end of the path.
    /// </summary>
    public void UpdateTraversalHead(int waypointIndex, Vector3 currentPosition)
    {
        traversalHeadIndex = waypointIndex;
        traversalHeadPosition = currentPosition;
    }

    private void Update()
    {
        if (isTraversing)
        {
            RenderTraversalLine();
            return;
        }

        if (anchor == null || mover == null) return;

        RecordWaypoint();
        RenderLine();
    }

    private void RecordWaypoint()
    {
        Vector3 currentPos = mover.position;

        // Always keep the last point tracking the mover live
        // Only insert a new waypoint when the mover has moved far enough from the second-to-last point
        if (path.Count >= 2)
        {
            Vector3 lastRecorded = path[path.Count - 2];
            if ((currentPos - lastRecorded).sqrMagnitude >= sqrSampleDistance)
            {
                // Commit the previous live point as a fixed waypoint, then add a new live point
                path.Add(currentPos);
            }
            else
            {
                // Just slide the live tail to the current position
                path[path.Count - 1] = currentPos;
            }
        }
    }

    private void RenderLine()
    {
        int count = path.Count;
        lr.positionCount = count;

        float time = Time.unscaledTime * noiseScrollSpeed;

        for (int i = 0; i < count; i++)
        {
            Vector3 basePos = path[i] + endpointOffset;

            // Taper noise at both ends so the line connects cleanly to the characters
            float t = count > 1 ? i / (float)(count - 1) : 0f;
            float envelope = Mathf.Sin(t * Mathf.PI);

            float noiseX = (Mathf.PerlinNoise(t * noiseFrequency + time, 0.0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0.0f, t * noiseFrequency + time + 31.7f) - 0.5f) * 2f;

            Vector3 offset = new Vector3(noiseX, noiseY, 0f) * noiseAmplitude * envelope;
            lr.SetPosition(i, basePos + offset);
        }
    }

    private void RenderTraversalLine()
    {
        // Show only the remaining path: current body position → end of path
        int remaining = path.Count - traversalHeadIndex;
        if (remaining <= 0)
        {
            lr.positionCount = 0;
            return;
        }

        int totalPoints = remaining + 1; // +1 for the interpolated head position
        lr.positionCount = totalPoints;

        float time = Time.unscaledTime * noiseScrollSpeed;

        // First point: the traversing object's current position (no noise — clean attachment)
        lr.SetPosition(0, traversalHeadPosition + endpointOffset);

        for (int i = 0; i < remaining; i++)
        {
            Vector3 basePos = path[traversalHeadIndex + i] + endpointOffset;

            float t = totalPoints > 1 ? (i + 1) / (float)(totalPoints - 1) : 0f;
            float envelope = Mathf.Sin(t * Mathf.PI);

            float noiseX = (Mathf.PerlinNoise(t * noiseFrequency + time, 0.0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0.0f, t * noiseFrequency + time + 31.7f) - 0.5f) * 2f;

            Vector3 offset = new Vector3(noiseX, noiseY, 0f) * noiseAmplitude * envelope;
            lr.SetPosition(i + 1, basePos + offset);
        }
    }
}
