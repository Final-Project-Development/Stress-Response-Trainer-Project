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
או: **Tools → Stress Trainer → Setup Environment Learning Tour Labels** (Home, Mamad, Map, Compass, First Aid, Water Bottle, Flashlight, Radio, Wounded Character)

### מיקום מדויק של הפאנל (HUB) מעל כל פריט

1. בחרי את האובייקט (למשל Map) → **World Item Label**
2. **Create / Select Label Anchor** — נוצר ילד `LabelAnchor`
3. ב-Scene view גררי את **LabelAnchor** (חץ ירוק) למקום המדויק מעל הפריט
4. חזרי על כל הפריטים ברשימה למעלה

### גודל הפאנל (HUB) מעל פריטים

על **EnvironmentLearning** (בסצנה):

| שדה | משמעות | להקטנה |
|-----|--------|--------|
| **World Label World Scale** | scale של הקנבס בעולם | נסי `0.004`–`0.005` |
| **World Label Panel Size** | רוחב×גובה (פיקסלים) | למשל `120, 36` |
| **World Label Font Size** | גודל טקסט | למשל `14`–`16` |

אחרי שינוי — **Play** מחדש (הפאנלים נבנים בתחילת הסיור).

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
- **Environment Learning Hud Panel** → **EnvironmentLearningHud** (את מעצבת ב-Inspector: Image, sprite, גודל, צבע, טקסט)
- על **EnvironmentLearning** → `Apply Default Hud Text At Start` = **כבוי** (ברירת מחדל) כדי שהעיצוב והטקסט בסצנה לא יידרסו ב-Play
- חיבור מהיר: **Tools → Stress Trainer → Wire Environment Learning HUD**
- **Environment Learning Spawn Point** → המיקום שבחרת (לרוב **Simulation2SpawnPoint** — אותו Transform כמו סימולציה 2). ריק = fallback ל-Simulation 2.
- **Use Gate Spawn** = כבוי. המרקר = מרכז ה-CharacterController (כמו בסימולציה 2).

## בדיקה

1. Play → Hub → Intro → Calibration
2. נפתח **Level_Select_UI**
3. **סיור היכרות** → הליכה בעיר, תוויות מעל פריטים
4. **Back** או **Esc** → חזרה ל-Level_Select_UI
5. סימולציה 1 → משימה רגילה (איסוף עובד)
