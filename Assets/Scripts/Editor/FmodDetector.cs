using System;
using UnityEditor;
using UnityEditor.Build;

// Sam włącza obsługę FMOD w kodzie gry: gdy w projekcie jest FMOD for Unity, dopisuje symbol FMOD_PRESENT
// (Project Settings > Player > Scripting Define Symbols), a gdy go nie ma, usuwa.
// Dzięki temu GameAudio działa z FMOD bez żadnych ręcznych kroków, a bez FMOD gra się kompiluje.
// Ten plik działa tylko w edytorze i nie trafia do gry.
[InitializeOnLoad]
public static class FmodDetector
{
    const string Symbol = "FMOD_PRESENT";

    static readonly NamedBuildTarget[] Targets =
    {
        NamedBuildTarget.Standalone,
        NamedBuildTarget.Android,
        NamedBuildTarget.iOS
    };

    static FmodDetector()
    {
        // po załadowaniu edytora, nie w trakcie ładowania domeny
        EditorApplication.delayCall += Sync;
    }

    static bool FmodInstalled()
    {
        // FMOD for Unity ma klasę FMODUnity.RuntimeManager w zestawie FMODUnity i leży w Assets/Plugins/FMOD
        return Type.GetType("FMODUnity.RuntimeManager, FMODUnity", false) != null
            || AssetDatabase.IsValidFolder("Assets/Plugins/FMOD");
    }

    static void Sync()
    {
        bool installed = FmodInstalled();

        foreach (NamedBuildTarget target in Targets)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            bool has = Array.IndexOf(defines.Split(';'), Symbol) >= 0;

            if (installed == has)
            {
                continue;
            }

            string updated = installed
                ? (defines.Length > 0 ? defines + ";" + Symbol : Symbol)
                : string.Join(";", Array.FindAll(defines.Split(';'), part => part != Symbol && part.Length > 0));

            PlayerSettings.SetScriptingDefineSymbols(target, updated);
        }
    }
}
