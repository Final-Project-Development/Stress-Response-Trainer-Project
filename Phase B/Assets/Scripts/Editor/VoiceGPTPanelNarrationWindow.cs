using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch-generates VoiceGPT WAV files from <see cref="TrainingFlowController"/> panel copy
/// and assigns them to the controller (or a <see cref="PanelNarrationLibrary"/>).
/// VoiceGPT runs in the Editor only — at runtime the game plays the generated AudioClips.
/// </summary>
public class VoiceGPTPanelNarrationWindow : EditorWindow
{
    private const string VoicesFolder = "Assets/VoiceGPT/Voices/Narration";
    private const string ConfigPath = "Assets/VoiceGPT/Models/config.txt";
    private const string RecommendedVoiceName = "Adrian";

    private static readonly string[] VoiceNames =
    {
        "Clara", "Tamara", "Alice", "Anya", "Anne", "Ayla", "Brianna", "Greta", "Harriet", "Sophia",
        "Tamsin", "Tanya", "Violet", "Drew", "Basir", "Dino", "Royce", "Victor", "Abe", "Adrian",
        "Balder", "Cade", "Damon", "Gil", "Ian", "Kaz", "Ludwig", "Sam", "Troy", "Vince", "Zack",
        "Noah", "Maya", "Una", "Lina", "Chance", "Sophie", "Camille", "Daisy", "Grace", "Lily",
        "Zoe", "Nell", "Bree", "Alexa", "Alma", "Rose", "Ike", "Phil", "Dajam", "Wolf", "Adam",
        "Kamal", "Eugene", "Finn", "Xander", "Louie", "Mark"
    };

    private TrainingFlowController _flow;
    private PanelNarrationLibrary _library;
    private int _voiceIndex;
    private float _cfgScale = 0.65f;
    private int _steps = 12;
    private bool _genIntro = true;
    private bool _genCalibration = true;
    private bool _genLearnBriefing = true;
    private bool _genSim1Briefing = true;
    private bool _genSim2Briefing = true;
    private Vector2 _scroll;

    [MenuItem("Window/VoiceGPT/Panel Narration Setup")]
    private static void Open()
    {
        var window = GetWindow<VoiceGPTPanelNarrationWindow>("Panel Narration");
        window.minSize = new Vector2(420f, 520f);
    }

    private void OnEnable()
    {
        if (_flow == null)
            _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        _voiceIndex = ResolveVoiceIndex(RecommendedVoiceName);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "VoiceGPT creates .wav files in the Editor. At runtime each panel plays its assigned AudioClip " +
            "(same text you see on screen). Install Python Scripting and vgpt first (see Window → VoiceGPT).",
            MessageType.Info);

        _flow = (TrainingFlowController)EditorGUILayout.ObjectField(
            "Training Flow Controller", _flow, typeof(TrainingFlowController), true);
        _library = (PanelNarrationLibrary)EditorGUILayout.ObjectField(
            "Narration Library (optional)", _library, typeof(PanelNarrationLibrary), false);

        if (_flow == null)
        {
            EditorGUILayout.HelpBox("Assign TrainingFlowController from the backup scene (or open that scene first).", MessageType.Warning);
            if (GUILayout.Button("Find In Open Scenes"))
                _flow = FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
            return;
        }

