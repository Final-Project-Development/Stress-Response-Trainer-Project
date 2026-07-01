# Run Instructions — VR Stress Response Trainer

**Project number:** 26-1-D-18  
**Department:** Software Engineering, Braude College of Engineering  
**Authors:** Sapir Gerstman, Ido Ben Amara  
**Advisor:** Dr. Moshe Sulamy  

**GitHub repository:** https://github.com/Final-Project-Development/Stress-Response-Trainer-Project  
**Recommended branch:** `main`

---

## Table of contents

1. [Overview](#1-overview)
2. [System requirements](#2-system-requirements)
3. [Downloading the code and setting up the environment](#3-downloading-the-code-and-setting-up-the-environment)
4. [Basic run — Unity Editor (no VR, no watch)](#4-basic-run--unity-editor-no-vr-no-watch)
5. [Running with Meta Quest (VR)](#5-running-with-meta-quest-vr)
6. [Running with Samsung watch (optional)](#6-running-with-samsung-watch-optional)
7. [Application flow — screen by screen](#7-application-flow--screen-by-screen)
8. [Help User guidance system](#8-help-user-guidance-system)
9. [Controls — keyboard and VR](#9-controls--keyboard-and-vr)
10. [Automated tests](#10-automated-tests)
11. [Project folder structure](#11-project-folder-structure)
12. [Troubleshooting](#12-troubleshooting)

---

## 1. Overview

The **VR Stress Response Trainer** is a VR training system for practicing decision-making and self-regulation under pressure. The repository includes:

- Main Unity application (`Phase B/`) — simulations, user interface, **SCI** (Stress Change Index) computation
- Smartwatch data bridge (`Fit3UnityBridge/`) — Android app + Windows **HrPcBridge**
- Previous-semester prototype (`Phase A/`) — presentation, project book PDF, demo video

**Default behaviour:** simulated HR/HRV data (`MockPhysiologySource`). A real watch connection is optional.

---

## 2. System requirements

### 2.1 Hardware — minimum for Editor testing (no VR)

| Component | Requirement |
|-----------|-------------|
| PC | Windows 10/11 (64-bit) |
| CPU | Intel i5 / AMD Ryzen 5 or better |
| RAM | 8 GB (16 GB recommended) |
| GPU | GTX 1060 6 GB or better |
| Storage | Free space for Unity project (~15 GB including Library) |

### 2.2 Hardware — full lab deployment (VR + watch)

| Component | Requirement |
|-----------|-------------|
| PC | As above + Meta Quest Link support |
| VR | Meta Quest 2 / 3 / Pro + controllers |
| USB cable | USB 3, 3 m (10 ft) recommended (or Air Link) |
| Watch | Samsung Galaxy Fit3 (or watch supported by Samsung Health) |
| Phone | Android 10+ with Samsung Health and Fit3 Samsung Bridge |

### 2.3 Software

| Software | Version | Purpose |
|----------|---------|---------|
| **Unity Hub** + **Unity Editor** | **6000.2.15f1** | Open `Phase B` |
| **Meta Quest app** | Latest | Quest Link / Air Link |
| **.NET SDK** | 8.0 | Run HrPcBridge |
| **Android Studio** | Latest | Build APK (watch pipeline only) |
| **Git** | Latest | Clone the repository |
| **Git LFS** | Installed | Large files in `Phase A` |

---

## 3. Downloading the code and setting up the environment

### 3.1 Clone the repository

```bash
git clone https://github.com/Final-Project-Development/Stress-Response-Trainer-Project.git
cd Stress-Response-Trainer-Project
git checkout main
git lfs pull
```

### 3.2 Install Unity

1. Open **Unity Hub** → **Installs** → install **6000.2.15f1** (same version as `Phase B/ProjectSettings/ProjectVersion.txt`).
2. **Projects** → **Add** → select the **`Phase B`** folder (not the repository root).
3. Wait for the first import to finish (may take a while).

### 3.3 Main scene

Open: **`Assets/Scenes/MainScene.unity`**

---

## 4. Basic run — Unity Editor (no VR, no watch)

This is the fastest way to test — suitable for development, demos, and use without external hardware.

| Step | Action |
|------|--------|
| 1 | Open `Phase B` in Unity Hub |
| 2 | Open `MainScene.unity` |
| 3 | Press **Play** ▶ |
| 4 | On the opening screen — **Register** (recommended) or continue as guest |
| 5 | Go through Intro → **Calibration** (~60 seconds; stand or sit still) |
| 6 | Choose **Simulation 1**, **Simulation 2**, or **Environment Learning** |
| 7 | After a simulation — view the results screen (SCI, recommendations, Pressure Graph) |

**Desktop controls:**

| Input | Action |
|-------|--------|
| WASD | Move |
| Mouse | Look |
| **E** | Interact (pickups, doors, phone, casualty) |
| **H** | Open / close **Help** |
| **Esc** | Pause / close overlays |
| **1 / 2 / 3** | Treatment steps (Simulation 2) |

The top toolbar shows **Simulated** — mock heart-rate data (expected without a watch).

---

## 5. Running with Meta Quest (VR)

### 5.1 Setup

1. Install the **Meta Quest app** on the PC.
2. Connect the Quest via **Link** (USB cable) or **Air Link** (Wi-Fi).
3. Confirm the headset is detected in the Meta Quest app before pressing Play in Unity.

### 5.2 Run

1. In Unity: press **Play** ▶ (with Quest connected).
2. The system detects VR automatically (`XRInputBridge`) and switches to Quest controllers.
3. UI panels (login, results, Help) render **inside the headset** as world-space canvases.

### 5.3 VR controls (summary)

| Action | Control |
|--------|---------|
| Move | Left stick |
| Turn | Right stick (horizontal) |
| Interact (same as E) | **Right Trigger** |
| Digit 1 (phone / treatment) | **A** or **X** |
| Digit 0 (dialing) | **B** or **Y** |
| Treatment step 2 | **B** / **Y** |
| Treatment step 3 | **Grip** |
| Pause | **Menu** |
| **Help** | **A + X** together |

---

## 6. Running with Samsung watch (optional)

> Required only for live HR data. Without this setup the system uses simulated physiology.

### 6.1 Startup order (must follow this sequence)

```
Watch → Samsung Health → Fit3 Samsung Bridge (phone) → HrPcBridge (PC) → Unity → Quest
```

### 6.2 Step 1 — HrPcBridge (PC)

```bash
cd Fit3UnityBridge/PcBridge/HrPcBridge
dotnet run
```

- Listens on port **7777** (UDP + TCP)
- Forwards to Unity on **localhost UDP 5055**
- Log file: `hr_log.jsonl`

### 6.3 Step 2 — Fit3 Samsung Bridge (phone)

1. Build the APK from `Fit3UnityBridge/Android/Fit3SamsungBridge/` (Android Studio).
2. Install on the phone and grant Samsung Health permissions.
3. Phone and PC must be on the **same Wi-Fi network**.
4. Enter the PC’s LAN IP address.
5. **Send Test Packet To PC** — connectivity test.
6. **Start Samsung SDK Streaming** — for a training session.

### 6.4 Step 3 — Unity

1. Start **HrPcBridge** before pressing **Play**.
2. Wear the watch before **Calibration**.
3. Top toolbar should show **Connected** (instead of Simulated) when packets arrive.

**Note:** Samsung Health often saves the full HR timeline when the **watch workout ends**. The **Pressure Graph** is therefore shown **after the simulation**, not as a live graph during the mission.

### 6.5 Network ports

| Port | Component | Purpose |
|------|-----------|---------|
| **7777** | HrPcBridge | Input from phone |
| **5055** | Unity `WorkoutHeartRateChartReceiver` | HR input in Unity |
| **5005** | Unity `UDPReceiver` | Legacy dev path only |

---

## 7. Application flow — screen by screen

```
Hub → Login (optional) → Intro → Calibration (60s)
    → Simulation pick
        ├── Environment Learning (guided tour)
        ├── Simulation 1 — indoor survival / shelter
        │       Briefing → Safety Warning → mission → results
        └── Simulation 2 — first aid
                Briefing → Safety Warning → mission → results
    → Return to simulation pick / Profile
```

### Simulation 1 — mission steps

1. Enter the home and collect 5 items: water bottle, flashlight, radio, phone, key.
2. Turn off the lights (switch).
3. Close the double door.
4. Run to the outdoor shelter (Mamad).

### Simulation 2 — mission steps

1. Pick up the first aid kit.
2. Go to the wounded person — call for help.
3. Public phone: door → coin → receiver → dial **101**.
4. Return to the wounded person — treatment steps 1, 2, 3.

### Results screen

Three tabs:

| Tab | Content |
|-----|---------|
| **Result** | SCI score, time, mission performance |
| **Recommendations** | Stress-regulation recommendations |
| **Pressure Graph** | HR timeline vs. baseline |

---

## 8. Help User guidance system

The application includes a **built-in user guidance layer** across all training phases. Main components:

| Component | Source file | Role |
|-----------|-------------|------|
| **Help Panel** | `UINavigationManager.cs` | Global help window — available in every phase |
| **Mission Status Panel** | `MissionStatusPanelController.cs` | Current task + last completed step |
| **Hint** | `MissionHintService.cs` | World labels above mission targets |
| **Environment Learning** | `EnvironmentLearningController.cs` | Guided tour with sidebar list |

### 8.1 Help — general assistance

- **Keyboard:** **H**
- **VR:** **A + X** together
- **UI:** **Help** button in the top toolbar

Content **changes by phase** (Calibration, Sim 1, Sim 2, Environment Learning).  
During an active simulation, a **Current task** line is also shown from `GameManager`.

### 8.2 Hint — mission hint

- Available during **Simulation 1 and 2** on the **Mission Status** panel.
- Pressing **Hint** shows a **world label** (WorldItemLabel) above the relevant target for ~14 seconds.
- Press again to hide the hint.
- If no hint exists for the current step — displays "No hint available for this step."

### 8.3 Environment Learning

- Optional tour after Calibration.
- Labels on world objects + a list in the left sidebar.
- In VR: look at a name in the list + **Right Trigger** to jump to that object.
- **Back** or **Menu** — return to simulation selection.

### 8.4 Pause / Back

| Button | Action |
|--------|--------|
| **Pause** | Pause the simulation |
| **Back** | Return to Hub (confirmation required during an active simulation) |

---

## 9. Controls — keyboard and VR

### Keyboard + mouse (desktop)

| Input | Action |
|-------|--------|
| WASD | Move |
| Mouse | Look |
| E | Interact |
| H | Help |
| Esc | Pause / close overlay |
| 1, 2, 3 | Treatment steps (Sim 2) |
| 0 | Digit 0 when dialing |

### Meta Quest (OpenXR)

| Input | Action |
|-------|--------|
| Left stick | Move |
| Right stick | Turn |
| Right Trigger | Interact + click UI |
| A / X | Digit 1 |
| B / Y | Digit 0 or 2 |
| Grip | Treatment step 3 |
| Menu | Pause |
| A + X | Help |

---

## 10. Automated tests

The project includes an automated test layer (Unity Test Framework).

**Location:** `Phase B/Assets/Tests/`

| Suite | Files |
|-------|-------|
| EditMode | `StressChangeIndexCalculatorTests`, `SimulationRunOutcomeTests` |
| PlayMode | `SmokePlayModeTests` |

**How to run:**

1. Unity → **Window → General → Test Runner**
2. Open the **EditMode** or **PlayMode** tab
3. Click **Run All**

See also: `Phase B/Assets/Tests/README.md`

---

## 11. Project folder structure

```
Stress-Response-Trainer-Project/
├── Phase A/                    # Previous-semester prototype deliverables
├── Phase B/                    # Main Unity application
│   ├── Assets/
│   │   ├── Scenes/MainScene.unity
│   │   ├── Scripts/            # Gameplay, UI, VR, biometrics
│   │   └── Tests/              # Automated tests
│   └── ProjectSettings/
├── Fit3UnityBridge/
│   ├── Android/Fit3SamsungBridge/   # Phone bridge APK
│   └── PcBridge/HrPcBridge/         # Windows relay
├── data_transfer_FP/           # Optional Python prototype
├── README.md                   # Project overview
└── RUN_INSTRUCTIONS.md         # This file
```

### Key scripts

| Area | Files |
|------|-------|
| Training flow | `TrainingFlowController.cs` |
| Help / UI navigation | `UINavigationManager.cs` |
| Mission hints | `MissionHintService.cs`, `MissionStatusPanelController.cs` |
| Simulation 1 | `GameManager.cs`, `ShelterTrigger.cs` |
| Simulation 2 | `PublicPhoneBoothMission.cs`, `WoundedMan.cs` |
| SCI | `StressChangeIndexCalculator.cs`, `SessionStressRecorder.cs` |
| VR | `XRInputBridge.cs`, `VrGameplayInput.cs` |
| Watch HR | `WorkoutHeartRateChartReceiver.cs` |

---

## 12. Troubleshooting

| Symptom | Fix |
|---------|-----|
| Unity won’t open / package errors | Use version **6000.2.15f1**; delete `Library` and reopen |
| VR not detected | Check Quest Link in Meta Quest app; press Play with headset connected |
| Toolbar shows **Simulated** | Expected without a watch; for live data start HrPcBridge + phone bridge |
| No packets from watch | Same Wi-Fi; allow port 7777 in firewall; correct PC IP on phone |
| Pressure Graph empty | HR may arrive only **after** the watch workout ends — complete the full simulation |
| Help won’t open in VR | Use **A+X** or the Help button in the toolbar |
| Hint not visible | Confirm you are in active Sim 1/2; press Hint on the Mission Status panel |
| Phase A files very small | Run `git lfs pull` for PPT/PDF/video files |

---

## Appendix — recommended pre-submission / demo checklist

1. `git clone` + `git checkout main` + `git lfs pull`
2. Open `Phase B` in Unity 6000.2.15f1
3. Play → Register → Calibration → complete **Simulation 1** → results
4. Return to Hub → complete **Simulation 2** → results
5. Test **Help (H)** and **Hint** during a simulation
6. (Optional) VR with Quest Link
7. (Optional) Watch + HrPcBridge
8. Test Runner → EditMode → Run All

---

**Technical contact:**  
Sapir.Gerstman@e.braude.ac.il · Ido.Ben.Amara@e.braude.ac.il
