# VR Stress Response Trainer

**Project number:** 26-1-D-18  
**Braude College of Engineering - Software Engineering Department**

Immersive VR training platform for practicing self-regulation and decision-making under pressure. The system records heart rate during training sessions, computes a **Stress Change Index (SCI)** against a personal baseline and presents results-including a Pressure Graph.

**Authors:** Sapir Gerstman · Ido Ben Amara  
**Advisor:** Dr. Moshe Sulamy

**Repository:** https://github.com/Final-Project-Development/Stress-Response-Trainer-Project

---

## Repository layout

| Path | Description |
|------|-------------|
| **`Submission/`** | Capstone submission package - Phase A & B includes books, poster, demo video ([details](Submission/README.md)) |
| **`Unity_Project/`** | **Main Unity VR training application**. |
| **`Fit3UnityBridge/`** | Wearable data pipeline: Android bridge apps + Windows **HrPcBridge** relay |
| **`data_transfer_FP/`** | Optional Python FastAPI WebSocket prototype |
| **`RUN_INSTRUCTIONS.md`** | Full setup, VR, watch pipeline, Help User system and troubleshooting |

---

## What the application does

1. **Login / registration** - user accounts and session history
2. **Calibration** - 60 seconds to establish a personal HR/HRV baseline
3. **Environment Learning** - optional guided tour of the training hub
4. **Simulation 1 - Indoor Survival** - shelter and safety tasks under stressors
5. **Simulation 2 - First Aid** - locate and treat a wounded casualty
6. **Results** - SCI score, performance summary and Pressure Graph

Physiology can come from a **Samsung Galaxy Fit3** (via the bridge pipeline below) or from **simulated HR/HRV** when no watch is connected.

---

## System architecture

The system uses a **four-node distributed pipeline**. In the primary lab setup (PC VR with Meta Quest Link), Unity runs on the Windows PC, the Quest is the VR display only.

```
Samsung Watch → Samsung Health (phone) → Fit3 Samsung Bridge (Android) → Unity Project → Quest Link to Meta Quest
```

| Node | Component | Role |
|------|-----------|------|
| ① Wearable | Samsung Galaxy Fit3 | Records HR via PPG, syncs to Samsung Health over BLE |
| ② Gateway | Fit3 Samsung Bridge (Android) | Reads HR from Samsung Health Data SDK |
| ③ Processing | **HrPcBridge** + **Unity** (`Unity_Project`) | HrPcBridge forwards to Unity, Unity runs SCI and the training flow |
| ④ Visualization | Meta Quest (Link / Air Link) | Displays the VR scene rendered by Unity on the PC |


## Quick start - run Unity_Project in Unity

### Prerequisites

- **Unity Hub** + **Unity 6000.2.15f1** (see `Unity_Project/ProjectSettings/ProjectVersion.txt`)
- Windows 10/11 PC with a VR-ready GPU (for headset play via Quest Link)
- **Meta Quest** headset + Meta Quest app (Link or Air Link)

### Steps

```bash
git clone https://github.com/Final-Project-Development/Stress-Response-Trainer-Project.git
cd Stress-Response-Trainer-Project
git checkout main
git lfs pull
```

1. Open **`Unity_Project`** in Unity Hub (not the repository root).
2. Open scene **`Assets/Scenes/MainScene.unity`**.
3. Press **Play**.
4. Register on the login screen (needed for profile and history).
5. Complete calibration, then choose Simulation 1 or 2.

**Desktop testing (no headset):** WASD, mouse, **E** to interact, **H** for Help.  
**VR:** Connect Quest via Link/Air Link, use controllers per in-app prompts.

See [`RUN_INSTRUCTIONS.md`](RUN_INSTRUCTIONS.md) for the full guide including the watch pipeline and Help User system.

---

## live Samsung watch pipeline

Run these **before** starting a Unity session that should use real HR data:

1. **HrPcBridge** (Windows, .NET 8):
   ```bash
   cd Fit3UnityBridge/PcBridge/HrPcBridge
   dotnet run
   ```
   
2. **Fit3 Samsung Bridge** - build/install the APK from `Fit3UnityBridge/Android/Fit3SamsungBridge/` on the Android phone. Grant Samsung Health permissions, set the PC's LAN IP, use *Send Test Packet To PC* or *Start Samsung SDK Streaming*.

3. Phone and PC must be on the **same Wi-Fi network**.

4. Start Unity **after** HrPcBridge is running.

---

## Fit3UnityBridge components

| Component | Path | Notes |
|-----------|------|-------|
| **Fit3 Samsung Bridge** | `Fit3UnityBridge/Android/Fit3SamsungBridge/` | Primary Android gateway (Samsung Health Data SDK) |
| **Fit3 Health Bridge** | `Fit3UnityBridge/Android/Fit3HealthBridge/` | Alternate prototype via Health Connect |
| **HrPcBridge** | `Fit3UnityBridge/PcBridge/HrPcBridge/` | Windows relay (.NET 8) |
| **Samsung SDK** | `Fit3UnityBridge/SamsungSDK/` | Samsung Health Data SDK reference |

---

## Automated tests

Unity Test Framework tests live under **`Unity_Project/Assets/Tests/`**:

| Suite | Tests |
|-------|-------|
| **EditMode** | `StressChangeIndexCalculatorTests`, `SimulationRunOutcomeTests` |
| **PlayMode** | `SmokePlayModeTests` |

**Run in editor:** Window → General → **Test Runner** → EditMode or PlayMode → Run All.

Core logic under test is in `Unity_Project/Assets/Scripts/Core/` (`StressTrainer.Core.asmdef`). Test assemblies are excluded from player builds.

---

## Tech stack

| Layer | Technology |
|-------|------------|
| Game engine | Unity **6000.2.15f1**, C# |
| Rendering | Universal Render Pipeline (URP) **17.2.0** |
| VR | OpenXR **1.16.1**, XR Interaction Toolkit |
| Android gateway | Kotlin, Samsung Health Data SDK |
| PC relay | .NET 8 (**HrPcBridge**) |
| Testing | Unity Test Framework (EditMode + PlayMode) |
| Optional bridge | Python 3.10+, FastAPI WebSocket (`data_transfer_FP/`) |
| Hardware | Meta Quest 2/3/Pro, Samsung Galaxy Fit3 + Android phone |

---

## Submission deliverables

All capstone submission files are under **`Submission/`**:

| Folder | Contents |
|--------|----------|
| [`Submission/Phase A/`](Submission/Phase%20A/) | Phase A book PDF, presentation, prototype video |
| [`Submission/Phase B/`](Submission/Phase%20B/) | Phase B book PDF, poster, demo video pending |

The **Unity source code** is in [`Unity_Project/`](Unity_Project/) at the repository root (not inside `Submission/`).

---

## Branches

| Branch | Purpose |
|--------|---------|
| **`main`** | **Default.** Latest integrated VR app, tests and bridges |
| `vr-improvements` | Merged into `main` |
| `unity-game` | Earlier integration branch |
| `data-transfer` | Watch bridge experiments (selectively merged) |

---

## Contact

- **Sapir Gerstman** - [Sapir.Gerstman@e.braude.ac.il](mailto:Sapir.Gerstman@e.braude.ac.il)
- **Ido Ben Amara** - [Ido.Ben.Amara@e.braude.ac.il](mailto:Ido.Ben.Amara@e.braude.ac.il)
- **Advisor:** Dr. Moshe Sulamy
