using UnityEditor;
using UnityEngine;

// Wygląd wierszy na listach w AudioManagerze: zamiast "Element 0" i rozwijanej listy zdarzeń widać po lewej nazwę zdarzenia
// z gry (np. Ui Click), a po prawej pole na event z FMOD-a. Zdarzenie ustawia się samo, więc nie da się go pomylić.
// Ten plik działa tylko w edytorze i nie trafia do gry.
[CustomPropertyDrawer(typeof(AudioEventEntry))]
[CustomPropertyDrawer(typeof(MusicEntry))]
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

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
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

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty reference = property.FindPropertyRelative("eventRef");
        return reference != null ? EditorGUI.GetPropertyHeight(reference, GUIContent.none) : EditorGUIUtility.singleLineHeight;
    }
}
