using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class XRSimulationMode
{
    private const string KeyboardMenu = "Tools/Simulation VR/Clavier Unity";
    private const string MetaMenu = "Tools/Simulation VR/Meta XR compatible Intel";
    private const string HeadsetMenu = "Tools/Simulation VR/Casque reel";
    private const string SimulatorSettings = "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset";
    private static string PreferenceKey => "FruitNinja.XRSimulationMode." + Application.dataPath;
    private static string Mode => EditorPrefs.GetString(PreferenceKey, "Keyboard");
    private static string RuntimeDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MetaXRSimulator-Compatibility", "78.1");

    static XRSimulationMode()
    {
        EditorApplication.delayCall += RestoreMode;
    }

    [MenuItem(KeyboardMenu, false, 100)]
    internal static void UseKeyboard() => SelectMode("Keyboard");

    [MenuItem(MetaMenu, false, 101)]
    internal static void UseMeta() => SelectMode("Meta");

    [MenuItem(HeadsetMenu, false, 102)]
    internal static void UseHeadset() => SelectMode("Headset");

    [MenuItem(KeyboardMenu, true)]
    private static bool ValidateKeyboard() => Validate(KeyboardMenu, "Keyboard");

    [MenuItem(MetaMenu, true)]
    private static bool ValidateMeta() => Validate(MetaMenu, "Meta");

    [MenuItem(HeadsetMenu, true)]
    private static bool ValidateHeadset() => Validate(HeadsetMenu, "Headset");

    private static bool Validate(string menu, string mode)
    {
        Menu.SetChecked(menu, Mode == mode);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool RuntimeAvailable()
    {
        return File.Exists(Path.Combine(RuntimeDirectory, "meta_openxr_simulator.json"))
            && File.Exists(Path.Combine(RuntimeDirectory, "config", "sim_core_configuration_compat.json"));
    }

    private static void SelectMode(string mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (mode == "Meta" && !RuntimeAvailable())
        {
            EditorUtility.DisplayDialog("Simulation VR", "Le runtime Meta XR compatible 78.1 doit etre installe dans :\n" + RuntimeDirectory, "OK");
            return;
        }
        ApplyMode(mode);
        EditorPrefs.SetString(PreferenceKey, mode);
        Debug.Log("Simulation VR : " + (mode == "Meta" ? "Meta XR 78.1, mode compatible Intel" : mode == "Keyboard" ? "clavier Unity" : "casque reel, runtime du systeme"));
    }

    private static void RestoreMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        string mode = Mode;
        if (mode == "Meta" && !RuntimeAvailable())
        {
            mode = "Keyboard";
            EditorPrefs.SetString(PreferenceKey, mode);
        }
        ApplyMode(mode);
    }

    private static void ApplyMode(string mode)
    {
        string runtime = mode == "Meta" ? Path.Combine(RuntimeDirectory, "meta_openxr_simulator.json") : null;
        string config = mode == "Meta" ? Path.Combine(RuntimeDirectory, "config", "sim_core_configuration_compat.json") : null;
        Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", runtime);
        Environment.SetEnvironmentVariable("XR_SELECTED_RUNTIME_JSON", runtime);
        Environment.SetEnvironmentVariable("META_XRSIM_CONFIG_JSON", config);

        if (mode == "Meta")
        {
            Environment.SetEnvironmentVariable("OTHER_XR_RUNTIME_JSON", runtime);
            ConfigureOtherRuntime(runtime);
        }

        var settings = AssetDatabase.LoadMainAssetAtPath(SimulatorSettings);
        if (settings != null)
        {
            var serialized = new SerializedObject(settings);
            var enabled = serialized.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab");
            if (enabled != null && enabled.boolValue != (mode == "Keyboard"))
            {
                enabled.boolValue = mode == "Keyboard";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(settings);
            }
        }
    }

    private static void ConfigureOtherRuntime(string path)
    {
        const string selectorName = "UnityEditor.XR.OpenXR.OpenXRRuntimeSelector";
        Type selector = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(selectorName)).FirstOrDefault(type => type != null);
        if (selector == null)
            throw new InvalidOperationException("Le selecteur OpenXR n'est pas disponible.");

        // OpenXR 1.16 caches the Other runtime path when its selector is created.
        var property = selector.GetProperty("RuntimeDetectors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var runtimes = property?.GetValue(null) as IEnumerable;
        if (runtimes == null)
            throw new InvalidOperationException("Le selecteur de runtime OpenXR a change.");
        foreach (object detector in runtimes)
        {
            if (detector.GetType().Name != "OtherRuntime")
                continue;
            detector.GetType().GetMethod("SetOtherRuntimeJsonPath")?.Invoke(detector, new object[] { path });
            return;
        }
        throw new InvalidOperationException("Le runtime OpenXR Other est introuvable.");
    }
}
