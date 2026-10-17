using System;
using System.Collections;
using UnityEngine;

namespace Zeldexile.Gameplay
{
    /// <summary>
    /// Build check: "Zeldexile.exe -zxshot C:\path\shot.png" saves one screenshot after a few seconds
    /// and quits. Lets a test pipeline see what the real build renders. Does nothing otherwise.
    /// </summary>
    public sealed class BuildCapture : MonoBehaviour
    {
        /// <summary>
        /// The game opens in the open world (Vale dos Pomares). "Zeldexile.exe -ilha" opens the trial island,
        /// "-vitrine" the hero showcase; "-vale" stays accepted.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OpenShowcase()
        {
            var args = Environment.GetCommandLineArgs();
            var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string wanted = null;
            if (Array.IndexOf(args, "-vitrine") >= 0) wanted = "Showcase";
            else if (Array.IndexOf(args, "-ilha") >= 0) wanted = "Prototype";
            else if (Array.IndexOf(args, "-vale") >= 0) wanted = "ValeInicial";
            if (wanted != null && wanted != current) UnityEngine.SceneManagement.SceneManager.LoadScene(wanted);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-zxshot");
            if (index < 0 || index + 1 >= args.Length) return;
            // A capture must keep running even if its window never gets focus.
            Application.runInBackground = true;
            var go = new GameObject("Captura de build");
            DontDestroyOnLoad(go);
            var routine = Array.IndexOf(args, "-zxwalk") >= 0 ? WalkSequence(args[index + 1]) : Capture(args[index + 1]);
            go.AddComponent<BuildCapture>().StartCoroutine(routine);
        }

        /// <summary>
        /// "-zxshot path.png -zxwalk": six frames of the heavy attack seen from the side (path-pesado-1..6), then the
        /// hero walks forward by itself: six frames of the walk (path-andar-1..6), one after stopping (path-parado),
        /// one sprinting (path-correr); then the game quits.
        /// </summary>
        private static IEnumerator WalkSequence(string path)
        {
            var stem = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? "", System.IO.Path.GetFileNameWithoutExtension(path));
            yield return new WaitForSecondsRealtime(4f);

            // Heavy attack first, standing in the open at the spawn, seen from the side: a tap of the right button,
            // then six frames over its duration.
            Zeldexile.Player.PlayerInputReader.ScriptedLook = new Vector2(1.6f, 0f);
            yield return new WaitForSecondsRealtime(0.5f);
            Zeldexile.Player.PlayerInputReader.ScriptedLook = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.5f);
            Zeldexile.Player.PlayerInputReader.ScriptedHeavyPress = true;
            yield return null;
            Zeldexile.Player.PlayerInputReader.ScriptedHeavyRelease = true;
            for (var i = 1; i <= 6; i++)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                ScreenCapture.CaptureScreenshot(stem + "-pesado-" + i + ".png");
            }

            yield return new WaitForSecondsRealtime(1f);
            Zeldexile.Player.PlayerInputReader.ScriptedLook = new Vector2(-1.6f, 0f);
            yield return new WaitForSecondsRealtime(0.5f);
            Zeldexile.Player.PlayerInputReader.ScriptedLook = Vector2.zero;
            Zeldexile.Player.PlayerInputReader.ScriptedMove = new Vector2(0f, 1f);
            yield return new WaitForSecondsRealtime(2.5f);
            for (var i = 1; i <= 6; i++)
            {
                ScreenCapture.CaptureScreenshot(stem + "-andar-" + i + ".png");
                yield return new WaitForSecondsRealtime(0.15f);
            }

            Zeldexile.Player.PlayerInputReader.ScriptedMove = Vector2.zero;
            yield return new WaitForSecondsRealtime(2.5f);
            ScreenCapture.CaptureScreenshot(stem + "-parado.png");
            yield return new WaitForSecondsRealtime(0.5f);
            Zeldexile.Player.PlayerInputReader.ScriptedMove = new Vector2(0f, 1f);
            Zeldexile.Player.PlayerInputReader.ScriptedSprint = true;
            yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(stem + "-correr.png");
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }

        private static IEnumerator Capture(string path)
        {
            yield return new WaitForSecondsRealtime(6f);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }
    }
}
