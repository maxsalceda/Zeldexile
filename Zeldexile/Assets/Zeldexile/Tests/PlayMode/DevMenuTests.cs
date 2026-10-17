using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zeldexile.Characters;
using Zeldexile.Player;
using Zeldexile.UI;

namespace Zeldexile.Tests
{
    /// <summary>The developer menu plays every animation it lists on the player's hero, and closing it restores play.</summary>
    public sealed class DevMenuTests
    {
        [UnityTest]
        public IEnumerator EveryListedAnimationPlaysOnTheHero()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Prototype");
            for (var i = 0; i < 20; i++) yield return null;

            var menu = Object.FindFirstObjectByType<DevMenu>();
            Assert.That(menu, Is.Not.Null, "The dev menu creates itself at startup.");
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            Assert.That(player, Is.Not.Null);
            var animator = player.GetComponent<CharacterAnimator>();
            var input = player.GetComponent<PlayerInputReader>();

            menu.Toggle();
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(input.GameplayBlocked, Is.True, "Open, the menu takes the keyboard and mouse.");

            var labels = DevMenu.Labels().ToList();
            Assert.That(labels.Count, Is.GreaterThanOrEqualTo(30));
            foreach (var label in labels)
            {
                Assert.That(menu.Play(label), Is.True, label);
                var hand = animator.Rig.WristR;
                var startPos = animator.Rig.transform.InverseTransformPoint(hand.position);
                var moved = 0f;
                // By time, not frames: batch mode runs far above 60 fps and poses blend over time.
                var until = Time.time + 0.8f;
                while (Time.time < until)
                {
                    yield return null;
                    moved = Mathf.Max(moved, (animator.Rig.transform.InverseTransformPoint(hand.position) - startPos).magnitude);
                }

                Debug.Log($"Menu dev: {label} moveu a mão {moved * 100f:0.0} cm");
            }

            menu.Toggle();
            yield return null;
            Assert.That(menu.IsOpen, Is.False);
            Assert.That(input.GameplayBlocked, Is.False, "Closed, the game gets its input back.");
            Assert.That(animator.PreviewClip, Is.Null, "Closing stops the clip preview.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(animator.Source, Is.Not.Null);
            Assert.That(animator.Source.GetType().Name, Is.Not.EqualTo("FixedSource"), "Closing gives the animator its motor back.");
        }

        [UnityTest]
        public IEnumerator CapturedClipsPlayFromTheirStart()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Prototype");
            for (var i = 0; i < 20; i++) yield return null;
            var menu = Object.FindFirstObjectByType<DevMenu>();
            var animator = Object.FindFirstObjectByType<PlayerCharacter>().GetComponent<CharacterAnimator>();
            menu.Toggle();
            Assert.That(menu.Play("Parado (idle)"), Is.True);
            Assert.That(animator.PreviewClip, Is.SameAs(animator.Rig.Idle));
            Assert.That(menu.Play("Andar"), Is.True);
            Assert.That(animator.PreviewClip, Is.SameAs(animator.Rig.Walk));
            Assert.That(animator.PreviewTime, Is.EqualTo(0f));
            yield return new WaitForSeconds(0.5f);
            Assert.That(animator.PreviewTime, Is.GreaterThan(0.3f), "The clip runs with the game's time.");
            Assert.That(menu.Play("Não existe"), Is.False);
            menu.Toggle();
        }
    }
}
