using System.IO;
using UnityEditor;
using UnityEngine;

// Tworzy nowe przedmioty do podnoszenia jako pliki w Assets/Data/Pickups i dopisuje je do tabeli Assets/Resources/PickupTable.
// Dzieje się to samo, po każdej kompilacji i przy wciśnięciu Play, więc nie trzeba niczego klikać. Istniejące pliki
// zostają bez zmian (swoje wagi i ustawienia możesz edytować w plikach). Skasowany plik wróci przy następnej kompilacji.
// Ten plik działa tylko w edytorze i nie trafia do gry.
[InitializeOnLoad]
public static class PickupSetup
{
    const string Folder = "Assets/Data/Pickups";

    static PickupSetup()
    {
        EditorApplication.delayCall += () => CreateNewPickups(true);

        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CreateNewPickups(true);
            }
        };
    }

    static readonly Color BadTint = new Color(1f, 0.62f, 0.62f);

    class Spec
    {
        public string file;
        public string displayName;
        public string description;
        public PickupIcon icon;
        public PickupMode mode = PickupMode.Permanent;
        public int extraBombs;
        public int extraRange;
        public int speedSteps;
        public float fuseChange;
        public float duration = 8f;
        public SpecialItem special = SpecialItem.None;
        public PickupEffect effect = PickupEffect.None;
        public int weightBlocks = 10;
        public int weightFountain = 15;
        public Color tint = Color.white;
    }

    // quiet = bez komunikatów, gdy nie było nic do zrobienia (wywołanie automatyczne).
    internal static void CreateNewPickups(bool quiet = false)
    {
        Spec[] specs =
        {
            new Spec { file = "Shield", displayName = "Tarcza", description = "Chroni przed jednym wybuchem.",
                icon = PickupIcon.Shield, mode = PickupMode.Permanent, effect = PickupEffect.Shield, weightBlocks = 12, weightFountain = 20 },
            new Spec { file = "Star", displayName = "Gwiazda", description = "Przez 5 sekund wybuchy Ci nie szkodzą.",
                icon = PickupIcon.Star, mode = PickupMode.Timed, duration = 5f, effect = PickupEffect.Invincibility, weightBlocks = 6, weightFountain = 20 },
            new Spec { file = "Kick", displayName = "Kopanie bomb", description = "Idź w bombę, żeby ją kopnąć. Jedno kopnięcie, kumulują się (do 3).",
                icon = PickupIcon.Boot, mode = PickupMode.Permanent, effect = PickupEffect.Kick, weightBlocks = 12, weightFountain = 20 },
            new Spec { file = "MegaBlast", displayName = "Mega wybuch", description = "Następna bomba ma zasięg o 3 większy. Jedno użycie.",
                icon = PickupIcon.MegaBomb, mode = PickupMode.NextBomb, extraRange = 3, weightBlocks = 10, weightFountain = 20 },
            new Spec { file = "BombStorm", displayName = "Burza bomb", description = "Przez 10 sekund możesz mieć o 3 bomby więcej.",
                icon = PickupIcon.BombStorm, mode = PickupMode.Timed, duration = 10f, extraBombs = 3, weightBlocks = 10, weightFountain = 15 },
            new Spec { file = "Turbo", displayName = "Turbo", description = "Przez 6 sekund biegasz dużo szybciej.",
                icon = PickupIcon.Bolt, mode = PickupMode.Timed, duration = 6f, speedSteps = 3, weightBlocks = 10, weightFountain = 15 },
            new Spec { file = "ShortFuse", displayName = "Krótki lont", description = "Przez 8 sekund Twoje bomby wybuchają szybciej.",
                icon = PickupIcon.Clock, mode = PickupMode.Timed, duration = 8f, fuseChange = -1f, weightBlocks = 8, weightFountain = 15 },
            new Spec { file = "Teleport", displayName = "Zamiana miejsc", description = "Zamień się miejscami z losowym przeciwnikiem. Jedno użycie.",
                icon = PickupIcon.Swirl, mode = PickupMode.NextBomb, special = SpecialItem.Teleport, weightBlocks = 8, weightFountain = 20 },
            new Spec { file = "CurseDizzy", displayName = "Zawroty głowy", description = "Przez 8 sekund sterowanie jest odwrócone.",
                icon = PickupIcon.Dizzy, mode = PickupMode.Timed, duration = 8f, effect = PickupEffect.ReverseControls, weightBlocks = 6, weightFountain = 10, tint = BadTint },
            new Spec { file = "Nuke", displayName = "Atomówka", description = "Giga rzadka. Stoisz w miejscu i klikasz, gdzie ją zrzucić. Ogromny wybuch.",
                icon = PickupIcon.Nuke, mode = PickupMode.NextBomb, special = SpecialItem.Nuke, weightBlocks = 1, weightFountain = 2 },
            new Spec { file = "CurseAutoBombs", displayName = "Biegunka bomb", description = "Przez 6 sekund sam zostawiasz bomby.",
                icon = PickupIcon.Skull, mode = PickupMode.Timed, duration = 6f, effect = PickupEffect.AutoBombs, weightBlocks = 6, weightFountain = 10, tint = BadTint }
        };

        Directory.CreateDirectory(Folder);

        PickupTable table = Resources.Load<PickupTable>(PickupTable.ResourcePath);

        if (table == null)
        {
            if (!quiet)
            {
                Debug.LogError("Bomb Game: nie znalazłem tabeli przedmiotów (Assets/Resources/PickupTable).");
            }

            return;
        }

        int created = 0;
        int added = 0;

        foreach (Spec spec in specs)
        {
            string path = Folder + "/" + spec.file + ".asset";
            PickupDefinition definition = AssetDatabase.LoadAssetAtPath<PickupDefinition>(path);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<PickupDefinition>();
                definition.displayName = spec.displayName;
                definition.description = spec.description;
                definition.placeholderIcon = spec.icon;
                definition.bubbleTint = spec.tint;
                definition.mode = spec.mode;
                definition.extraBombs = spec.extraBombs;
                definition.extraRange = spec.extraRange;
                definition.speedSteps = spec.speedSteps;
                definition.fuseChange = spec.fuseChange;
                definition.durationSeconds = spec.duration;
                definition.special = spec.special;
                definition.effect = spec.effect;
                definition.uses = 1;
                definition.weightFromBlocks = spec.weightBlocks;
                definition.weightFromFountain = spec.weightFountain;

                AssetDatabase.CreateAsset(definition, path);
                created++;
            }

            if (!table.items.Contains(definition))
            {
                table.items.Add(definition);
                added++;
            }
        }

        if (created == 0 && added == 0 && quiet)
        {
            return;
        }

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Debug.Log("Bomb Game: nowe przedmioty. Utworzone pliki: " + created + ", dopisane do tabeli: " + added
            + ". Szanse na drop zmienisz w plikach w " + Folder + " (Weight From Blocks i Weight From Fountain).");
    }
}
