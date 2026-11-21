using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls an individual slot reel: manages vertical symbol looping,
/// smooth acceleration, steady high-speed spin, deceleration, and elastic bounce easing.
/// </summary>
public class Reel : MonoBehaviour
{
    [Header("Reel Configuration")]
    [Tooltip("Vertical spacing between adjacent symbols in pixels.")]
    [SerializeField] private float symbolSpacing = 160f;

    [Tooltip("Maximum spinning speed in pixels per second.")]
    [SerializeField] private float maxSpinSpeed = 2200f;

    [Tooltip("Number of symbol rows visible and buffered (recommended: 5).")]
    [SerializeField] private int symbolSlotCount = 5;

    [Header("Component References")]
    [SerializeField] private RectTransform symbolContainer;

    // Events
    public event Action<Reel> OnReelStopped;

    // State
    public bool IsSpinning { get; private set; }
    public SymbolData StoppedSymbol { get; private set; }

    private List<Image> symbolImages = new List<Image>();
    private List<RectTransform> symbolRects = new List<RectTransform>();
    private SymbolData[] symbolCatalog;

    public void Initialize(SymbolData[] symbols, Sprite defaultSprite)
    {
        symbolCatalog = symbols;
        symbolImages.Clear();
        symbolRects.Clear();

        if (symbolContainer == null)
            symbolContainer = GetComponent<RectTransform>();

        // Find or create child symbol slots
        Image[] existingImages = symbolContainer.GetComponentsInChildren<Image>(true);
        if (existingImages.Length >= symbolSlotCount)
        {
            for (int i = 0; i < symbolSlotCount; i++)
            {
                symbolImages.Add(existingImages[i]);
                symbolRects.Add(existingImages[i].rectTransform);
            }
        }

        // Position slots vertically around center (0 is center payline)
        int half = symbolSlotCount / 2;
        for (int i = 0; i < symbolSlotCount; i++)
        {
            float yPos = (i - half) * symbolSpacing;
            symbolRects[i].anchoredPosition = new Vector2(0, yPos);

            if (symbols != null && symbols.Length > 0)
            {
                SymbolData initialSymbol = symbols[UnityEngine.Random.Range(0, symbols.Length)];
                symbolImages[i].sprite = initialSymbol.sprite;
                symbolImages[i].preserveAspect = true;
                if (i == half)
                {
                    StoppedSymbol = initialSymbol;
                }
            }
        }
    }

    /// <summary>
    /// Starts the reel spin coroutine with pre-determined target symbol and duration.
    /// </summary>
    public void Spin(SymbolData targetSymbol, float spinDuration, float delay = 0f)
    {
        if (IsSpinning) return;
        StartCoroutine(SpinRoutine(targetSymbol, spinDuration, delay));
    }

    private IEnumerator SpinRoutine(SymbolData targetSymbol, float spinDuration, float delay)
    {
        IsSpinning = true;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // --- Phase 1: Anticipation (Small initial pullback) ---
        float anticipationTime = 0.12f;
        float elapsed = 0f;
        float initialPullback = 20f;
        while (elapsed < anticipationTime)
        {
            elapsed += Time.deltaTime;
            float step = (initialPullback / anticipationTime) * Time.deltaTime;
            ShiftAllSymbols(step);
            yield return null;
        }

        // --- Phase 2: Acceleration & Steady Spin ---
        float currentSpeed = 0f;
        float timer = 0f;
        float totalSpinTime = Mathf.Max(0.8f, spinDuration);

        while (timer < totalSpinTime)
        {
            timer += Time.deltaTime;

            // Accelerate rapidly to max speed
            if (currentSpeed < maxSpinSpeed)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpinSpeed, maxSpinSpeed * 4f * Time.deltaTime);
            }

