using UnityEngine;
using Zeldexile.Combat;

namespace Zeldexile.Player
{
    /// <summary>Orbit camera behind the character, with collision so it never goes through cliffs.</summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;
        public PlayerInputReader Input;
        public PlayerTargeting Targeting;

        [SerializeField] private float distance = 6.5f;
        [SerializeField] private float pivotHeight = 1.7f;
        [SerializeField] private float sensitivity = 1.6f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private float shoulderOffset = 0.6f;

        [Header("Zoom")]
        [SerializeField] private float minDistance = 2.5f;
        [SerializeField] private float maxDistance = 12f;
        [SerializeField] private float zoomStep = 0.9f;
        [SerializeField] private float zoomSmoothing = 10f;

        private float _yaw;
        private float _pitch = 14f;
        private float _currentDistance;
        /// <summary>Distance chosen with the wheel; <see cref="distance"/> eases toward it.</summary>
        private float _targetDistance;

        public float Yaw => _yaw;
        public float Distance => distance;

        private void Start()
        {
            _targetDistance = distance = Mathf.Clamp(distance, minDistance, maxDistance);
            _currentDistance = distance;
            if (Target != null) _yaw = Target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            var dt = Time.unscaledDeltaTime;
            var locked = Targeting != null ? Targeting.Target : null;
            if (locked != null)
            {
                // Lock-on: frame the target, keep the player on the near side of the shot.
                var to = locked.transform.position - Target.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-8f * dt));
                _pitch = Mathf.Lerp(_pitch, 12f, 1f - Mathf.Exp(-4f * dt));
            }

            if (Input != null)
            {
                if (locked == null) _yaw += Input.Look.x * sensitivity;
                _pitch = Mathf.Clamp(_pitch - Input.Look.y * sensitivity, minPitch, maxPitch);
                // Wheel up = closer. Steps scale with distance so zooming feels even near and far.
                if (Mathf.Abs(Input.Zoom) > 0.001f)
                    _targetDistance = Mathf.Clamp(_targetDistance - Input.Zoom * zoomStep * (_targetDistance / 6.5f), minDistance, maxDistance);
                var blocked = Input.GameplayBlocked;
                Cursor.lockState = blocked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = blocked;
            }

            distance = Mathf.Lerp(distance, _targetDistance, 1f - Mathf.Exp(-zoomSmoothing * dt));

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var pivot = Target.position + Vector3.up * pivotHeight + rotation * Vector3.right * shoulderOffset;
            var back = rotation * Vector3.back;

            // The player sits on the Ignore Raycast layer, so default casts never hit it.
            var wanted = distance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.6f, hit.distance);

            _currentDistance = wanted < _currentDistance
                ? wanted
                : Mathf.Lerp(_currentDistance, wanted, 1f - Mathf.Exp(-6f * dt));

            // Shake: trauma squared drives smooth noise, so small hits barely move and big ones jolt.
            var trauma = GameFeel.Current != null ? GameFeel.Current.Trauma : 0f;
            var shake = trauma * trauma;
            var time = Time.unscaledTime * 22f;
            var offset = new Vector3(Mathf.PerlinNoise(time, 0.1f) - 0.5f, Mathf.PerlinNoise(0.2f, time) - 0.5f, 0f) * (0.5f * shake);
            var roll = (Mathf.PerlinNoise(time, 7.3f) - 0.5f) * 6f * shake;

            transform.SetPositionAndRotation(pivot + back * _currentDistance + rotation * offset, rotation * Quaternion.Euler(0f, 0f, roll));
        }
    }
}
