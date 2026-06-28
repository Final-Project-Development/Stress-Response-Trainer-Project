# Handoff — Phase B (Sapir → Partner)

This build is the **desktop training version**. It is fully playable without a smartwatch or VR headset.
Your task: connect **live biometrics** and **VR** on top of this baseline.

## What works now (no hardware)

- Full flow: Gate → Login (optional) → Intro → 60s calibration → Level select → Sim 1 / Sim 2 / Environment learning
- Mission gameplay, timers, results (Result / Recommendations / SCI graph), profile + session history
- **SCI / HRV** from `MockPhysiologySource` (synthetic stress response)
- Live HR chart UI exists but expects UDP on port **5055** (watch bridge)

**Branch:** `unity-game`  
**Scene:** `Assets/Scenes/MainScene.unity`  
**Unity:** `6000.2.15f1` (see `ProjectSettings/ProjectVersion.txt`)

## Controls (desktop)

| Input | Action |
|--------|--------|
| WASD + mouse | Move / look |
| E | Interact (pickups, door, phone, casualty) |
| Escape | Pause / overlays (via `UINavigationManager`) |

## Smartwatch integration (your work)

Two UDP paths — do not confuse them:

| Port | Component | Purpose |
|------|-----------|---------|
| **5005** | `UDPReceiver` on BioMetrics | Android gateway → HR + HRV for **SCI** (`MockPhysiologySource`) |
| **5055** | `WorkoutHeartRateChartReceiver` | Watch timeline → **live HR chart** during simulations |

### Enable live gateway (SCI)

1. Open **MainScene** → find **MockPhysiologySource** (or object with `UDPReceiver`).
2. Set `UDPReceiver.expectGatewayTraffic = true` when gateway should be required.
3. Set `MockPhysiologySource.useLiveUdpWhenAvailable = true`.
4. Run Android bridge from `Fit3UnityBridge/` (same Wi‑Fi as PC; firewall allows UDP **5005**).
5. Packet formats: see `UDPReceiver.cs` (`HR:75`, `HR:75,HRV:52.3`, or JSON).

### Enable live watch chart

1. `WorkoutHeartRateChartReceiver.unityListenPort` = **5055** (default).
2. Send workout HR timeline JSON to PC (see `Fit3UnityBridge/PcBridge` or Samsung bridge).
3. Top bar pill (`TopBarWatchStatusController`) should show **Connected** when fresh packets arrive.

Until enabled, status shows **simulated HR/HRV** — this is expected.

## VR integration (your work)

Current player: **`SimpleFPSController`** (desktop FPS, not XR rig).

Suggested steps:

1. Add **XR Origin** (OpenXR already in project packages).
2. Replace or wrap desktop look/move with XR controller / head tracking.
3. Map **E interact** to XR trigger or UI ray — see `PlayerInteract`, `MissionInteractProximity`.
4. Test `UINavigationManager` cursor lock vs VR menu.
5. Update README architecture line: desktop handoff → VR build.

VirtualGrasp package is present under `Assets/com.gleechi.unity.virtualgrasp` if needed for hands.

## Key scripts

| Area | Files |
|------|--------|
| Flow / phases | `TrainingFlowController.cs` |
| Sim 1 missions | `GameManager.cs`, `ShelterTrigger.cs` |
| Sim 2 missions | `PublicPhoneBoothMission.cs`, `WoundedMan.cs` |
| SCI + recording | `SessionStressRecorder.cs`, `StressChangeIndexCalculator.cs` |
| Results text | `StressRecommendations.cs`, `SimulationResultsPanelsConfig.cs` |
| Results graphs | `SimpleStressLineGraph.cs`, `TextureLineChartRenderer.cs` |
| Auth / profile | `LocalAuthStore.cs`, `UserProfileController.cs` |

## Inspector flags (already set for designers)

- `preserveManualSim1ResultsLayout` / `preserveManualSim2ResultsLayout` — do not override Result/Recommendations TMP layout at runtime.
- `sim1ResultsPanels` / `sim2ResultsPanels` — toggle which metric lines appear.

## Pre-integration smoke test (partner)

1. Pull `unity-game`, open Phase B in Unity 6000.2.15f1.
2. Play **MainScene** → complete Sim 1 and Sim 2 without watch.
3. Confirm results panels and SCI graph (with mission markers) appear.
4. Register/login → open **My Profile** → chart shows after runs.
5. Then enable UDP/VR one subsystem at a time.

## Out of scope in this handoff

- `Assets/Scripts/Unused/` — legacy, not wired in MainScene.
- `Assets/_Recovery/` — local Unity recovery, ignore.
- Product name in Player Settings may still show default — set before final build.