        EditorGUILayout.Space(8);
        _voiceIndex = EditorGUILayout.Popup("Voice", _voiceIndex, VoiceNames);
        if (VoiceNames[_voiceIndex] != RecommendedVoiceName)
        {
            EditorGUILayout.HelpBox(
                $"Recommended single narrator for this project: {RecommendedVoiceName} " +
                "(calm, clear instructional tone). Use the same voice for every panel.",
                MessageType.None);
        }
        _cfgScale = EditorGUILayout.Slider("CFG Scale", _cfgScale, 0f, 1f);
        _steps = EditorGUILayout.IntSlider("Steps", _steps, 2, 30);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Generate clips for", EditorStyles.boldLabel);
        _genIntro = EditorGUILayout.ToggleLeft("Intro panel (Intro_Panel)", _genIntro);
        _genCalibration = EditorGUILayout.ToggleLeft("Calibration panel (Baseline_Panel)", _genCalibration);
        _genLearnBriefing = EditorGUILayout.ToggleLeft("Environment learning briefing (LearnBriefing_Panel)", _genLearnBriefing);
        _genSim1Briefing = EditorGUILayout.ToggleLeft("Simulation 1 briefing (Sim1Briefing_Panel)", _genSim1Briefing);
        _genSim2Briefing = EditorGUILayout.ToggleLeft("Simulation 2 briefing (Sim2Briefing_Panel)", _genSim2Briefing);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Each sentence / paragraph is generated as its own clip (e.g. Adrian_Intro_0, Adrian_Intro_1). " +
            "At runtime the game plays them one by one with a short pause between sentences.",
            MessageType.None);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160f));
        DrawTextPreview("Intro", _flow.introNarrationText);
        DrawTextPreview("Calibration", _flow.calibrationInstruction);
        DrawTextPreview("Learn briefing", _flow.learnBriefingBody);
        DrawTextPreview("Sim 1 briefing", _flow.missionBriefingBody);
        DrawTextPreview("Sim 2 briefing", _flow.sim2BriefingBody);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        EditorGUI.BeginDisabledGroup(_flow == null);
        if (GUILayout.Button("Generate Selected VoiceGPT Clips", GUILayout.Height(32f)))
            GenerateSelected();
        if (GUILayout.Button("Assign Existing Narration Clips From Folder", GUILayout.Height(26f)))
            AssignExistingClipsFromFolder();
        EditorGUI.EndDisabledGroup();
    }

    private static void DrawTextPreview(string label, string text)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        EditorGUILayout.TextArea(text ?? string.Empty, GUILayout.MinHeight(36f));
        int units = PanelNarrationTextUtil.SplitIntoSpeechUnits(text ?? string.Empty).Count;
        if (units > 0)
            EditorGUILayout.LabelField($"Speech units: {units}", EditorStyles.miniLabel);
        EditorGUILayout.Space(4f);
    }

    private void GenerateSelected()
    {
        if (!Directory.Exists(VoicesFolder))
            Directory.CreateDirectory(VoicesFolder);

        try
        {
            EnsureNltkData();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[VoiceGPT Panel Narration] NLTK setup failed: {ex.Message}");
            return;
        }

        var voiceName = VoiceNames[_voiceIndex];
        var generated = 0;

        if (_genIntro)
            generated += GenerateSequenceFromLines("Intro", GetIntroSpeechLines(_flow), voiceName);
        if (_genCalibration)
            generated += GenerateSequence("Calibration", _flow.calibrationInstruction, voiceName);
        if (_genLearnBriefing)
            generated += GenerateSequence("LearnBriefing", _flow.learnBriefingBody, voiceName);
        if (_genSim1Briefing)
            generated += GenerateSequence("Sim1Briefing", _flow.missionBriefingBody, voiceName);
        if (_genSim2Briefing)
            generated += GenerateSequence("Sim2Briefing", _flow.sim2BriefingBody, voiceName);

        AssetDatabase.Refresh();
        AssignExistingClipsFromFolder();
        EditorUtility.DisplayDialog("VoiceGPT", $"Finished generating {generated} clip(s).\nOutput: {VoicesFolder}", "OK");
    }

    private static string[] GetIntroSpeechLines(TrainingFlowController flow)
    {
        return new[]
        {
            flow.introParagraph1,
            flow.introParagraph2,
            flow.introParagraph3,
            flow.introParagraph4
        };
    }

    private int GenerateSequenceFromLines(string fileStem, string[] lines, string voiceName)
    {
        if (lines == null || lines.Length == 0)
            return 0;

        int generated = 0;
        int clipIndex = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            if (GenerateOne($"{fileStem}_{clipIndex}", lines[i], voiceName))
                generated++;
            clipIndex++;
        }

        return generated;
    }

    private int GenerateSequence(string fileStem, string sourceText, string voiceName)
    {
        var units = PanelNarrationTextUtil.SplitIntoSpeechUnits(sourceText);
        if (units.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"[VoiceGPT Panel Narration] Skipped {fileStem}: empty text.");
            return 0;
        }

        int generated = 0;
        for (int i = 0; i < units.Count; i++)
        {
            if (GenerateOne($"{fileStem}_{i}", units[i], voiceName))
                generated++;
        }

        return generated;
    }

    private bool GenerateOne(string fileStem, string sourceText, string voiceName)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            UnityEngine.Debug.LogWarning($"[VoiceGPT Panel Narration] Skipped {fileStem}: empty text.");
            return false;
        }

        var prompt = SanitizeForVoiceGpt(sourceText);
        var fileName = $"{voiceName}_{fileStem}";
        var outputAssetPath = $"{VoicesFolder}/{fileName}.wav";
        var outputFullPath = Path.GetFullPath(outputAssetPath);

        WriteConfig(prompt, voiceName, outputFullPath, _cfgScale, _steps);

        try
        {
            RunVoiceGptMainPy();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[VoiceGPT Panel Narration] Generation failed for {fileStem}: {ex.Message}");
            return false;
        }

        if (!File.Exists(outputFullPath))
        {
            UnityEngine.Debug.LogError($"[VoiceGPT Panel Narration] Expected output missing: {outputAssetPath}");
            return false;
        }

        UnityEngine.Debug.Log($"[VoiceGPT Panel Narration] Generated {outputAssetPath}");
        return true;
    }

    /// <summary>
    /// Unity's built-in Python often lacks vgpt/nltk. Use a full Python install instead.
    /// </summary>
    private static void RunVoiceGptMainPy()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
            throw new InvalidOperationException("Could not resolve project root.");

        var scriptPath = Path.Combine(projectRoot, "Assets", "VoiceGPT", "Models", "main.py");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("VoiceGPT main.py not found.", scriptPath);

        var pythonExe = ResolvePythonExecutable();
        var pythonArgs = pythonExe == "py"
            ? $"-3 \"{scriptPath}\""
            : $"\"{scriptPath}\"";
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = pythonArgs,
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException($"Failed to start Python: {pythonExe}");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        var stdoutText = stdout.ToString().Trim();
        var stderrText = stderr.ToString().Trim();

        if (!string.IsNullOrWhiteSpace(stdoutText))
            UnityEngine.Debug.Log("[VoiceGPT] " + stdoutText);

        if (process.ExitCode != 0)
        {
            var detail = BuildPythonFailureMessage(process.ExitCode, stdoutText, stderrText);
            throw new InvalidOperationException(detail);
        }
    }

    private static void EnsureNltkData()
    {
        var pythonExe = ResolvePythonExecutable();
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = pythonExe == "py"
                ? "-3 -c \"import nltk; nltk.download('punkt_tab'); nltk.download('punkt')\""
                : "-c \"import nltk; nltk.download('punkt_tab'); nltk.download('punkt')\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException($"Failed to start Python: {pythonExe}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(BuildPythonFailureMessage(process.ExitCode, stdout, stderr));
    }

    private static string BuildPythonFailureMessage(int exitCode, string stdout, string stderr)
    {
        var combined = (stdout + "\n" + stderr).Trim();
        if (string.IsNullOrWhiteSpace(combined))
            return $"Python exited with code {exitCode}";

        var lines = combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 8)
            return combined;

        return string.Join(Environment.NewLine, lines[^8..]);
    }

    private static string ResolvePythonExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        {
            Path.Combine(localAppData, @"Programs\Python\Python312\python.exe"),
            Path.Combine(localAppData, @"Programs\Python\Python311\python.exe"),
            Path.Combine(localAppData, @"Programs\Python\Python310\python.exe"),
            Path.Combine(localAppData, @"Python\pythoncore-3.12-64\python.exe"),
            Path.Combine(localAppData, @"Python\pythoncore-3.11-64\python.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate) && PythonCanImport(candidate, "vgpt"))
                return candidate;
        }

        if (PythonCanImport("python", "vgpt"))
            return "python";

        if (PythonCanImport("py", "vgpt"))
            return "py";

        throw new InvalidOperationException(
            "Could not find Python with vgpt installed. Run once in a terminal:\n" +
            "python -m pip install \"Assets/VoiceGPT/Models/vgpt-0.1.6-py3-none-any.whl\"");
    }

    private static bool PythonCanImport(string pythonExe, string moduleName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = pythonExe == "py"
                    ? $"-3 -c \"import {moduleName}\""
                    : $"-c \"import {moduleName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            process.WaitForExit(15000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteConfig(string text, string voiceName, string outputFullPath, float cfgScale, int steps)
    {
        var previewVoicePath = $"{Application.dataPath}/VoiceGPT/Voices/Preview Voices/{voiceName}.wav";
        var alpha = (1f - cfgScale).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var beta = cfgScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lines = new[]
        {
            $"Model = \"{Application.dataPath}/VoiceGPT/Models/Model.pth\"",
            $"Config = \"{Application.dataPath}/VoiceGPT/Models/ModelConfig.yml\"",
            $"ASRModel = \"{Application.dataPath}/VoiceGPT/Models/ASRModel.pth\"",
            $"ASRConfig = \"{Application.dataPath}/VoiceGPT/Models/ASRConfig.yml\"",
            $"F0Model = \"{Application.dataPath}/VoiceGPT/Models/F0Model.t7\"",
            $"BERTModel = \"{Application.dataPath}/VoiceGPT/Models/BERTModel.t7\"",
            $"BERTConfig = \"{Application.dataPath}/VoiceGPT/Models/BERTConfig.yml\"",
            "_text = \"\"\"" + text + "\"\"\"",
            "_targetVoice = \"" + previewVoicePath.Replace("\\", "/") + "\"",
            "_outputPath = \"" + outputFullPath.Replace("\\", "/") + "\"",
            "_eScale = 3",
            "_alpha = " + alpha,
            "_beta = " + beta,
            "_steps = " + steps,
            "_enableEScale = False"
        };

        File.WriteAllText(ConfigPath, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
    }

    private void AssignExistingClipsFromFolder()
    {
        if (_flow == null)
            return;

        var voiceName = VoiceNames[_voiceIndex];
        _flow.introNarrationSentenceClips = LoadClipSequence(voiceName, "Intro");
        _flow.calibrationNarrationSentenceClips = LoadClipSequence(voiceName, "Calibration");
        _flow.learnBriefingNarrationSentenceClips = LoadClipSequence(voiceName, "LearnBriefing");
        _flow.missionBriefingNarrationSentenceClips = LoadClipSequence(voiceName, "Sim1Briefing");
        _flow.sim2BriefingNarrationSentenceClips = LoadClipSequence(voiceName, "Sim2Briefing");

        var intro = FirstClip(_flow.introNarrationSentenceClips);
        var calibration = FirstClip(_flow.calibrationNarrationSentenceClips);
        var learnBriefing = FirstClip(_flow.learnBriefingNarrationSentenceClips);
        var sim1 = FirstClip(_flow.missionBriefingNarrationSentenceClips);
        var sim2 = FirstClip(_flow.sim2BriefingNarrationSentenceClips);

        Undo.RecordObject(_flow, "Assign panel narration clips");
        if (intro != null) _flow.introNarrationClip = intro;
        if (calibration != null) _flow.calibrationNarrationClip = calibration;
        if (learnBriefing != null) _flow.learnBriefingNarrationClip = learnBriefing;
        if (sim1 != null) _flow.missionBriefingNarrationClip = sim1;
        if (sim2 != null) _flow.sim2BriefingNarrationClip = sim2;
        EditorUtility.SetDirty(_flow);

        if (_library != null)
        {
            Undo.RecordObject(_library, "Assign panel narration library");
            _library.introSentenceClips = _flow.introNarrationSentenceClips;
            _library.calibrationSentenceClips = _flow.calibrationNarrationSentenceClips;
            _library.learnBriefingSentenceClips = _flow.learnBriefingNarrationSentenceClips;
            _library.sim1MissionBriefingSentenceClips = _flow.missionBriefingNarrationSentenceClips;
            _library.sim2BriefingSentenceClips = _flow.sim2BriefingNarrationSentenceClips;
            if (intro != null) _library.introClip = intro;
            if (calibration != null) _library.calibrationClip = calibration;
            if (learnBriefing != null) _library.learnBriefingClip = learnBriefing;
            if (sim1 != null) _library.sim1MissionBriefingClip = sim1;
            if (sim2 != null) _library.sim2BriefingClip = sim2;
            EditorUtility.SetDirty(_library);
        }

        var gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gameManager != null && _library != null && _library.allItemsCollectedRunToMamadClip != null)
        {
            Undo.RecordObject(gameManager, "Assign mission voice clip");
            gameManager.allItemsCollectedRunToMamadClip = _library.allItemsCollectedRunToMamadClip;
            EditorUtility.SetDirty(gameManager);
        }

        UnityEngine.Debug.Log("[VoiceGPT Panel Narration] Assigned clips on TrainingFlowController" +
                  (_library != null ? " and PanelNarrationLibrary." : "."));
    }

    private static AudioClip LoadClip(string fileStem)
    {
        var path = $"{VoicesFolder}/{fileStem}.wav";
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static AudioClip[] LoadClipSequence(string voiceName, string panelStem)
    {
        var clips = new System.Collections.Generic.List<AudioClip>();
        for (int i = 0; i < 32; i++)
        {
            var clip = LoadClip($"{voiceName}_{panelStem}_{i}");
            if (clip == null)
                break;
            clips.Add(clip);
        }

        return clips.ToArray();
    }

    private static AudioClip FirstClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;
        return clips[0];
    }

    private static int ResolveVoiceIndex(string voiceName)
    {
        for (int i = 0; i < VoiceNames.Length; i++)
        {
            if (VoiceNames[i] == voiceName)
                return i;
        }

        return 0;
    }

    /// <summary>Matches VoiceGPT editor sanitization for local model.</summary>
    public static string SanitizeForVoiceGpt(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = text.Replace("•", " ");
        text = text.Replace("—", "-");
        text = text.Replace("–", "-");
        text = text.Replace("→", " then ");
        text = Regex.Replace(text, @"\r?\n", "  ");
        // VoiceGPT local model is most reliable with plain ASCII.
        text = Regex.Replace(text, @"[^\x20-\x7E]", " ");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }
}
