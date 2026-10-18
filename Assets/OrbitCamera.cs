using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                  // point à regarder (optionnel)
    public Vector3 targetPosition = Vector3.zero; // si target == null

    [Header("Orbit")]
    public float distance = 150f;
    public float minDistance = 10f;
    public float maxDistance = 800f;

    public float xSpeed = 120f;   // rotation horizontale
    public float ySpeed = 80f;    // rotation verticale
    public float yMinLimit = -10f;
    public float yMaxLimit = 85f;

    [Header("Zoom")]
    public float zoomSpeed = 200f;

    [Header("Pan (Right mouse)")]
    public float panSpeed = 0.5f;

    [Header("Smoothing")]
    public bool smooth = true;
    public float smoothTime = 0.08f;

    float yaw;
    float pitch;

    Vector3 currentTargetPos;
    Vector3 targetPosVelocity;

    void Start()
    {
        // Initial angles from current camera orientation
        var angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        currentTargetPos = (target != null) ? target.position : targetPosition;

        // Prevent rigidbody interference
        if (TryGetComponent<Rigidbody>(out var rb))
            rb.freezeRotation = true;
    }

    void Update()
    {
        // --- Zoom with mouse wheel ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        // --- Orbit with left mouse drag ---
        if (Input.GetMouseButton(0))
        {
            yaw += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            pitch = ClampAngle(pitch, yMinLimit, yMaxLimit);
        }

        // --- Pan with right mouse drag (optional) ---
        if (Input.GetMouseButton(1))
        {
            // Move target in camera plane
            float dx = -Input.GetAxis("Mouse X") * panSpeed;
            float dy = -Input.GetAxis("Mouse Y") * panSpeed;

            Vector3 right = transform.right;
            Vector3 up = transform.up;
            currentTargetPos += right * dx + up * dy;

            // If target exists, you can also move it:
            // if (target != null) target.position = currentTargetPos;
        }

        // Update target position (follow transform if provided)
        Vector3 desiredTarget = (target != null) ? target.position : currentTargetPos;

        if (smooth)
            currentTargetPos = Vector3.SmoothDamp(currentTargetPos, desiredTarget, ref targetPosVelocity, smoothTime);
        else
            currentTargetPos = desiredTarget;

        // Compute camera position
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 offset = rotation * new Vector3(0, 0, -distance);
        transform.position = currentTargetPos + offset;

        // Look at target
        transform.rotation = rotation;
    }

    static float ClampAngle(float angle, float min, float max)
    {
        // Normalize to [-180, 180]
        while (angle < -180f) angle += 360f;
        while (angle > 180f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}
