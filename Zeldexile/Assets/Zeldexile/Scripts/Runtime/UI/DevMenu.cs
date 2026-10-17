using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Zeldexile.Characters;
using Zeldexile.Player;

namespace Zeldexile.UI
{
    /// <summary>
    /// Developer menu, opened and closed with the apostrophe key (the key left of 1 on an ABNT2 keyboard; the
    /// quote key works too). For now it lists every animation of the hero to try them one at a time:
    /// <list type="bullet">
    /// <item>captured clips (<see cref="MotionClip"/>), played as they are, looping, in place;</item>
    /// <item>one-shot actions (<see cref="ActionKind"/>), replayed in a loop or once;</item>
    /// <item>locomotion states, by feeding the animator a fixed body state (walking, running, gliding...).</item>
    /// </list>
    /// A playback speed (Time.timeScale) slows everything down or pauses it. While open, gameplay input is
    /// blocked and the cursor is free. Created on its own at startup, in the editor and in builds.
    /// </summary>
    // After HeroPresenter (order 0), which sets crouching from the motor every Update.
    [DefaultExecutionOrder(900)]
    public sealed class DevMenu : MonoBehaviour
    {
        /// <summary>A body state held by the menu instead of the player's motor.</summary>
        private sealed class FixedSource : IAnimationSource
        {
            public Locomotion Locomotion { get; set; }
            public Vector3 Velocity { get; set; }
            public float MaxGroundSpeed { get; set; }
        }

        private enum Kind { Clip, Action, State }

        private sealed class Entry
        {
            public Kind Kind;
            public string Label;
            public Func<CharacterRig, MotionClip> Clip;
            public ActionKind Action;
            public Locomotion Locomotion;
            public float Speed;
            public float VerticalSpeed;
            public bool Crouch;
        }

        private static readonly Entry[] Entries =
        {
            Clip("Parado (idle)", r => r.Idle),
            Clip("Andar", r => r.Walk),
            Clip("Correr", r => r.Run),
            Clip("Começar a andar", r => r.StartWalk),
            Clip("Parar de andar", r => r.StopWalk),
            Clip("Curva à esquerda", r => r.TurnLeft),
            Clip("Meia-volta", r => r.TurnAround),
            Clip("Andar agachado", r => r.CrouchWalk),
            Clip("Pular", r => r.Jump),
            Clip("Pular correndo", r => r.RunningJump),
            Clip("Subir beirada", r => r.Vault),
            Clip("Escalar", r => r.Climb),
            Clip("Combo de espada", r => r.Slash),
            Clip("Chute", r => r.Kick),

            Action("Golpe leve (combo)", ActionKind.SlashLight),
            Action("Golpe pesado", ActionKind.SlashHeavy),
            Action("Estocada", ActionKind.Thrust),
            Action("Arremessar", ActionKind.Throw),
            Action("Atirar com o arco", ActionKind.Shoot),
            Action("Lançar runa", ActionKind.Cast),
            Action("Levar dano", ActionKind.Hurt),
            Action("Pegar item", ActionKind.Pickup),
            Action("Aparar (parry)", ActionKind.Parry),
            Action("Aterrissar", ActionKind.Land),
            Action("Pulo na escalada", ActionKind.ClimbJump),
            Action("Arrancada nadando", ActionKind.SwimDash),
            Action("Chute (finalizador)", ActionKind.Kick),
            Action("Subir beirada", ActionKind.Vault),

            State("Andando (1,6 m/s)", Locomotion.Grounded, 1.6f),
            State("Trotando (4 m/s)", Locomotion.Grounded, 4f),
            State("Correndo (7,5 m/s)", Locomotion.Grounded, 7.5f),
            State("Agachado andando", Locomotion.Grounded, 0.9f, crouch: true),
            State("No ar subindo", Locomotion.Airborne, 0f, 6f),
            State("No ar caindo", Locomotion.Airborne, 0f, -8f),
            State("Planando", Locomotion.Gliding, 4f, -1.5f),
            State("Nadando", Locomotion.Swimming, 1.5f),
            State("Escalando", Locomotion.Climbing, 0f, 1.2f),
        };

