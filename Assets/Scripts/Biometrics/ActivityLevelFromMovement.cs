using UnityEngine;
#if UNITY_2020_1_OR_NEWER
using UnityEngine.AI;
#endif

public class ActivityLevelFromMovement : MonoBehaviour
{
    [Header("Where to write the activity level")]
    public BioMetricsEstimator bio;

    [Header("Where to read movement from (optional)")]
    [Tooltip("If empty, it will use THIS transform position delta.")]
    public Transform playerTransform;

    [Tooltip("If your player has a Rigidbody, assign it for best velocity reading.")]
    public Rigidbody rb;

    [Tooltip("If your player uses CharacterController, assign it for velocity.")]
    public CharacterController cc;

#if UNITY_2020_1_OR_NEWER
    [Tooltip("If your player uses NavMeshAgent, assign it for velocity.")]
    public NavMeshAgent agent;
#endif

    [Header("Tuning")]
    [Tooltip("Speed (m/s) that should map to activityLevel = 1. Example: sprint speed.")]
    public float maxExpectedSpeed = 6f;

    [Tooltip("Below this speed we consider player 'still'.")]
    public float stillSpeedThreshold = 0.08f;

    [Tooltip("Seconds below threshold before we snap to 'still' (reduces flicker).")]
    public float stillGraceSeconds = 0.35f;

    [Tooltip("How quickly activity changes. Higher = faster response.")]
    public float smoothing = 6f;

    private Vector3 _lastPos;
    private float _stillTimer;

    void Start()
    {
        if (playerTransform == null) playerTransform = transform;
        _lastPos = playerTransform.position;
    }

    void Update()
    {
        if (bio == null || playerTransform == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float speed = ReadSpeed(dt);

        // still detection with grace time
        if (speed < stillSpeedThreshold) _stillTimer += dt;
        else _stillTimer = 0f;

        float targetActivity;
        if (_stillTimer >= stillGraceSeconds)
        {
            targetActivity = 0f;
        }
        else
        {
            // Normalize speed to 0..1
            targetActivity = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxExpectedSpeed));
        }

        // Smooth changes so it feels stable
        float alpha = 1f - Mathf.Exp(-smoothing * dt);
        bio.activityLevel = Mathf.Lerp(bio.activityLevel, targetActivity, alpha);
    }

    float ReadSpeed(float dt)
    {
        // Best sources first (more reliable than position delta)

        if (rb != null)
            return rb.linearVelocity.magnitude;

        if (cc != null)
            return cc.velocity.magnitude;

#if UNITY_2020_1_OR_NEWER
        if (agent != null)
            return agent.velocity.magnitude;
#endif

        // Fallback: position delta
        Vector3 pos = playerTransform.position;
        float speed = (pos - _lastPos).magnitude / dt;
        _lastPos = pos;
        return speed;
    }
}