using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Characters;
using Zeldexile.World;

namespace Zeldexile.Gameplay
{
    /// <summary>
    /// Stage that shows the hero through every action the game has: rest, walk, run, the three weapon swings,
    /// guard and parry, throw, bow aim, crouch, kneel, hurt, jump, climb, glide and swim. It uses the same
    /// model, rig, animator and gear rules as the player, so it doubles as the visual check for the character.
    /// Open Assets/Zeldexile/Scenes/Showcase.unity (menu: Zeldexile → Criar cena de vitrine do herói).
    /// </summary>
    public sealed class CharacterShowcase : MonoBehaviour
    {
        private sealed class Source : IAnimationSource
        {
            public Locomotion Locomotion { get; set; }
            public Vector3 Velocity { get; set; }
            public float MaxGroundSpeed => 7f;
        }

        public struct Step
        {
            public string Label;
            public float Seconds;
            public Locomotion Locomotion;
            /// <summary>Ground speed in m/s; the hero walks a circle at exactly this speed.</summary>
            public float Speed;
            public string Stance;
            public ActionKind Action;
            /// <summary>Seconds between repeats of the action (0 = once at the start).</summary>
            public float Repeat;
        }

        public static readonly Step[] Steps =
        {
            new Step { Label = "Repouso", Seconds = 2.5f, Stance = "" },
            new Step { Label = "Caminhada (1,6 m/s)", Seconds = 3f, Speed = 1.6f, Stance = "" },
            new Step { Label = "Corrida (7 m/s)", Seconds = 2.5f, Speed = 7f, Stance = "" },
            new Step { Label = "Em guarda", Seconds = 1.5f, Stance = "armed" },
            new Step { Label = "Combo de espada", Seconds = 2.2f, Stance = "armed", Action = ActionKind.SlashLight, Repeat = 0.42f },
            new Step { Label = "Golpe pesado (duas mãos)", Seconds = 1.6f, Stance = "armed", Action = ActionKind.SlashHeavy, Repeat = 0.8f },
            new Step { Label = "Estocada (lança)", Seconds = 1.4f, Stance = "armed", Action = ActionKind.Thrust, Repeat = 0.45f },
            new Step { Label = "Defesa com escudo", Seconds = 2f, Stance = "guard" },
            new Step { Label = "Defesa perfeita", Seconds = 1.2f, Stance = "guard", Action = ActionKind.Parry, Repeat = 0.6f },
            new Step { Label = "Arremesso", Seconds = 1.2f, Stance = "armed", Action = ActionKind.Throw, Repeat = 0.6f },
            new Step { Label = "Mira com arco", Seconds = 1.8f, Stance = "aim", Action = ActionKind.Shoot, Repeat = 0.6f },
            new Step { Label = "Agachar / pegar", Seconds = 1.4f, Stance = "armed", Action = ActionKind.Pickup, Repeat = 0.7f },
            new Step { Label = "Ajoelhar (panela, estátua)", Seconds = 1.8f, Stance = "kneel" },
            new Step { Label = "Dano", Seconds = 1.2f, Stance = "armed", Action = ActionKind.Hurt, Repeat = 0.5f },
            new Step { Label = "Pulo", Seconds = 1.2f, Locomotion = Locomotion.Airborne },
            new Step { Label = "Escalada", Seconds = 1.8f, Locomotion = Locomotion.Climbing, Speed = 2f },
            new Step { Label = "Planador", Seconds = 1.8f, Locomotion = Locomotion.Gliding, Speed = 6f },
            new Step { Label = "Nado", Seconds = 1.8f, Locomotion = Locomotion.Swimming, Speed = 2.4f },
        };

        public static float TotalSeconds
        {
            get
            {
                var t = 0f;
                foreach (var s in Steps) t += s.Seconds;
                return t;
            }
        }

        [SerializeField] private float circleRadius = 3f;
        [SerializeField] private bool orbitCamera = true;

        public Camera ViewCamera { get; private set; }
        public string CurrentLabel { get; private set; } = "";

        private readonly Source _source = new Source();
        private readonly Dictionary<GearPart, List<Renderer>> _gear = new Dictionary<GearPart, List<Renderer>>();
        private CharacterAnimator _animator;
        private Transform _hero;
        private float _clock;
        private int _step = -1;
        private float _stepStart;
        private float _lastAction;
        private float _angle;
        private GUIStyle _label;
        private Vector3 _focus;
        private bool _focusSet;

        private void Awake()
        {
            BuildStage();
            var holder = new GameObject("Herói");
            holder.transform.SetParent(transform, false);
            _hero = holder.transform;
            _animator = holder.AddComponent<CharacterAnimator>();
            _animator.Rig = CharacterRig.Build(_hero, RigStyle.Hero);
            _animator.Source = _source;
            foreach (var r in _animator.Rig.GetComponentsInChildren<Renderer>(true))
            {
                var part = CharacterRig.Classify(r.name);
                if (part == GearPart.None) continue;
                if (!_gear.TryGetValue(part, out var list)) _gear[part] = list = new List<Renderer>();
                list.Add(r);
            }
        }

