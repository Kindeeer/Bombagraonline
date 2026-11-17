using System.Collections.Generic;
using UnityEngine;

// Mina: pułapka kładziona przedmiotem specjalnym. Leży na polu i po chwili się uzbraja. Wybucha jak mała bomba,
// gdy wejdzie na nią przeciwnik albo gdy dosięgnie jej ogień. Ten, kto ją położył (i jego drużyna), widzi ją cały czas
// i może po niej chodzić. Przeciwnicy widzą ją tylko przez chwilę po położeniu, potem jest prawie niewidoczna.
//
// Online: położenie miny leci do wszystkich. O wejściu na minę decyduje telefon gracza, który na nią wszedł
// (tylko on zna dokładnie swoją pozycję), i mówi pozostałym, że mina wybuchła.
public class Mine : MonoBehaviour
{
    // tyle sekund po położeniu mina jeszcze nie wybucha i widać ją u wszystkich
    public const float ArmDelay = 1.2f;
    // zasięg wybuchu w polach (mniejszy niż zwykła bomba)
    public const int BlastRange = 1;
    // tak słabo widzą uzbrojoną minę przeciwnicy
    const float HiddenAlpha = 0.14f;

    static readonly Dictionary<Vector2Int, Mine> mines = new Dictionary<Vector2Int, Mine>();

    Vector2Int cell;
    int ownerSlot = -1;
    Bomb bombPrefab;
    float age;
    bool exploded;
    SpriteRenderer body;
    SpriteRenderer lampLight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        mines.Clear();
    }

    public static bool Has(Vector2Int cell)
    {
        return mines.ContainsKey(cell);
    }

    // Kładzie minę na polu. Zwraca false, gdy na polu już jest mina albo bomba.
    public static bool Place(Vector2Int cell, int ownerSlot, Bomb bombPrefab)
    {
        GridMap map = GridMap.Instance;

        if (map == null || bombPrefab == null || mines.ContainsKey(cell) || BombRegistry.HasBomb(cell))
        {
            return false;
        }

        GameObject go = new GameObject("Mine");
        go.transform.position = map.GridToWorld(cell) + Vector3.down * 0.12f;

        Mine mine = go.AddComponent<Mine>();
        mine.cell = cell;
        mine.ownerSlot = ownerSlot;
        mine.bombPrefab = bombPrefab;

        mine.body = go.AddComponent<SpriteRenderer>();
        mine.body.sprite = PickupArt.Icon(PickupIcon.Mine);
        mine.body.sortingOrder = 0;
        go.transform.localScale = Vector3.one * 1.05f;

        // czerwona lampka na środku: miga szybko przy uzbrajaniu, potem powoli
        GameObject lamp = new GameObject("Light");
        lamp.transform.SetParent(go.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        mine.lampLight = lamp.AddComponent<SpriteRenderer>();
        mine.lampLight.sprite = UiKit.CircleSprite();
        mine.lampLight.sortingOrder = 0;

        float size = mine.lampLight.sprite != null ? mine.lampLight.sprite.bounds.size.x : 1f;
        lamp.transform.localScale = Vector3.one * (0.16f / Mathf.Max(0.01f, size));

        mines[cell] = mine;
        GameAudio.Play(AudioEvent.BombPlace, go.transform.position);
        return true;
    }

    // Mina na tym polu wybucha (wiadomość z sieci: wszedł na nią gracz na innym telefonie).
    public static void ExplodeAt(Vector2Int cell)
    {
        if (mines.TryGetValue(cell, out Mine mine) && mine != null)
        {
            mine.Explode(false);
        }
    }

    void OnDestroy()
    {
        if (mines.TryGetValue(cell, out Mine mine) && mine == this)
        {
            mines.Remove(cell);
        }
    }

    void Update()
    {
        if (exploded)
        {
            return;
        }

        age += Time.deltaTime;
        bool armed = age >= ArmDelay;

        UpdateLook(armed);

        MatchManager match = MatchManager.Instance;
        GridMap map = GridMap.Instance;

        if (!armed || match == null || map == null || match.RoundOver)
        {
            return;
        }

        // ogień z bomby obok też odpala minę (u każdego gracza liczone tak samo, bo wszyscy mają te same bomby)
        if (map.IsOnFire(cell))
        {
            Explode(false);
            return;
        }

        foreach (Combatant combatant in match.Slots)
        {
            // cudzy gracz online sam ogłasza, że wszedł na minę; właściciel i jego drużyna chodzą po niej bezpiecznie
            if (combatant == null || !combatant.IsAlive || combatant.remote || combatant.slot == ownerSlot
                || Teams.AlliedSlots(combatant.slot, ownerSlot))
            {
                continue;
            }

            if (map.WorldToGrid(combatant.health.transform.position) == cell)
            {
                Explode(true);
                return;
            }
        }
    }

    // Wygląd: swoi widzą minę wyraźnie, przeciwnicy tylko podczas uzbrajania, potem jest ledwo widoczna.
    void UpdateLook(bool armed)
    {
        bool mineOrAllied = ownerSlot == 0 || Teams.AlliedSlots(0, ownerSlot);
        float alpha = mineOrAllied || !armed ? 1f : Mathf.Lerp(1f, HiddenAlpha, Mathf.Clamp01((age - ArmDelay) / 0.6f));

        body.color = new Color(1f, 1f, 1f, alpha);

        float blink = Mathf.Repeat(age * (armed ? 1.2f : 6f), 1f) < 0.5f ? 1f : 0.25f;
        lampLight.color = new Color(1f, 0.2f, 0.15f, alpha * blink);
    }

    // announce = true: mina wybuchła, bo wszedł na nią gracz z tego telefonu, więc mówimy o tym pozostałym.
    void Explode(bool announce)
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        mines.Remove(cell);

        if (announce && OnlineMatch.Active)
        {
            OnlineMatch.SendMineHit(cell);
        }

        // wybuch robi zwykła bomba z bardzo krótkim lontem: ten sam ogień, niszczenie bloków i liczenie zabójstw
        if (BombRegistry.TryGet(cell, out Bomb existing))
        {
            existing.Trigger();
        }
        else if (bombPrefab != null)
        {
            Bomb bomb = Instantiate(bombPrefab);
            bomb.Init(cell, 0.05f, BlastRange);
            bomb.ownerSlot = ownerSlot;
        }

        Destroy(gameObject);
    }
}
