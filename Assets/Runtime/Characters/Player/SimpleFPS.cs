using UnityEngine;
using UnityEngine.InputSystem;
using ValVert.Core;

[RequireComponent(typeof(CharacterController))]
public sealed class SimpleFPS : MonoBehaviour
{
    [Header("Developer-only walking")]
    [Min(0f)] public float speed = 4f;
    [Min(0f)] public float runSpeed = 8f;
    [Min(0f)] public float mouseSens = 0.1f;
    public float gravity = -9.81f;
    [Min(0f)] public float jump = 1.2f;

    [Header("Developer-only flight")]
    [Min(0f)] public float flySpeed = 16f;
    [Min(0f)] public float fastFlySpeed = 64f;
    public Transform cam;

    CharacterController _controller;
    AppModeManager _modes;
    AppInputMode _returnMode = AppInputMode.None;
    float _pitch;
    float _verticalSpeed;
    bool _localDeveloperMode;
    bool _flying;

    public bool DeveloperModeActive => IsDeveloperModeActive;
    public bool IsFlying => _flying;
    static bool DeveloperControlsAllowed => Application.isEditor || Debug.isDebugBuild;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        // This is an exploration aid, not the shipped gameplay character.
        if (!DeveloperControlsAllowed) enabled = false;
    }

    void Start()
    {
        CacheAppModeManager();
        if (_modes == null) SetLocalCursor(false);
    }

    void Update()
    {
        if (!DeveloperControlsAllowed) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame) ToggleDeveloperMode();
        if (!IsDeveloperModeActive) return;

        if (keyboard.f2Key.wasPressedThisFrame) ToggleFlightMode();
        Look(Mouse.current);
        if (_flying) Fly(keyboard);
        else Walk(keyboard);
    }

    void OnDisable()
    {
        if (_flying) SetFlight(false);
        if (_modes != null && _modes.IsInitialized && _modes.CurrentMode == AppInputMode.Developer)
            _modes.SetMode(_returnMode);
        if (_modes == null && _localDeveloperMode) SetLocalCursor(false);
        _localDeveloperMode = false;
    }

    bool IsDeveloperModeActive
    {
        get
        {
            if (!DeveloperControlsAllowed) return false;
            if (_modes != null && _modes.IsInitialized)
                return _modes.CurrentMode == AppInputMode.Developer;
            return _localDeveloperMode;
        }
    }

    void CacheAppModeManager()
    {
        if (_modes == null) ValVertServices.TryGet(out _modes);
    }

    public void ToggleDeveloperMode()
    {
        if (!DeveloperControlsAllowed) return;
        CacheAppModeManager();
        if (_modes != null && _modes.IsInitialized)
        {
            if (_modes.CurrentMode == AppInputMode.Developer)
            {
                SetFlight(false);
                _modes.SetMode(_returnMode);
                Debug.Log("[SimpleFPS][DEV] Developer exploration disabled.");
                return;
            }

            // Never take control away from the main menu or a narrative cutscene.
            if (_modes.CurrentMode == AppInputMode.UI || _modes.CurrentMode == AppInputMode.Cutscene)
                return;

            _returnMode = _modes.CurrentMode;
            _modes.SetMode(AppInputMode.Developer);
            Debug.Log("[SimpleFPS][DEV] Developer exploration enabled.");
            return;
        }

        _localDeveloperMode = !_localDeveloperMode;
        if (!_localDeveloperMode) SetFlight(false);
        SetLocalCursor(_localDeveloperMode);
        Debug.Log(_localDeveloperMode
            ? "[SimpleFPS][DEV] Local developer exploration enabled."
            : "[SimpleFPS][DEV] Local developer exploration disabled.");
    }

    public void ToggleFlightMode()
    {
        if (!DeveloperControlsAllowed) return;
        if (!IsDeveloperModeActive) return;
        SetFlight(!_flying);
        Debug.Log(_flying ? "[SimpleFPS][DEV] Noclip flight enabled." : "[SimpleFPS][DEV] Walk mode restored.");
    }

    void SetFlight(bool enabled)
    {
        if (_flying == enabled) return;
        _flying = enabled;
        _verticalSpeed = 0f;
        if (_controller) _controller.enabled = !enabled;
    }

    void Look(Mouse mouse)
    {
        if (mouse == null) return;
        Vector2 delta = mouse.delta.ReadValue() * mouseSens;
        transform.Rotate(0f, delta.x, 0f);
        _pitch = Mathf.Clamp(_pitch - delta.y, -85f, 85f);
        if (cam) cam.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    void Walk(Keyboard keyboard)
    {
        Vector3 move = ReadPlanarMove(keyboard);
        float moveSpeed = keyboard.leftShiftKey.isPressed ? runSpeed : speed;
        if (_controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            _verticalSpeed = Mathf.Sqrt(jump * -2f * gravity);
        if (_controller.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -2f;
        _verticalSpeed += gravity * Time.deltaTime;
        _controller.Move((move * moveSpeed + Vector3.up * _verticalSpeed) * Time.deltaTime);
    }

    void Fly(Keyboard keyboard)
    {
        Vector3 move = transform.right * ReadAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed)
                     + (cam ? cam.forward : transform.forward) * ReadAxis(keyboard.wKey.isPressed, keyboard.sKey.isPressed)
                     + Vector3.up * ReadAxis(keyboard.spaceKey.isPressed, keyboard.leftCtrlKey.isPressed);
        float moveSpeed = keyboard.leftShiftKey.isPressed ? fastFlySpeed : flySpeed;
        if (move.sqrMagnitude > 1f) move.Normalize();
        transform.position += move * moveSpeed * Time.deltaTime;
    }

    Vector3 ReadPlanarMove(Keyboard keyboard)
    {
        Vector3 move = transform.forward * ReadAxis(keyboard.wKey.isPressed, keyboard.sKey.isPressed)
                     + transform.right * ReadAxis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
        move.y = 0f;
        return move.sqrMagnitude > 1f ? move.normalized : move;
    }

    static float ReadAxis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

    static void SetLocalCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnGUI()
    {
        if (!DeveloperControlsAllowed) return;
        if (!IsDeveloperModeActive) return;
        GUI.Label(new Rect(12f, 12f, 620f, 26f),
            _flying
                ? "DEV EXPLORER — WASD move · Mouse look · Space/Ctrl altitude · Shift fast · F2 walk · F1 exit"
                : "DEV EXPLORER — WASD walk · Mouse look · Space jump · Shift run · F2 fly · F1 exit");
    }
}
