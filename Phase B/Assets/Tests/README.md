# Automated Tests

This folder holds the project's automated test layer, built on the **Unity Test Framework** (NUnit).

## Why this does not affect the game

- Test code lives in its own **assembly definitions** (`StressTrainer.EditModeTests`,
  `StressTrainer.PlayModeTests`). Unity automatically **excludes test assemblies from
  player builds**, so none of this ships in the actual app or changes runtime behavior.
- The only production change made to support testing was isolating two **pure,
  dependency-free** logic files into a small runtime assembly,
  `Assets/Scripts/Core/StressTrainer.Core.asmdef`:
  - `StressChangeIndexCalculator.cs` (the core SCI algorithm)
  - `SimulationRunOutcome.cs` (mission timing/pace data)
  Their file GUIDs were preserved during the move, and the assembly is
  `autoReferenced`, so every existing script keeps compiling and behaving exactly
  as before. No game logic was modified.

## How to run the tests

1. Open the project in Unity.
2. Menu: **Window > General > Test Runner**.
3. Choose the **EditMode** tab (fast, pure-logic tests) or **PlayMode** tab
   (runs inside the player loop), then click **Run All**.

You can also run from the command line:

```bash
Unity.exe -runTests -batchmode -projectPath "Phase B" -testPlatform EditMode -testResults results-editmode.xml
Unity.exe -runTests -batchmode -projectPath "Phase B" -testPlatform PlayMode -testResults results-playmode.xml
```

## What is covered today

- **EditMode**
  - `StressChangeIndexCalculatorTests` — SCI percentage math, divide-by-zero guard,
    band classification thresholds (Low / Moderate / High), labels and band colors.
  - `SimulationRunOutcomeTests` — value clamping, reason trimming, fast/slow pace logic.
- **PlayMode**
  - `SmokePlayModeTests` — verifies the PlayMode pipeline (frame stepping) and that
    pure logic behaves consistently inside the running player loop.

## How to add more tests

- Put fast logic tests under `EditMode/`, and tests that need the runtime loop,
  physics, or scene objects under `PlayMode/`.
- To test a class that currently lives in the default `Assembly-CSharp`, that class
  must belong to an assembly definition the test assembly can reference (Unity test
  assemblies cannot reference `Assembly-CSharp`). The low-risk pattern used here is
  to move small, pure, self-contained logic into `Assets/Scripts/Core` and reference
  `StressTrainer.Core` from the test assembly.
