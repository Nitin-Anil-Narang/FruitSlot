# 🎰 Fruit Slot Machine (Unity 2D)

A polished, responsive 3-reel classic fruit slot machine built in Unity. This project was developed as part of the **Unity Slot Game Assignment**, focusing on clean object-oriented architecture, authentic casino RNG mathematics, dynamic reel physics, and responsive game feel.

---

## 🎮 Game Overview

The game simulates an authentic physical casino slot cabinet with modern digital feedback:
* **Core Loop:** Set your bet, pull the mechanical lever (or hit Spin), watch the reels spin with staggered suspense timing, and collect payouts when 3 matching symbols land on the center payline.
* **Betting Tiers:** Cycle through configurable bet amounts ($10, $25, $50, $100, $250, $500) or hit **MAX BET**.
* **Bankroll System:** Persistent wallet saved via `PlayerPrefs` across scenes, featuring a low-funds warning and top-up modal so the player can keep testing.
* **Audio System:** Dedicated SFX for lever pulls, reel spins, mechanical reel stops, coin showers, and jackpot fanfares.

---

## 🚀 How to Run the WebGL Build

The compiled WebGL build is included directly in the repository under the **`FruitTest/`** folder.

Because modern web browsers enforce strict CORS policies on local files (`file:///`), WebAssembly builds must be served via a local HTTP server:

### Option 1: Quick Terminal Server (Python)
1. Open your terminal and navigate to the project directory:
   ```bash
   cd FruitTest
   python3 -m http.server 8080
   ```
2. Open your browser and go to:
   ```
   http://localhost:8080
   ```

### Option 2: Node / NPM
```bash
npx serve FruitTest
```

### Running in the Unity Editor
* **Unity Version:** Unity 6 (`6000.6.10f1`) or higher (2D URP).
* Open the project in Unity.
* Navigate to `Assets/Scenes/StartScene.unity` and press **Play**.

---

## 🕹️ Controls

| Action | Control |
| :--- | :--- |
| **Pull Lever / Spin** | Click physical Lever, click **SPIN** button, or press **Spacebar** |
| **Adjust Bet** | Click **+** / **-** buttons or **MAX BET** |
| **Developer / Testing Panel** | Press **F1** or **`~` (Tilde)** on keyboard |
| **Rules / Paytable** | Click **RULES** button |

---

## 🧠 Architectural Approach & Design Decisions

### 1. Separation of Concerns (Decoupled MVC Pattern)
Rather than writing a monolithic script, I separated the game into distinct, single-responsibility components:
* **`SlotMachineController.cs` (Controller):** Acts as the coordinator. Manages betting states (`Idle`, `Spinning`, `Evaluating`, `Payout`), queries the RNG, triggers reel spins, and handles payout routines.
* **`Reel.cs` (View / Physics):** Controls individual reel motion, symbol wrapping, and easing. It is completely decoupled from win evaluation and communicates its state back to the controller via C# events (`OnReelStopped`).
* **`PlayerWallet.cs` (Model / State):** Persistent singleton that manages balance deductions, winnings, and storage via `PlayerPrefs`.
* **`AudioManager.cs` (Audio Service):** Centralized sound manager with one-shot playback and mute toggle.

### 2. Industry-Standard RNG Mathematics (Pre-Determined Outcomes)
In authentic casino slot architecture, reels do not physically decide the outcome when they stop. Instead:
1. The moment the player initiates a spin, the controller computes the target outcome **upfront** using weighted probabilities.
2. The targets are passed to each `Reel` instance.
3. The visual reel spin is purely a graphical presentation and suspense-building reveal of the calculated outcome.

**Weighted Probability Distribution:**
* High-paying symbols (e.g., Lucky 7, Golden Bell) have lower weights in the catalog.
* Common symbols (e.g., Cherry, Plum) have higher weights.
* **Near-Miss Logic:** On losing spins, there is a tuned probability where Reel 1 and Reel 2 match while Reel 3 lands on a different symbol. This recreates the authentic "near-miss" psychological suspense of real slot machines without altering the true win percentage.

