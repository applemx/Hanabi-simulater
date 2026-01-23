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

    void LateUpdate()
    {
        if (target == null) return;

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

        if (topDown)
            pitch = topDownPitch;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = target.position + rot * (Vector3.back * distance);
        transform.position = pos;
        transform.LookAt(target.position, Vector3.up);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