        private void BuildStage()
        {
            var floor = Effects.Primitive(PrimitiveType.Cylinder, transform, Palette.Solid(new Color(0.55f, 0.66f, 0.42f)));
            floor.name = "Palco";
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(9f, 0.05f, 9f);

            if (FindAnyObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                sun.intensity = 1.05f;
                sun.color = new Color(1f, 0.97f, 0.92f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.9f);
                RenderSettings.ambientEquatorColor = new Color(0.78f, 0.8f, 0.82f);
                RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.36f);
            }

            ViewCamera = Camera.main;
            if (ViewCamera == null)
            {
                ViewCamera = new GameObject("Câmera").AddComponent<Camera>();
                ViewCamera.tag = "MainCamera";
            }

            ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.backgroundColor = new Color(0.78f, 0.86f, 0.94f);
            ViewCamera.fieldOfView = 40f;
        }

        /// <summary>Advances the show by a fixed time (captures call this with 1/fps for exact frames).</summary>
        public void Advance(float dt)
        {
            _clock += dt;
            var total = TotalSeconds;
            var t = _clock % total;
            var index = 0;
            var start = 0f;
            while (index < Steps.Length - 1 && t >= start + Steps[index].Seconds)
            {
                start += Steps[index].Seconds;
                index++;
            }

            var step = Steps[index];
            if (index != _step)
            {
                _step = index;
                _stepStart = _clock;
                _lastAction = -100f;
                CurrentLabel = step.Label;
            }

            // Locomotion: walk a circle at exactly the animated speed, so feet and ground travel agree.
            var speed = step.Locomotion == Locomotion.Grounded || step.Locomotion == Locomotion.Swimming ? step.Speed : 0f;
            _angle += speed / circleRadius * dt;
            var onCircle = speed > 0f;
            var pos = onCircle ? new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * circleRadius : Vector3.zero;
            var heading = onCircle ? new Vector3(-Mathf.Sin(_angle), 0f, Mathf.Cos(_angle)) : Vector3.back;
            pos.y = step.Locomotion == Locomotion.Swimming ? 0.6f : step.Locomotion == Locomotion.Airborne ? 0.6f : step.Locomotion == Locomotion.Gliding ? 1.2f : 0f;
            _hero.localPosition = Vector3.Lerp(_hero.localPosition, pos, onCircle ? 1f : 1f - Mathf.Exp(-6f * dt));
            _hero.localRotation = Quaternion.Slerp(_hero.localRotation, Quaternion.LookRotation(heading), 1f - Mathf.Exp(-10f * dt));

            _source.Locomotion = step.Locomotion;
            _source.Velocity = step.Locomotion == Locomotion.Climbing ? Vector3.up * step.Speed
                : step.Locomotion == Locomotion.Airborne ? new Vector3(0f, 2f, 0f)
                : heading * step.Speed;

            _animator.Guarding = step.Stance == "guard";
            _animator.Aiming = step.Stance == "aim";
            _animator.Kneeling = step.Stance == "kneel";
            var armed = step.Stance == "armed" || step.Stance == "guard";
            _animator.Armed = armed;
            _animator.ShieldInHand = armed;
            ShowGear(armed, step.Stance == "aim");

            if (step.Action != ActionKind.None && (_lastAction < 0f || (step.Repeat > 0f && _clock - _lastAction >= step.Repeat)))
            {
                _lastAction = _clock;
                _animator.Play(step.Action);
            }

            if (ViewCamera != null)
            {
                var yaw = orbitCamera ? 200f + _clock * 14f : 200f;
                // Follow the hero (including height when gliding or jumping), slightly farther while moving.
                var target = _hero.position + Vector3.up * 1.0f;
                _focus = _focusSet ? Vector3.Lerp(_focus, target, 1f - Mathf.Exp(-5f * dt)) : target;
                _focusSet = true;
                var rot = Quaternion.Euler(12f, yaw, 0f);
                ViewCamera.transform.SetPositionAndRotation(_focus - rot * Vector3.forward * (onCircle ? 6.2f : 5.2f), rot);
            }
        }

        private void ShowGear(bool armed, bool aiming)
        {
            Set(GearPart.SwordHand, armed);
            Set(GearPart.SwordBack, !armed);
            Set(GearPart.ShieldHand, armed);
            Set(GearPart.ShieldBack, !armed);
            Set(GearPart.BowHand, aiming);
            Set(GearPart.BowBack, false);
        }

        private void Set(GearPart part, bool visible)
        {
            if (!_gear.TryGetValue(part, out var list)) return;
            foreach (var r in list)
                if (r.enabled != visible)
                    r.enabled = visible;
        }

        /// <summary>Drives itself in play unless a capture drives it frame by frame.</summary>
        public bool ManualClock { get; set; }

        private void Update()
        {
            if (!ManualClock) Advance(Time.deltaTime);
        }

        private void OnGUI()
        {
            if (ManualClock) return;
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(24, 18, 600, 34), CurrentLabel, _label);
        }
    }
}
