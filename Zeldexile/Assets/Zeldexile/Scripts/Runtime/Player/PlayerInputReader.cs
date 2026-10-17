using UnityEngine;
using UnityEngine.InputSystem;

namespace Zeldexile.Player
{
    /// <summary>
    /// Polls keyboard/mouse and gamepad. Kept in one place so the rest of the code never touches devices.
    /// Layout follows the reference game: jump, run, attack, throw, bow, rune, guard/lock and interact.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Sprint { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool ThrowPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        /// <summary>Cast the selected rune.</summary>
        public bool RunePressed { get; private set; }
        /// <summary>Right button / right trigger held: bow aim (with a bow, after a short hold); in the air this also slows time.</summary>
        public bool BowAimHeld { get; private set; }
        /// <summary>Right button / right trigger pressed and released this frame: a tap is the heavy sword attack.</summary>
        public bool HeavyPressed { get; private set; }
        public bool HeavyReleased { get; private set; }
        /// <summary>Shield up (and target lock while held).</summary>
        public bool GuardHeld { get; private set; }
        public bool LockPressed { get; private set; }
        public bool DodgePressed { get; private set; }
        /// <summary>Crouch toggle: left Ctrl, or the left stick pressed in.</summary>
        public bool CrouchPressed { get; private set; }
        public bool MenuPressed { get; private set; }
        public int SkillSlotPressed { get; private set; } = -1;
        /// <summary>Camera zoom: positive = closer, negative = farther (mouse wheel notches, or right stick click held).</summary>
        public float Zoom { get; private set; }

        /// <summary>While a menu is open, gameplay input is suppressed.</summary>
        public bool GameplayBlocked { get; set; }

        public bool FocusHeld => BowAimHeld;

        /// <summary>Build checks (BuildCapture "-zxwalk"): a stick held by script instead of the devices.</summary>
        public static Vector2? ScriptedMove;
        public static bool ScriptedSprint;
        /// <summary>One-frame scripted right-button press and release (consumed when read).</summary>
        public static bool ScriptedHeavyPress, ScriptedHeavyRelease;
        /// <summary>Scripted camera look, added every frame while set.</summary>
        public static Vector2 ScriptedLook;

