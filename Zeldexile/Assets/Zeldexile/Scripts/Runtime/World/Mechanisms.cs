using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain;

namespace Zeldexile.World
{
    /// <summary>Metal that stays powered for a moment after lightning reaches it.</summary>
    [RequireComponent(typeof(ElementalSurface))]
    public sealed class MetalConductor : MonoBehaviour
    {
        [SerializeField] private float poweredDuration = 2.5f;
        private float _poweredUntil;
        private Renderer[] _copper;
        private bool _wasPowered;

        public bool IsPowered => Time.time < _poweredUntil;
        public event Action<MetalConductor> Powered;

        private void Awake()
        {
            var surface = GetComponent<ElementalSurface>();
            surface.Kind = SurfaceKind.Metal;
            surface.Energized += _ =>
            {
                _poweredUntil = Time.time + poweredDuration;
                Powered?.Invoke(this);
            };
            Paint(Palette.Metal);
        }

        /// <summary>Only the copper pieces glow; stone parts of the model keep their color.</summary>
        private void Paint(Color color)
        {
            if (_copper == null || _copper.Length == 0)
            {
                var all = GetComponentsInChildren<Renderer>();
                _copper = System.Array.FindAll(all, r => r.enabled && (r.name.EndsWith("@copper") || r.gameObject == gameObject));
            }

            foreach (var r in _copper) Palette.Paint(r, color);
        }

        private void Update()
        {
            if (IsPowered == _wasPowered) return;
            _wasPowered = IsPowered;
            Paint(_wasPowered ? Palette.MetalPowered : Palette.Metal);
        }
    }

    /// <summary>Opens once every linked conductor is powered at the same time.</summary>
    public sealed class PowerGate : MonoBehaviour
    {
        public readonly List<MetalConductor> Conductors = new List<MetalConductor>();
        public bool IsOpen { get; private set; }
        public event Action<PowerGate> Opened;

        private Vector3 _closedPosition;
        private float _openProgress;
        private float _dropDistance;

        private void Start()
        {
            _closedPosition = transform.position;
            var col = GetComponent<Collider>();
            _dropDistance = (col != null ? col.bounds.size.y : transform.localScale.y) + 0.1f;
            foreach (var c in Conductors) c.Powered += _ => Check();
        }

        private void Check()
        {
            if (IsOpen) return;
            foreach (var c in Conductors)
                if (!c.IsPowered)
                    return;
            IsOpen = true;
            Opened?.Invoke(this);
        }

        private void Update()
        {
            if (!IsOpen || _openProgress >= 1f) return;
            _openProgress = Mathf.Min(1f, _openProgress + Time.deltaTime * 0.6f);
            transform.position = _closedPosition + Vector3.down * _dropDistance * _openProgress;
        }
    }
}
