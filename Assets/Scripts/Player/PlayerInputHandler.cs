using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KernelKnight.Player
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Gamepad Settings")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float analogDeadzone = 0.15f;

        // Input Values
        public Vector2 MoveInput { get; private set; }
        public float RawHorizontal { get; private set; }
        public float RawVertical { get; private set; }

        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool DashPressed { get; private set; }
        public bool ParryPressed { get; private set; }
        public bool HealHeld { get; private set; }

        public bool IsLookingUp => RawVertical > 0.5f;
        public bool IsHoldingDown => RawVertical < -0.5f;

        public enum ActiveDeviceType { KeyboardAndMouse, Gamepad }
        public ActiveDeviceType CurrentDevice { get; private set; } = ActiveDeviceType.KeyboardAndMouse;

        private void Update()
        {
            GatherInput();
        }

        private void GatherInput()
        {
            // Reset frame triggers
            JumpPressed = false;
            JumpHeld = false;
            AttackPressed = false;
            DashPressed = false;
            ParryPressed = false;
            HealHeld = false;
            RawHorizontal = 0f;
            RawVertical = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            bool kbmActive = false;
            bool padActive = false;

            // --- 1. KEYBOARD & MOUSE INPUT ---
            if (kb != null)
            {
                // Horizontal (A / D / Arrow Keys)
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) { RawHorizontal -= 1f; kbmActive = true; }
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) { RawHorizontal += 1f; kbmActive = true; }

                // Vertical (W / S / Arrow Keys)
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) { RawVertical += 1f; kbmActive = true; }
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) { RawVertical -= 1f; kbmActive = true; }

                // Jump: Space
                if (kb.spaceKey.wasPressedThisFrame) { JumpPressed = true; kbmActive = true; }
                if (kb.spaceKey.isPressed) { JumpHeld = true; kbmActive = true; }

                // Dash: Shift (Left or Right Shift)
                if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                {
                    DashPressed = true;
                    kbmActive = true;
                }

                // Parry: F
                if (kb.fKey.wasPressedThisFrame)
                {
                    ParryPressed = true;
                    kbmActive = true;
                }
                // Attack: Left Mouse Button OR Keyboard (J, X, Z, Enter)
                if (kb.jKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                {
                    AttackPressed = true;
                    kbmActive = true;
                }

                // Heal / Patching: Hold E or Q
                if (kb.eKey.isPressed || kb.qKey.isPressed)
                {
                    HealHeld = true;
                    kbmActive = true;
                }
            }

            if (mouse != null)
            {
                // Attack: Left Mouse Button
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    AttackPressed = true;
                    kbmActive = true;
                }
            }

            // --- 2. GAMEPAD INPUT (XBOX LAYOUT) ---
            if (pad != null)
            {
                // Left Stick Analog Input with Deadzone
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.magnitude < analogDeadzone)
                {
                    stick = Vector2.zero;
                }
                else
                {
                    padActive = true;
                }

                // D-Pad Alternative Input
                Vector2 dpad = pad.dpad.ReadValue();
                if (dpad.sqrMagnitude > 0.01f)
                {
                    padActive = true;
                }

                // Combine Gamepad directional movement
                float padH = Mathf.Abs(stick.x) > 0.01f ? stick.x : dpad.x;
                float padV = Mathf.Abs(stick.y) > 0.01f ? stick.y : dpad.y;

                if (Mathf.Abs(padH) > 0.01f || Mathf.Abs(padV) > 0.01f)
                {
                    RawHorizontal = padH;
                    RawVertical = padV;
                    padActive = true;
                }

                // Jump: A button (buttonSouth)
                if (pad.buttonSouth.wasPressedThisFrame) { JumpPressed = true; padActive = true; }
                if (pad.buttonSouth.isPressed) { JumpHeld = true; padActive = true; }

                // Attack: X button (buttonWest)
                if (pad.buttonWest.wasPressedThisFrame) { AttackPressed = true; padActive = true; }

                // Dash: B button (buttonEast)
                if (pad.buttonEast.wasPressedThisFrame) { DashPressed = true; padActive = true; }

                // Parry: LB (Left Bumper / leftShoulder) - Specifically required!
                if (pad.leftShoulder.wasPressedThisFrame) { ParryPressed = true; padActive = true; }

                // Heal / Patching: Hold RB (Right Bumper / rightShoulder)
                if (pad.rightShoulder.isPressed) { HealHeld = true; padActive = true; }
            }

            // Device switching tracking
            if (padActive) CurrentDevice = ActiveDeviceType.Gamepad;
            else if (kbmActive) CurrentDevice = ActiveDeviceType.KeyboardAndMouse;

#elif ENABLE_LEGACY_INPUT_MANAGER
            RawHorizontal = Input.GetAxisRaw("Horizontal");
            RawVertical = Input.GetAxisRaw("Vertical");

            JumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump");
            JumpHeld = Input.GetKey(KeyCode.Space) || Input.GetButton("Jump");

            AttackPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J);
            DashPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            ParryPressed = Input.GetKeyDown(KeyCode.F);
            HealHeld = Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Q);
#endif

            MoveInput = new Vector2(RawHorizontal, RawVertical);
        }
    }
}
