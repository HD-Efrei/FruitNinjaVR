#if UNITY_STANDALONE_WIN
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Management;

public sealed class WindowsVRMenu : MonoBehaviour
{
    private const string ModeArgument = "--fruitninja-vr=";
    private static string SimulatorDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MetaXRSimulator-Compatibility", "78.1");
    private static string RuntimePath => Path.Combine(SimulatorDirectory, "meta_openxr_simulator.json");
    private static string ConfigPath => Path.Combine(SimulatorDirectory, "config", "sim_core_configuration_compat.json");

    private bool menuOpen;
    private bool initializing;
    private bool initializationAttempted;
    private bool sessionRunning;
    private bool subsystemsPaused;
    private bool shortcutHeld;
    private IntPtr gameWindow;
    private string currentMode;
    private string message = "Choisissez votre mode de jeu.";
    private float previousTimeScale;
    private bool previousAudioPause;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private XRManagerSettings xrManager;
    private GUIStyle titleStyle, textStyle, buttonStyle;

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateMenu()
    {
#if !UNITY_EDITOR
        var menu = new GameObject("Menu VR Windows");
        DontDestroyOnLoad(menu);
        menu.AddComponent<WindowsVRMenu>();
#endif
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        using (Process process = Process.GetCurrentProcess()) gameWindow = process.MainWindowHandle;
        OpenMenu();
        yield return null;
        foreach (string argument in Environment.GetCommandLineArgs())
        {
            if (!argument.StartsWith(ModeArgument, StringComparison.Ordinal)) continue;
            string mode = argument.Substring(ModeArgument.Length);
            if (mode == "meta" || mode == "headset") SelectMode(mode);
            break;
        }
    }

    private void Update()
    {
        // Meta possède sa propre fenêtre : les raccourcis doivent fonctionner aussi lorsqu'elle a le focus.
        GetWindowThreadProcessId(GetForegroundWindow(), out uint process);
        bool held = process == GetCurrentProcessId() &&
            ((GetAsyncKeyState(0x1B) & 0x8000) != 0 || (GetAsyncKeyState(0x70) & 0x8000) != 0);
        bool pressed = held && !shortcutHeld;
        shortcutHeld = held;
        if (pressed && !initializing)
        {
            if (menuOpen && sessionRunning) CloseMenu();
            else OpenMenu();
        }
    }

    private void OpenMenu()
    {
        if (menuOpen) return;
        if (sessionRunning)
        {
            xrManager.StopSubsystems();
            subsystemsPaused = true;
        }
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        menuOpen = true;
        if (gameWindow != IntPtr.Zero) SetForegroundWindow(gameWindow);
    }

    private void CloseMenu()
    {
        if (!menuOpen) return;
        if (subsystemsPaused)
        {
            xrManager.StartSubsystems();
            RefreshTrackingOrigin();
            subsystemsPaused = false;
        }
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        menuOpen = false;
    }

    private void SelectMode(string mode)
    {
        if (initializing) return;
        if (mode == "meta" && (!File.Exists(RuntimePath) || !File.Exists(ConfigPath)))
        {
            message = "La simulation Meta n'est pas installée sur cet ordinateur.";
            return;
        }
        if (sessionRunning && mode == currentMode)
        {
            CloseMenu();
            return;
        }

        // Un chargeur OpenXR déjà utilisé conserve son runtime natif jusqu'à la fin du processus.
        if (initializationAttempted) Restart(mode);
        else StartCoroutine(StartVR(mode));
    }

    private static void ConfigureRuntime(string mode, ProcessStartInfo process = null)
    {
        string runtime = mode == "meta" ? RuntimePath : null;
        string config = mode == "meta" ? ConfigPath : null;
        foreach (string key in new[] { "XR_RUNTIME_JSON", "XR_SELECTED_RUNTIME_JSON", "OTHER_XR_RUNTIME_JSON", "META_XRSIM_CONFIG_JSON" })
        {
            string value = key == "META_XRSIM_CONFIG_JSON" ? config : runtime;
            if (process == null) Environment.SetEnvironmentVariable(key, value);
            else if (value == null) process.EnvironmentVariables.Remove(key);
            else process.EnvironmentVariables[key] = value;
        }
    }

