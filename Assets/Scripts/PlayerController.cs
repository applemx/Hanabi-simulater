using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 5.5f;
    public float sprintMultiplier = 1.7f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;

    [Header("Look")]
    public Transform cameraRoot;          // Main Camera
    public float mouseSensitivity = 2.0f;
    public float pitchMin = -80f;
    public float pitchMax = 80f;

    CharacterController cc;
    float yaw;
    float pitch;
    float verticalVel;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraRoot == null) cameraRoot = Camera.main.transform;
    }

    void Start()
    {
        // 今の見た目の角度から開始する
        yaw = transform.eulerAngles.y;

        if (cameraRoot != null)
        {
            float x = cameraRoot.localEulerAngles.x;
            if (x > 180f) x -= 360f;   // 0..360 を -180..180 に戻す
            pitch = Mathf.Clamp(x, pitchMin, pitchMax);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else
        {
            pitch = 0f;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // ESCで解除、クリックで再ロック
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Look();
        Move();
    }

    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = (transform.right * h + transform.forward * v);
        move = Vector3.ClampMagnitude(move, 1f);

        float spd = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        // 接地判定
        if (cc.isGrounded && verticalVel < 0f) verticalVel = -2f;

        // ジャンプ（任意：いらなければ消してOK）
        if (cc.isGrounded && Input.GetKeyDown(KeyCode.Space))
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // 重力
        verticalVel += gravity * Time.deltaTime;

        Vector3 velocity = move * spd + Vector3.up * verticalVel;

        cc.Move(velocity * Time.deltaTime);
    }
}
