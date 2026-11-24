using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Wygląd wierszy na listach w AudioManagerze: zamiast "Element 0" i rozwijanej listy zdarzeń widać po lewej nazwę zdarzenia
// z gry (np. Ui Click), a po prawej pole na event z FMOD-a. Zdarzenie ustawia się samo, więc nie da się go pomylić.
// Ten plik działa tylko w edytorze i nie trafia do gry.
[CustomPropertyDrawer(typeof(AudioEventEntry))]
[CustomPropertyDrawer(typeof(MusicEntry))]
[CustomPropertyDrawer(typeof(PickupSoundEntry))]
public class AudioEntryDrawer : PropertyDrawer
{
    static SerializedProperty IdOf(SerializedProperty entry)
    {
        return entry.FindPropertyRelative("id") ?? entry.FindPropertyRelative("track");
    }

    static string NameOf(SerializedProperty entry)
    {
        SerializedProperty id = IdOf(entry);
        string[] names = id != null ? id.enumDisplayNames : null;

        return names != null && id.enumValueIndex >= 0 && id.enumValueIndex < names.Length ? names[id.enumValueIndex] : "?";
    }

    // Wiersz przedmiotu ma dwa pola: dźwięk zebrania i dźwięk użycia.
    static bool IsPickup(SerializedProperty entry)
    {
        return entry.FindPropertyRelative("item") != null;
    }

    static string PickupName(SerializedProperty entry)
    {
        Object item = entry.FindPropertyRelative("item").objectReferenceValue;
        return item != null ? item.name : "(brak przedmiotu)";
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (IsPickup(property))
        {
            DrawPickup(position, property);
            return;
        }

        // bez FMOD-a pole eventu nie istnieje (jest dopiero w paczce FMOD for Unity)
        SerializedProperty reference = property.FindPropertyRelative("eventRef");
        string eventName = NameOf(property);

        if (reference != null)
        {
            EditorGUI.PropertyField(position, reference, new GUIContent(eventName));
        }
        else
        {
            EditorGUI.LabelField(position, eventName, "najpierw zaimportuj FMOD for Unity");
        }
    }

    static void DrawPickup(Rect position, SerializedProperty property)
    {
        string name = PickupName(property);
        SerializedProperty collect = property.FindPropertyRelative("collect");
        SerializedProperty use = property.FindPropertyRelative("use");

        if (collect == null || use == null)
        {
            EditorGUI.LabelField(position, name, "najpierw zaimportuj FMOD for Unity");
            return;
        }

        float line = EditorGUI.GetPropertyHeight(collect, GUIContent.none);
        Rect first = new Rect(position.x, position.y, position.width, line);
        Rect second = new Rect(position.x, position.y + line + EditorGUIUtility.standardVerticalSpacing, position.width, line);

        EditorGUI.PropertyField(first, collect, new GUIContent(name + " (zebranie)"));
        EditorGUI.PropertyField(second, use, new GUIContent(name + " (użycie)"));
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (IsPickup(property))
        {
            SerializedProperty collect = property.FindPropertyRelative("collect");
            return collect != null
                ? EditorGUI.GetPropertyHeight(collect, GUIContent.none) * 2f + EditorGUIUtility.standardVerticalSpacing * 2f
                : EditorGUIUtility.singleLineHeight;
        }

        SerializedProperty reference = property.FindPropertyRelative("eventRef");
        return reference != null ? EditorGUI.GetPropertyHeight(reference, GUIContent.none) : EditorGUIUtility.singleLineHeight;
    }
}
