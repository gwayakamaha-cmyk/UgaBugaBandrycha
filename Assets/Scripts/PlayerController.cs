using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// First-person controller: WASD, Shift sprint with stamina, Space jump, mouse look, F flashlight.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float jumpHeight = 1.1f;
    public float gravity = -20f;

    [Header("Look")]
    public Transform cameraPivot;
    public float mouseSensitivity = 0.08f;
    public float maxPitch = 85f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float drainPerSecond = 25f;
    public float regenPerSecond = 15f;
    [Tooltip("Pause after sprinting before stamina starts to refill.")]
    public float regenDelay = 0.8f;
    [Tooltip("After running dry, sprint unlocks again only once stamina is back to this value.")]
    public float sprintUnlockStamina = 25f;
    public Image staminaFill;
    public Color staminaColor = new Color(0.85f, 0.85f, 0.75f, 0.9f);
    public Color exhaustedColor = new Color(0.8f, 0.15f, 0.1f, 0.9f);

    [Header("Flashlight")]
    public Light flashlight;

    public bool IsLocked { get; private set; }

    CharacterController controller;
    Vector3 velocity;
    float yaw, pitch;
    float stamina;
    float regenTimer;
    bool exhausted;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraPivot == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraPivot = cam.transform;
        }
        stamina = maxStamina;
        yaw = transform.eulerAngles.y;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UpdateStaminaUI();
    }

    void Update()
    {
        if (IsLocked) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        float dt = Time.deltaTime;

        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if (kb == null) return;

        Vector2 input = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
        if (input.sqrMagnitude > 1f) input.Normalize();
        bool moving = input.sqrMagnitude > 0.01f;

        if (exhausted && stamina >= sprintUnlockStamina) exhausted = false;
        bool sprinting = kb.leftShiftKey.isPressed && moving && !exhausted && stamina > 0f;

        if (sprinting)
        {
            stamina -= drainPerSecond * dt;
            regenTimer = regenDelay;
            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true;
            }
        }
        else
        {
            regenTimer -= dt;
            if (regenTimer <= 0f) stamina = Mathf.Min(maxStamina, stamina + regenPerSecond * dt);
        }

        Vector3 move = (transform.right * input.x + transform.forward * input.y) * (sprinting ? sprintSpeed : walkSpeed);

        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        if (controller.isGrounded && kb.spaceKey.wasPressedThisFrame)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        velocity.y += gravity * dt;

        controller.Move((move + Vector3.up * velocity.y) * dt);

        if (kb.fKey.wasPressedThisFrame && flashlight != null)
            flashlight.enabled = !flashlight.enabled;

        UpdateStaminaUI();
    }

    void UpdateStaminaUI()
    {
        if (staminaFill == null) return;
        staminaFill.fillAmount = stamina / maxStamina;
        staminaFill.color = exhausted ? exhaustedColor : staminaColor;
    }

    /// <summary>Locks all input and snaps the camera onto <paramref name="target"/>.</summary>
    public void Caught(Transform target)
    {
        if (IsLocked) return;
        IsLocked = true;
        if (flashlight != null) flashlight.enabled = true;
        StartCoroutine(StareAt(target));
    }

    IEnumerator StareAt(Transform target)
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            Vector3 dir = target.position - cameraPivot.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var flat = new Vector3(dir.x, 0f, dir.z);
                if (flat.sqrMagnitude > 0.0001f)
                {
                    var bodyRot = Quaternion.LookRotation(flat);
                    transform.rotation = Quaternion.Slerp(transform.rotation, bodyRot, 25f * Time.deltaTime);
                }
                var camRot = Quaternion.LookRotation(dir);
                var shake = Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), Random.Range(-2f, 2f)) ;
                cameraPivot.rotation = Quaternion.Slerp(cameraPivot.rotation, camRot, 25f * Time.deltaTime) * (t < 1.5f ? shake : Quaternion.identity);
            }
            yield return null;
        }
    }
}