            ShiftAllSymbols(-currentSpeed * Time.deltaTime);
            WrapSymbolsRandomly();
            yield return null;
        }

        // --- Phase 3: Continuous Smooth Deceleration to Target Symbol ---
        int centerIndex = symbolSlotCount / 2;

        // Calculate distance required to bring the next symbol precisely onto the center payline (Y = 0)
        float currentCenterY = symbolRects[centerIndex].anchoredPosition.y;
        float mod = currentCenterY % symbolSpacing;
        if (mod < 0f) mod += symbolSpacing;
        float alignDistance = mod; // distance to move down to reach next exact grid line

        // Glide through 3 full symbol cycles + alignDistance for a cinematic ease-out
        float totalDecelDistance = alignDistance + (3f * symbolSpacing);
        float decelDuration = 0.55f;
        float decelElapsed = 0f;
        float prevTraveled = 0f;

        while (decelElapsed < decelDuration)
        {
            decelElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(decelElapsed / decelDuration);
            // Cubic ease-out curve for natural physical braking
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float currentTraveled = easeOut * totalDecelDistance;
            float step = currentTraveled - prevTraveled;
            prevTraveled = currentTraveled;

            ShiftAllSymbols(-step);
            WrapSymbolsRandomly();
            yield return null;
        }

        // Apply any remaining subpixel step to complete totalDecelDistance exactly
        float remainder = totalDecelDistance - prevTraveled;
        if (Mathf.Abs(remainder) > 0.0001f)
        {
            ShiftAllSymbols(-remainder);
            WrapSymbolsRandomly();
        }

        // Ensure winning target symbol is placed on center slot
        symbolImages[centerIndex].sprite = targetSymbol.sprite;

        // Precision grid alignment
        for (int i = 0; i < symbolSlotCount; i++)
        {
            int offsetFromCenter = i - centerIndex;
            symbolRects[i].anchoredPosition = new Vector2(0, offsetFromCenter * symbolSpacing);
        }

        // --- Phase 4: Elastic Overshoot & Settle (Game Feel) ---
        float overshootAmount = 18f;
        float settleDuration = 0.2f;
        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleDuration;
            float bounceOffset = -Mathf.Sin(t * Mathf.PI) * overshootAmount * (1f - t);

            for (int i = 0; i < symbolSlotCount; i++)
            {
                int offsetFromCenter = i - centerIndex;
                symbolRects[i].anchoredPosition = new Vector2(0, (offsetFromCenter * symbolSpacing) + bounceOffset);
            }

            yield return null;
        }

        // Final snap to exact resting position
        for (int i = 0; i < symbolSlotCount; i++)
        {
            int offsetFromCenter = i - centerIndex;
            symbolRects[i].anchoredPosition = new Vector2(0, offsetFromCenter * symbolSpacing);
        }

        StoppedSymbol = targetSymbol;
        IsSpinning = false;
        OnReelStopped?.Invoke(this);
    }

    private void ShiftAllSymbols(float deltaY)
    {
        for (int i = 0; i < symbolRects.Count; i++)
        {
            Vector2 pos = symbolRects[i].anchoredPosition;
            pos.y += deltaY;
            symbolRects[i].anchoredPosition = pos;
        }
    }

    private void WrapSymbolsRandomly()
    {
        float bottomThreshold = -((symbolSlotCount / 2) + 0.6f) * symbolSpacing;
        float wrapDistance = symbolSlotCount * symbolSpacing;

        for (int i = 0; i < symbolRects.Count; i++)
        {
            if (symbolRects[i].anchoredPosition.y < bottomThreshold)
            {
                Vector2 pos = symbolRects[i].anchoredPosition;
                pos.y += wrapDistance;
                symbolRects[i].anchoredPosition = pos;

                // Pick a new random symbol appearance when cycling through the top
                if (symbolCatalog != null && symbolCatalog.Length > 0)
                {
                    SymbolData randomSym = symbolCatalog[UnityEngine.Random.Range(0, symbolCatalog.Length)];
                    symbolImages[i].sprite = randomSym.sprite;
                }
            }
        }
    }
}