### 3. Reel Animation & "Game Feel" (4-Phase Motion System)
To make the reels feel tactile and mechanical rather than static:
```
[Phase 1: Anticipation] ➔ [Phase 2: High-Speed Loop] ➔ [Phase 3: Cubic Ease-Out] ➔ [Phase 4: Elastic Bounce]
```
1. **Phase 1: Anticipation (Pullback / 120ms):** The reel jerks slightly upward before spinning downwards, simulating the tension of mechanical gears.
2. **Phase 2: High-Speed Loop & Object Pooling:** Rather than instantiating hundreds of symbol prefabs (which causes garbage collection spikes), each reel uses a fixed pool of **5 symbol slots**. As a slot moves below the bottom threshold, it wraps around to the top and is recycled with a new random sprite.
3. **Phase 3: Smooth Cubic Deceleration:** When braking, the reel calculates the exact modulo distance needed to align with the center payline and smoothly decelerates using a cubic ease-out curve (`1 - (1 - t)³`).
4. **Phase 4: Elastic Bounce (Overshoot):** The reel slightly overshoots the payline and bounces back into place using a damped sine wave formula (`-sin(t * π) * (1 - t)`), giving a physical snap when it latches.
5. **Staggered Stop Timing:** Reels stop sequentially (Reel 1 at 1.2s, Reel 2 at 1.55s, Reel 3 at 1.9s) to build anticipation.

---

## 🌟 Bonus Features & Additions

* **Free Spins Bonus Mode:** Hitting special bonus symbols triggers **Free Spins** with a **2x multiplier** on all winnings. The machine automatically spins through the awarded free spins.
* **Rolling Win Counter:** When winning, numbers do not instantly change; a count-up coroutine (`Mathf.Lerp`) rolls the numbers up over time to maximize satisfaction.
* **Tiered Celebration Popups:** Distinct visual modals trigger for Big Wins ($25\times$+) and Jackpots ($50\times$+).
* **Interactive Cabinet Lever:** Features two-frame custom sprite states and vertical pull offset to mimic pulling a physical mechanical slot handle.
* **Developer Test Panel (F1 / `~`):** A hidden developer overlay allowing evaluators to force immediate symbol wins (777, Bell, Cherry, Bar), test near-miss sequences, or inject test bankroll funds without waiting on RNG.

---

## 📁 Project Structure

```text
Assets/
├── Animations/       # Animation clips & controllers
├── Prefabs/          # Reusable UI & game object prefabs
├── Resources/        # Dynamically loaded runtime audio clips
├── Scenes/
│   ├── StartScene.unity   # Splash menu, rules modal & bankroll setup
│   └── GameScene.unity    # Main slot machine gameplay scene
├── Scripts/
│   ├── AudioManager.cs            # Centralized SFX playback & audio toggle
│   ├── PlayerWallet.cs            # Bankroll management & PlayerPrefs persistence
│   ├── Reel.cs                    # 4-phase reel animation, symbol wrapping & bounce
│   ├── SlotMachineController.cs   # Game state coordinator, RNG & win evaluation
│   ├── StartMenuController.cs     # Main menu UI logic
│   └── SymbolData.cs              # Symbol data model (weight, payout, sprite)
├── Sounds/           # Source audio assets (.wav files)
├── Sprites/          # Slot cabinet, reels, and fruit symbol artwork
└── UI/               # Button states, popups, and gradient backgrounds
FruitTest/            # Playable WebGL build files (index.html, wasm, data)
```

---

## 🏆 Evaluation Summary

| Requirement | Implementation Details |
| :--- | :--- |
| **Winning Logic** | Evaluates center payline matches across all 3 reels with symbol multiplier payouts. |
| **Smooth Reel Animations** | 4-phase physical motion (pullback, 5-slot recycled loop, cubic ease-out, elastic bounce). |
| **Clean Symbol Display** | Pixel-aligned vertical symbol spacing with automatic grid centering on stop. |
| **Randomized Outcomes** | Pre-determined weighted RNG with fair hit frequencies and realistic near-miss logic. |
| **Bonus Features** | Free spins with 2x multiplier, rolling win counter, celebration modals, dev test panel. |
| **Code Structure & OOP** | Modular architecture, decoupled events, persistent singleton wallet, and descriptive naming. |
