# VR Stress Response Trainer

**Project Number: 26-1-D-18** 

**Braude College of Engineering**

**Software Engineering Department**

## Repository layout

* **`Phase A/`** - prototype deliverables from the previous semester.
* **`Phase B/`** - Unity project (active game).
* **`Fit3UnityBridge/`** - Android / PC bridges for Samsung watch UDP.
* **`Phase B/HANDOFF_TO_PARTNER.md`** - integration guide for watch + VR (read this before hardware work).

## Run Phase B (desktop — current handoff)

1. Install **Unity 6000.2.15f1** (same as `Phase B/ProjectSettings/ProjectVersion.txt`).
2. Open folder **`Phase B`** in Unity Hub.
3. Open scene **`Assets/Scenes/MainScene.unity`**.
4. Press **Play**.
5. Optional: **Register** on Login screen (required for Profile/history).
6. Complete **calibration** (~60s), then choose Simulation 1 or 2.

**Controls:** WASD, mouse, **E** to interact. No VR headset required for this build.

**Biometrics:** Simulated HR/HRV by default. Live watch/gateway is optional — see `Phase B/HANDOFF_TO_PARTNER.md`.

**Git branch for latest game:** `unity-game`

## 📖 Overview

The **VR Stress Response Trainer** is an immersive simulation platform designed to enhance self-regulation and cognitive functioning under extreme pressure. Inspired by real-world emergency events, the system monitors physiological markers (HR and HRV) in real-time to create a personalized stress profile and provide actionable biofeedback.

## 🏗 System Architecture

The project utilizes a **Distributed Data Pipeline** across four primary nodes to ensure low-latency data synchronization:

1. **Wearable Node (Smartwatch):** Samples raw PPG sensor data (HR/HRV).
2. **Gateway Node (Android App):** Intercepts data via BLE and relays it to the workstation over Wi-Fi/Serial.
3. **Processing Node (Unity Engine):** The central hub that runs the **SCI (Stress Change Index) Algorithm** to analyze stress levels against a baseline.
4. **Visualization Node (VR Headset):** Renders immersive scenarios (Indoor Survival & First Aid) and provides visual feedback.

**Current Phase B handoff:** Desktop FPS build with simulated physiology. VR headset and live smartwatch are integrated by the partner branch (see `Phase B/HANDOFF_TO_PARTNER.md`).
   
<img width="987" height="189" alt="image" src="https://github.com/user-attachments/assets/1fc7485d-1689-4361-a11c-f613639c2a9d" />

## 🛠 Tech Stack

* **Game Engine:** Unity 3D (C#).
* **VR Toolkit:** XR Interaction Toolkit & OpenXR.
* **Mobile:** Android SDK
* **Hardware:** VR Headset , Smartwatch.

## 👥 Authors

* **Sapir Gerstman** - [Sapir.Gerstman@e.braude.ac.il](mailto:Sapir.Gerstman@e.braude.ac.il)
* **Ido Ben Amara** - [Ido.Ben.Amara@e.braude.ac.il](mailto:Ido.Ben.Amara@e.braude.ac.il)

**Advisor:** Dr. Moshe Sulamy


