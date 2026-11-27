using System.Collections.Generic;
using UnityEngine;

// Chromy zdobyte ze skrzynki, ale jeszcze nieobejrzane w EDIT. Pokazuje je czerwona kropka "nowe": na przycisku SKINS w menu
// głównym, na karcie zwierzaka i przy samej chromie w EDIT. Kropki danego zwierzaka znikają, gdy gracz wyjdzie z jego EDIT.
// Zapisane na urządzeniu (PlayerPrefs), tak jak same chromy.
public static class NewChromas
{
    const string Key = "new_chromas";

    static HashSet<string> items;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        items = null;
    }

    static HashSet<string> Items
    {
        get
        {
            if (items == null)
            {
                items = new HashSet<string>();

                foreach (string token in PlayerPrefs.GetString(Key, "").Split(';'))
                {
                    if (token.Length > 0)
                    {
                        items.Add(token);
                    }
                }
            }

            return items;
        }
    }

    static string Token(string skinLabel, int chroma)
    {
        return skinLabel + "#" + chroma;
    }

    public static bool Any => Items.Count > 0;

    public static void Mark(string skinLabel, int chroma)
    {
        if (Items.Add(Token(skinLabel, chroma)))
        {
            Save();
        }
    }

    public static bool IsNew(string skinLabel, int chroma)
    {
        return Items.Contains(Token(skinLabel, chroma));
    }

    public static bool AnyFor(string skinLabel)
    {
        string prefix = skinLabel + "#";

        foreach (string token in Items)
        {
            if (token.StartsWith(prefix))
            {
                return true;
            }
        }

        return false;
    }

    public static void ClearSkin(string skinLabel)
    {
        string prefix = skinLabel + "#";

        if (Items.RemoveWhere(token => token.StartsWith(prefix)) > 0)
        {
            Save();
        }
    }

    static void Save()
    {
        PlayerPrefs.SetString(Key, string.Join(";", Items));
        PlayerPrefs.Save();
    }
}
