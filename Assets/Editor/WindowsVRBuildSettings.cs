using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;

// Le build Windows attend le choix du joueur ; Play dans l'éditeur garde son fonctionnement habituel.
[InitializeOnLoad]
internal sealed class WindowsVRBuildSettings : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string RestoreKey = "FruitNinja.WindowsVR.RestoreAutomaticStartup";
    private const string ValueKey = "FruitNinja.WindowsVR.AutomaticStartup";
    public int callbackOrder => -1000;

    static WindowsVRBuildSettings()
    {
        EditorApplication.update += RestoreAfterFailedBuild;
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
        var settings = Settings;
        if (settings == null) throw new BuildFailedException("La configuration XR Windows est manquante.");
        SessionState.SetBool(ValueKey, settings.InitManagerOnStart);
        SessionState.SetBool(RestoreKey, true);
        settings.InitManagerOnStart = false;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.StandaloneWindows64)
        {
            // OpenXR doit aussi attendre le menu du côté natif, avant le chargement des scripts.
            string bootConfig = Path.Combine(Path.GetDirectoryName(report.summary.outputPath),
                Path.GetFileNameWithoutExtension(report.summary.outputPath) + "_Data", "boot.config");
            File.WriteAllLines(bootConfig, File.ReadAllLines(bootConfig)
                .Where(line => !line.StartsWith("xrsdk-pre-init-library=")));
        }
        Restore();
    }

    private static XRGeneralSettings Settings =>
        XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);

    private static void RestoreAfterFailedBuild()
    {
        if (!BuildPipeline.isBuildingPlayer) Restore();
    }

    private static void Restore()
    {
        if (!SessionState.GetBool(RestoreKey, false)) return;
        var settings = Settings;
        if (settings == null) return;
        settings.InitManagerOnStart = SessionState.GetBool(ValueKey, true);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        SessionState.EraseBool(RestoreKey);
    }
}