    private IEnumerator StartVR(string mode)
    {
        initializing = true;
        message = mode == "meta" ? "Démarrage de la simulation Meta…" : "Connexion au casque VR…";
        yield return null;
        ConfigureRuntime(mode);
        xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (xrManager == null)
        {
            message = "La configuration XR du jeu est indisponible.";
            initializing = false;
            yield break;
        }

        initializationAttempted = true;
        yield return xrManager.InitializeLoader();
        if (xrManager.activeLoader == null)
        {
            message = mode == "meta"
                ? "La simulation n'a pas démarré. Réessayez ou choisissez le casque."
                : "Aucun casque disponible. Connectez-le ou choisissez la simulation.";
            initializing = false;
            yield break;
        }
        xrManager.StartSubsystems();
        RefreshTrackingOrigin();
        yield return new WaitForSecondsRealtime(0.5f);
        foreach (var origin in FindObjectsByType<XROrigin>())
        {
            UnityEngine.Debug.Log($"Hauteur VR : origine={origin.transform.position.y:F2}, " +
                $"offset={origin.CameraFloorOffsetObject.transform.localPosition.y:F2}, " +
                $"casque local={origin.Camera.transform.localPosition.y:F2}, mode={origin.CurrentTrackingOriginMode}");
        }
        currentMode = mode;
        sessionRunning = true;
        initializing = false;
        message = "Changer de mode relance le jeu et interrompt la partie en cours.";
        UnityEngine.Debug.Log("Mode VR Windows : " + mode);
        CloseMenu();
    }

    private static void RefreshTrackingOrigin()
    {
        // Le XR Origin a démarré avant les sous-systèmes XR : réappliquer son mode relance sa configuration de hauteur.
        foreach (var origin in FindObjectsByType<XROrigin>())
        {
            var requestedMode = origin.RequestedTrackingOriginMode;
            origin.RequestedTrackingOriginMode = requestedMode;
        }
    }

    private void Restart(string mode)
    {
        try
        {
            string executable = Application.dataPath.Substring(0, Application.dataPath.Length - "_Data".Length) + ".exe";
            var process = new ProcessStartInfo(executable, ModeArgument + mode + " -screen-fullscreen 0")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable)
            };
            ConfigureRuntime(mode, process);
            Process child = Process.Start(process);
            if (child == null) throw new InvalidOperationException("Le nouveau processus n'a pas démarré.");
            child.Dispose();
            Application.Quit();
        }
        catch (Exception exception)
        {
            message = "Le jeu n'a pas pu se relancer. Fermez-le puis ouvrez-le à nouveau.";
            UnityEngine.Debug.LogWarning("Relance VR impossible : " + exception.Message);
        }
    }

    private void OnApplicationQuit()
    {
        if (xrManager == null || xrManager.activeLoader == null) return;
        if (!subsystemsPaused) xrManager.StopSubsystems();
        xrManager.DeinitializeLoader();
    }

    private void OnGUI()
    {
        if (!menuOpen) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        }

        float scale = Mathf.Min(Screen.width / 900f, Screen.height / 680f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = Screen.height / scale;
        GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.94f);
        GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        float x = (width - 560f) / 2f, y = (height - 570f) / 2f;
        GUI.Label(new Rect(x, y, 560, 52), "Fruit Ninja VR", titleStyle);
        GUI.Label(new Rect(x, y + 58, 560, 36), "Paramètres de lancement", textStyle);
        GUI.Label(new Rect(x, y + 102, 560, 36), sessionRunning
            ? "Mode actif : " + (currentMode == "meta" ? "Simulation Meta" : "Casque VR")
            : "Casque VR ou simulation sans casque", textStyle);
        GUI.Label(new Rect(x, y + 147, 560, 72), message, textStyle);

        GUI.enabled = !initializing;
        if (GUI.Button(new Rect(x + 30, y + 236, 500, 54), "Simulation Meta", buttonStyle)) SelectMode("meta");
        if (GUI.Button(new Rect(x + 30, y + 304, 500, 54), "Casque VR", buttonStyle)) SelectMode("headset");
        if (sessionRunning && GUI.Button(new Rect(x + 30, y + 372, 500, 54), "Reprendre", buttonStyle)) CloseMenu();
        GUI.enabled = true;
        if (GUI.Button(new Rect(x + 30, y + 440, 500, 54), "Quitter le jeu", buttonStyle)) Application.Quit();
        GUI.Label(new Rect(x, y + 514, 560, 40), "Échap ou F1 : ouvrir / fermer ce menu", textStyle);
        GUI.matrix = Matrix4x4.identity;
    }
}
#endif
