# סיור היכרות — backup.unity

## מהיר (Unity)

1. פתחי **backup.unity**
2. תפריט: **Tools → Stress Trainer → Setup Environment Learning (open scene)**
3. תפריט: **Tools → Stress Trainer → Create World Label Panel Prefab** (פעם אחת)
4. בצעי את השלבים הידניים למטה

## ידני — Level_Select_UI

1. ב-Hierarchy: **Level_Select_UI** → Add Component → **LevelSelectUI**
2. גררי **FlowManager** (TrainingFlowController) לשדה Training Flow
3. בתוך **Scrollable_Area → Content** — שכפלי בלוק של סימולציה 1 (הכרטיס/כפתור)
4. על העותק:
   - טקסט: **סיור היכרות** / **למידת העיר**
   - **Button → On Click** → `LevelSelectUI.SelectEnvironmentLearning`

(או: On Click → `TrainingFlowController.UI_PickEnvironmentLearningAfterCalibration`)

## ידני — תוויות בעיר

על כל אובייקט → **Add Component → WorldItemLabel**

| אובייקט | Label Text | World Offset Y |
|---------|------------|----------------|
| mamad | ממ"ד | 3–4 |
| WaterBottle | מים | 2 |
| (ערכת עזרה) | ערכת עזרה ראשונה | 2 |
| (פנס/רדיו וכו') | לפי שם | 2 |

**Label Panel Prefab** → `Assets/Prefabs/EnvironmentLearning/WorldLabelPanel.prefab`  
רקע הפאנל: ספרייט `inventory-highlight-large 1_0` (כמו ב-EnvironmentLearningHud), צבע לבן.  
(עצבו את הריבוע ב-prefab — זה העיצוב שלכם)

## ידני — FlowManager

על **FlowManager** → TrainingFlowController:

- **Environment Learning Controller** → אובייקט EnvironmentLearning
- **Environment Learning Hud Panel** → EnvironmentLearningHud (נוצר אוטומטית ב-Setup)
- **Environment Learning Spawn Point** → EnvironmentLearningSpawn (אופציונלי)

## בדיקה

1. Play → Hub → Intro → Calibration
2. נפתח **Level_Select_UI**
3. **סיור היכרות** → הליכה בעיר, תוויות מעל פריטים
4. **Back** או **Esc** → חזרה ל-Level_Select_UI
5. סימולציה 1 → משימה רגילה (איסוף עובד)
