using System;
using UnityEngine;

/// <summary>
/// Defines the properties and payout rates for an individual slot machine symbol.
/// </summary>
[Serializable]
public class SymbolData
{
    [Header("Symbol Identity")]
    public int id;
    public string symbolName;
    public Sprite sprite;

    [Header("Payout & Odds")]
    [Tooltip("Multiplier applied to the current bet when 3 matching symbols land on the payline.")]
    public int payoutMultiplier = 10;

    [Tooltip("RNG weight for probability calculation (lower = rarer, higher = more frequent).")]
    [Range(1, 100)]
    public int weight = 10;

    [Tooltip("Is this symbol a special bonus/scatter symbol?")]
    public bool isBonus = false;
}
