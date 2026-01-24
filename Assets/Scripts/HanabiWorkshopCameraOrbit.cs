using UnityEngine;

public class HanabiWorkshopCameraOrbit : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float distance = 2.8f;
    [SerializeField] float yaw = 0f;
    [SerializeField] float pitch = 35f;
    [SerializeField] float rotateSpeed = 120f;
    [SerializeField] float zoomSpeed = 1.0f;
    [SerializeField] bool topDown = true;
    [SerializeField, Range(45f, 89f)] float topDownPitch = 85f;
    [SerializeField] KeyCode toggleTopDownKey = KeyCode.T;
    [SerializeField] bool inputEnabled = true;
    [SerializeField] bool useFixedAngles = false;
    [SerializeField] float fixedYaw = -35f;
    [SerializeField] float fixedPitch = 35f;
    [SerializeField] bool applyViewport = false;
    [SerializeField] Rect viewport = new Rect(0.7f, 0.04f, 0.28f, 0.28f);

    Camera cachedCamera;

    void Awake()
    {
        cachedCamera = GetComponent<Camera>();
        if (applyViewport && cachedCamera != null)
            cachedCamera.rect = viewport;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (applyViewport && cachedCamera != null)
            cachedCamera.rect = viewport;

        if (inputEnabled)
        {
            if (Input.GetKeyDown(toggleTopDownKey))
                topDown = !topDown;

            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * rotateSpeed * Time.deltaTime;
                if (!topDown)
                {
                    pitch -= Input.GetAxis("Mouse Y") * rotateSpeed * Time.deltaTime;
                    pitch = Mathf.Clamp(pitch, 5f, 85f);
                }
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, 1.2f, 6f);
            }
        }

        if (useFixedAngles)
        {
            yaw = fixedYaw;
            pitch = fixedPitch;
        }
        else if (topDown)
        {
            pitch = topDownPitch;
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = target.position + rot * (Vector3.back * distance);
        transform.position = pos;
        transform.LookAt(target.position, Vector3.up);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetTopDown(bool enabled)
    {
        topDown = enabled;
        useFixedAngles = false;
    }

    public bool IsTopDown()
    {
        return topDown;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void Configure(bool inputEnabled, bool topDownEnabled, bool useFixedAngles, float fixedYaw, float fixedPitch, float distanceValue, bool applyViewport, Rect viewport)
    {
        this.inputEnabled = inputEnabled;
        this.topDown = topDownEnabled;
        this.useFixedAngles = useFixedAngles;
        this.fixedYaw = fixedYaw;
        this.fixedPitch = fixedPitch;
        this.distance = distanceValue;
        this.applyViewport = applyViewport;
        this.viewport = viewport;
        if (applyViewport && cachedCamera != null)
            cachedCamera.rect = viewport;
    }
}
