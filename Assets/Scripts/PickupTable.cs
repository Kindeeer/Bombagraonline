using System;
using System.Collections.Generic;
using UnityEngine;

// Lista przedmiotów w grze i szansa na drop z bloków. Gra wczytuje plik Assets/Resources/PickupTable.
[CreateAssetMenu(fileName = "PickupTable", menuName = "Bomb Game/Pickup Table", order = 1)]
public class PickupTable : ScriptableObject
{
    public const string ResourcePath = "PickupTable";

    [Header("Drop From Destroyed Blocks")]
    [Range(0f, 1f)] public float blockDropChance = 0.3f;
    [Tooltip("Seconds a new bubble ignores fire (otherwise the blast that broke the block would pop it).")]
    [Min(0f)] public float fireImmuneSeconds = 0.7f;

    [Header("Pickups (drag new ones here)")]
    public List<PickupDefinition> items = new List<PickupDefinition>();

    public PickupDefinition PickFromBlocks()
    {
        return Pick(item => item.weightFromBlocks);
    }

    public PickupDefinition PickFromFountain()
    {
        return Pick(item => item.weightFromFountain);
    }

    PickupDefinition Pick(Func<PickupDefinition, int> weightOf)
    {
        int total = 0;

        foreach (PickupDefinition item in items)
        {
            if (item != null)
            {
                total += Mathf.Max(0, weightOf(item));
            }
        }

        if (total <= 0)
        {
            return null;
        }

        int roll = UnityEngine.Random.Range(0, total);

        foreach (PickupDefinition item in items)
        {
            if (item == null)
            {
                continue;
            }

            int weight = Mathf.Max(0, weightOf(item));

            if (roll < weight)
            {
                return item;
            }

            roll -= weight;
        }

        return null;
    }
}
