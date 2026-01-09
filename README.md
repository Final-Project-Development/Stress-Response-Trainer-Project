# VR Stress Response Trainer

**Project Number: 26-1-D-18** 
**Braude College of Engineering | Software Engineering Department**

## 📖 Overview

The **VR Stress Response Trainer** is an immersive simulation platform designed to enhance self-regulation and cognitive functioning under extreme pressure. Inspired by real-world emergency events, the system monitors physiological markers (HR and HRV) in real-time to create a personalized stress profile and provide actionable biofeedback.

## 🏗 System Architecture

The project utilizes a **Distributed Data Pipeline** across four primary nodes to ensure low-latency data synchronization:

1. **Wearable Node (Smartwatch):** Samples raw PPG sensor data (HR/HRV).
2. **Gateway Node (Android App):** Intercepts data via BLE and relays it to the workstation over Wi-Fi/Serial.
3. **Processing Node (Unity Engine):** The central hub that runs the **SCI (Stress Change Index) Algorithm** to analyze stress levels against a baseline.
4. **Visualization Node (VR Headset):** Renders immersive scenarios (Indoor Survival & First Aid) and provides visual feedback.

## 🛠 Tech Stack

* **Game Engine:** Unity 3D (C#).
* **VR Toolkit:** XR Interaction Toolkit & OpenXR.
* **Mobile:** Android SDK
* **Hardware:** VR Headset , Smartwatch.

## 👥 Authors

* **Sapir Gerstman** - [Sapir.Gerstman@e.braude.ac.il](mailto:Sapir.Gerstman@e.braude.ac.il)
* **Ido Ben Amara** - [Ido.Ben.Amara@e.braude.ac.il](mailto:Ido.Ben.Amara@e.braude.ac.il)

**Advisor:** Dr. Moshe Sulamy


