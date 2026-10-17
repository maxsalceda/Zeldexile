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
            go.AddComponent<BuildCapture>().StartCoroutine(Capture(args[index + 1]));
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