        private float _guardHeldSince = -1f;
        private bool _guardTapConsumed;

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            var move = Vector2.zero;
            var look = Vector2.zero;
            var zoom = 0f;
            bool sprint = false, jump = false, attack = false, throwIt = false, interact = false, rune = false;
            bool bowAim = false, guardDown = false, guardReleased = false, dodge = false, menu = false, crouch = false;
            bool heavyDown = false, heavyUp = false;
            SkillSlotPressed = -1;

            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                sprint |= kb.leftShiftKey.isPressed;
                jump |= kb.spaceKey.wasPressedThisFrame;
                throwIt |= kb.fKey.wasPressedThisFrame;
                interact |= kb.eKey.wasPressedThisFrame;
                rune |= kb.rKey.wasPressedThisFrame;
                menu |= kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame;
                if (kb.digit1Key.wasPressedThisFrame) SkillSlotPressed = 0;
                if (kb.digit2Key.wasPressedThisFrame) SkillSlotPressed = 1;
                if (kb.digit3Key.wasPressedThisFrame) SkillSlotPressed = 2;
                if (kb.digit4Key.wasPressedThisFrame) SkillSlotPressed = 3;
                guardDown |= kb.qKey.isPressed;
                guardReleased |= kb.qKey.wasReleasedThisFrame;
                dodge |= kb.leftAltKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;
                crouch |= kb.leftCtrlKey.wasPressedThisFrame;
            }

            if (mouse != null)
            {
                look += mouse.delta.ReadValue() * 0.1f;
                // One wheel notch reads 120 on Windows; normalise to 1 per notch.
                zoom += mouse.scroll.ReadValue().y / 120f;
                attack |= mouse.leftButton.wasPressedThisFrame;
                bowAim |= mouse.rightButton.isPressed;
                heavyDown |= mouse.rightButton.wasPressedThisFrame;
                heavyUp |= mouse.rightButton.wasReleasedThisFrame;
            }

            if (pad != null)
            {
                move += pad.leftStick.ReadValue();
                var stick = pad.rightStick.ReadValue();
                // Right stick pressed in: vertical stick zooms instead of tilting the camera.
                if (pad.rightStickButton.isPressed)
                {
                    zoom += stick.y * Time.unscaledDeltaTime * 4f;
                    stick.y = 0f;
                }

                look += stick * 3f;
                sprint |= pad.buttonEast.isPressed;
                jump |= pad.buttonNorth.wasPressedThisFrame;
                interact |= pad.buttonSouth.wasPressedThisFrame;
                attack |= pad.buttonWest.wasPressedThisFrame;
                throwIt |= pad.rightShoulder.wasPressedThisFrame;
                bowAim |= pad.rightTrigger.isPressed;
                heavyDown |= pad.rightTrigger.wasPressedThisFrame;
                heavyUp |= pad.rightTrigger.wasReleasedThisFrame;
                rune |= pad.leftTrigger.wasPressedThisFrame;
                guardDown |= pad.leftShoulder.isPressed;
                guardReleased |= pad.leftShoulder.wasReleasedThisFrame;
                menu |= pad.startButton.wasPressedThisFrame;
                crouch |= pad.leftStickButton.wasPressedThisFrame;
                if (pad.dpad.right.wasPressedThisFrame) SkillSlotPressed = 0;
                if (pad.dpad.up.wasPressedThisFrame) SkillSlotPressed = 1;
                if (pad.dpad.left.wasPressedThisFrame) SkillSlotPressed = 2;
                if (pad.dpad.down.wasPressedThisFrame) SkillSlotPressed = 3;
            }

            // Guard button: a quick tap toggles the lock, holding it raises the shield (and locks on).
            var lockOn = false;
            if (guardDown)
            {
                if (_guardHeldSince < 0f)
                {
                    _guardHeldSince = Time.unscaledTime;
                    _guardTapConsumed = false;
                }
            }

            if (guardReleased)
            {
                if (_guardHeldSince >= 0f && Time.unscaledTime - _guardHeldSince < 0.22f && !_guardTapConsumed) lockOn = true;
                _guardHeldSince = -1f;
            }

            var guardHeld = guardDown && _guardHeldSince >= 0f && Time.unscaledTime - _guardHeldSince >= 0.22f;

            MenuPressed = menu;
            if (GameplayBlocked)
            {
                Move = Vector2.zero;
                Look = Vector2.zero;
                Zoom = 0f;
                Sprint = JumpPressed = AttackPressed = ThrowPressed = InteractPressed = RunePressed = false;
                BowAimHeld = GuardHeld = LockPressed = DodgePressed = CrouchPressed = HeavyPressed = HeavyReleased = false;
                SkillSlotPressed = -1;
                return;
            }

            CrouchPressed = crouch;
            if (ScriptedMove.HasValue) move = ScriptedMove.Value;
            sprint |= ScriptedSprint;
            look += ScriptedLook;
            heavyDown |= ScriptedHeavyPress;
            heavyUp |= ScriptedHeavyRelease;
            ScriptedHeavyPress = ScriptedHeavyRelease = false;

            Move = Vector2.ClampMagnitude(move, 1f);
            Look = look;
            Zoom = zoom;
            Sprint = sprint;
            JumpPressed = jump;
            AttackPressed = attack;
            ThrowPressed = throwIt;
            InteractPressed = interact;
            RunePressed = rune;
            BowAimHeld = bowAim;
            HeavyPressed = heavyDown;
            HeavyReleased = heavyUp;
            GuardHeld = guardHeld;
            LockPressed = lockOn;
            DodgePressed = dodge;
        }
    }
}
