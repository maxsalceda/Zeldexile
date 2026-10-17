using UnityEngine;
using Zeldexile.Combat;

namespace Zeldexile.Player
{
    /// <summary>
    /// Lock-on: pick the nearest creature in front of the camera, keep it until it dies, goes too far,
    /// or the player presses lock again.
    /// </summary>
    public sealed class PlayerTargeting : MonoBehaviour
    {
        public PlayerInputReader Input;
        public Camera ViewCamera;

        [SerializeField] private float acquireRange = 22f;
        [SerializeField] private float loseRange = 30f;
        [SerializeField] private float maxAngle = 70f;

        public EnemyBrain Target { get; private set; }

        private void Update()
        {
            if (Target != null && (Target.Health.IsDead || !Target.isActiveAndEnabled ||
                                   (Target.transform.position - transform.position).sqrMagnitude > loseRange * loseRange))
                Target = null;

            if (!Input.LockPressed) return;
            Target = Target != null ? null : FindTarget();
        }

        private EnemyBrain FindTarget()
        {
            EnemyBrain best = null;
            var bestScore = float.MaxValue;
            var forward = ViewCamera != null ? ViewCamera.transform.forward : transform.forward;
            forward.y = 0f;
            foreach (var enemy in EnemyBrain.Active)
            {
                if (enemy.Health.IsDead) continue;
                var to = enemy.transform.position - transform.position;
                var distance = to.magnitude;
                if (distance > acquireRange) continue;
                to.y = 0f;
                var angle = Vector3.Angle(forward, to);
                if (angle > maxAngle) continue;
                var score = distance + angle * 0.15f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }

            return best;
        }
    }
}
