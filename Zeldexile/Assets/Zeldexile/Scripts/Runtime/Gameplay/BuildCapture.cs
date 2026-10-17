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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-zxshot");
            if (index < 0 || index + 1 >= args.Length) return;
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