        private static readonly float[] Speeds = { 0f, 0.1f, 0.25f, 0.5f, 1f, 2f };

        private bool _open;
        private bool _blockedInput;
        private Vector2 _scroll;
        private PlayerCharacter _player;
        private CharacterAnimator _animator;
        private PlayerInputReader _input;
        private Entry _playing;
        private bool _loopActions = true;
        private float _speed = 1f;
        private float _nextReplay;
        private IAnimationSource _ownSource;
        private FixedSource _fixed;
        private GUIStyle _title, _label, _button, _selected, _section;

        public bool IsOpen => _open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindFirstObjectByType<DevMenu>() != null) return;
            var go = new GameObject("Menu dev");
            DontDestroyOnLoad(go);
            go.AddComponent<DevMenu>();
        }

        private static Entry Clip(string label, Func<CharacterRig, MotionClip> clip) =>
            new Entry { Kind = Kind.Clip, Label = label, Clip = clip };

        private static Entry Action(string label, ActionKind action) =>
            new Entry { Kind = Kind.Action, Label = label, Action = action };

        private static Entry State(string label, Locomotion locomotion, float speed, float vertical = 0f, bool crouch = false) =>
            new Entry { Kind = Kind.State, Label = label, Locomotion = locomotion, Speed = speed, VerticalSpeed = vertical, Crouch = crouch };

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.backquoteKey.wasPressedThisFrame || kb.quoteKey.wasPressedThisFrame)) Toggle();
            if (_playing == null || _animator == null) return;
            // HeroPresenter sets crouching every frame from the motor; the menu's crouched walk wins afterwards.
            if (_playing.Kind == Kind.State && _playing.Crouch) _animator.Crouching = true;

            if (_playing.Kind == Kind.Action && _loopActions && !_animator.ActionPlaying)
            {
                if (_nextReplay <= 0f) _nextReplay = Time.time + 0.4f;
                else if (Time.time >= _nextReplay)
                {
                    _animator.Play(_playing.Action);
                    _nextReplay = 0f;
                }
            }
        }

        /// <summary>Opens or closes the menu. Closing stops whatever animation it was playing.</summary>
        public void Toggle()
        {
            _open = !_open;
            if (_open)
            {
                FindPlayer();
                if (_input != null && !_input.GameplayBlocked)
                {
                    _input.GameplayBlocked = true;
                    _blockedInput = true;
                }
            }
            else
            {
                Stop();
                if (_blockedInput && _input != null) _input.GameplayBlocked = false;
                _blockedInput = false;
            }
        }

        /// <summary>Plays the animation of the list with this label (for tests and the build checks).</summary>
        public bool Play(string label)
        {
            FindPlayer();
            foreach (var entry in Entries)
                if (entry.Label == label)
                {
                    Play(entry);
                    return true;
                }

            return false;
        }

        /// <summary>Labels of every animation the menu lists, in order.</summary>
        public static IEnumerable<string> Labels()
        {
            foreach (var entry in Entries) yield return entry.Label;
        }

        private void FindPlayer()
        {
            if (_animator != null) return;
            _player = FindFirstObjectByType<PlayerCharacter>();
            if (_player == null) return;
            _animator = _player.GetComponent<CharacterAnimator>();
            _input = _player.GetComponent<PlayerInputReader>();
        }

        private void Play(Entry entry)
        {
            if (_animator == null || _animator.Rig == null) return;
            Stop();
            _playing = entry;
            Time.timeScale = _speed;
            switch (entry.Kind)
            {
                case Kind.Clip:
                    _animator.PreviewClip = entry.Clip(_animator.Rig);
                    _animator.PreviewTime = 0f;
                    break;
                case Kind.Action:
                    _animator.Play(entry.Action);
                    _nextReplay = 0f;
                    break;
                case Kind.State:
                    _ownSource = _animator.Source;
                    _fixed = new FixedSource
                    {
                        Locomotion = entry.Locomotion,
                        MaxGroundSpeed = _ownSource != null ? _ownSource.MaxGroundSpeed : 7.5f
                    };
                    _animator.Source = _fixed;
                    break;
            }
        }

        private void Stop()
        {
            if (_animator != null)
            {
                _animator.PreviewClip = null;
                if (_fixed != null && _animator.Source == _fixed) _animator.Source = _ownSource;
            }

            _fixed = null;
            _ownSource = null;
            _playing = null;
            Time.timeScale = 1f;
        }

        private void FixedUpdate()
        {
            // The state's velocity follows the hero's facing, so a walk reads as walking forward.
            if (_fixed != null && _player != null)
                _fixed.Velocity = _player.transform.forward * _playing.Speed + Vector3.up * _playing.VerticalSpeed;
        }

        private void OnDisable()
        {
            if (_open) Toggle();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, richText = true };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            _section = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleLeft, richText = true };
            _selected = new GUIStyle(_button) { fontStyle = FontStyle.Bold };
            _selected.normal.textColor = _selected.hover.textColor = new Color(1f, 0.85f, 0.35f);
        }

        private void OnGUI()
        {
            if (!_open) return;
            EnsureStyles();
            const float width = 380f;
            // Stops above the HUD's clock box in the bottom-right corner.
            var area = new Rect(Screen.width - width - 16f, 16f, width, Screen.height - 110f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));
            GUILayout.Label("Menu dev  <size=12><color=#aaa>(' fecha)</color></size>", _title);

            if (_animator == null || _animator.Rig == null)
            {
                GUILayout.Label("Nenhum herói em cena.", _label);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label(Status(), _label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Parar", _button, GUILayout.Width(70f))) Stop();
            _loopActions = GUILayout.Toggle(_loopActions, " repetir ações", GUILayout.Width(120f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Velocidade", _label, GUILayout.Width(74f));
            foreach (var s in Speeds)
            {
                var text = s == 0f ? "pausa" : s + "x";
                if (GUILayout.Button(text, Mathf.Approximately(_speed, s) ? _selected : _button))
                {
                    _speed = s;
                    if (_playing != null) Time.timeScale = s;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            _scroll = GUILayout.BeginScrollView(_scroll);
            Section("Clipes capturados", Kind.Clip);
            Section("Ações", Kind.Action);
            Section("Estados de locomoção", Kind.State);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Section(string title, Kind kind)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title, _section);
            foreach (var entry in Entries)
            {
                if (entry.Kind != kind) continue;
                var detail = "";
                var available = true;
                if (kind == Kind.Clip)
                {
                    var clip = entry.Clip(_animator.Rig);
                    available = clip != null;
                    detail = clip != null ? $"  <color=#999>{clip.Duration:0.00} s</color>" : "  <color=#c66>ausente</color>";
                }

                GUI.enabled = available;
                if (GUILayout.Button(entry.Label + detail, entry == _playing ? _selected : _button)) Play(entry);
                GUI.enabled = true;
            }
        }

        private string Status()
        {
            if (_playing == null) return "<color=#aaa>Escolha uma animação. O herói fica parado no lugar.</color>";
            var line = $"Tocando: <b>{_playing.Label}</b>";
            if (_playing.Kind == Kind.Clip && _animator.PreviewClip != null)
            {
                var d = _animator.PreviewClip.Duration;
                var t = _animator.PreviewTime % d;
                line += $"\n<color=#aaa>{t:0.00} / {d:0.00} s  ({t / d * 100f:0}%)</color>";
            }
            else if (_playing.Kind == Kind.Action)
                line += $"\n<color=#aaa>{_animator.ActionProgress * 100f:0}%</color>";

            return line;
        }
    }
}
