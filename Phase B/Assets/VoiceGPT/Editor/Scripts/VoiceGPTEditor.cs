using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
using Unity.EditorCoroutines.Editor;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Scripting.Python;


namespace AiKodexVoiceGPT
{
    public class VoiceGPTEditor : EditorWindow
    {
        string text = "";
        int charLimit = 500;
        public enum Model
        {
            [InspectorName("VoiceGPT - 0.1.6 (Local)")]
            VoiceGPT_Local,
            // Not supported now:
            // [InspectorName("VoiceGPT (Server-Based)")]
            // VoiceGPT_X,
        };
        public static Model model = Model.VoiceGPT_Local;
        public enum Voice
        {
            Clara,
            Tamara,
            Alice,
            Anya,
            Anne,
            Ayla,
            Brianna,
            Greta,
            Harriet,
            Sophia,
            Tamsin,
            Tanya,
            Violet,
            Drew,
            Basir,
            Dino,
            Royce,
            Victor,
            Abe,
            Adrian,
            Balder,
            Cade,
            Damon,
            Gil,
            Ian,
            Kaz,
            Ludwig,
            Sam,
            Troy,
            Vince,
            Zack,
            Noah,
            Maya,
            Una,
            Lina,
            Chance,
            Sophie,
            Camille,
            Daisy,
            Grace,
            Lily,
            Zoe,
            Nell,
            Bree,
            Alexa,
            Alma,
            Rose,
            Ike,
            Phil,
            Dajam,
            Wolf,
            Adam,
            Kamal,
            Eugene,
            Finn,
            Xander,
            Louie,
            Mark,
        }
        public static Voice voice = Voice.Clara;
        public enum Language
        {
            [InspectorName("English")]
            en,

            [InspectorName("Spanish")]
            es,

            [InspectorName("French")]
            fr,

            [InspectorName("German")]
            de,

            [InspectorName("Italian")]
            it,

            [InspectorName("Portuguese")]
            pt,

            [InspectorName("Polish")]
            pl,

            [InspectorName("Turkish")]
            tr,

            [InspectorName("Russian")]
            ru,

            [InspectorName("Dutch")]
            nl,

            [InspectorName("Czech")]
            cs,

            [InspectorName("Arabic")]
            ar,

            [InspectorName("Chinese")]
            zh,

            [InspectorName("Hungarian")]
            hu,

            [InspectorName("Korean")]
            ko,
            [InspectorName("Hindi")]
            hi
        }
        public enum LocalLanguage
        {
            [InspectorName("English")]
            en,
        }
        public static Language language = Language.en;
        public static LocalLanguage locallanguage = LocalLanguage.en;
        //bool split = true;
        bool useVoiceCloning;
        float cfgScale = 0.7f;
        int eScale = 3, steps = 5;
        bool enableEScale;
        private bool initDone = false;
        private GUIStyle StatesLabel, styleError;
        public static bool running = false;
        private Vector2 mainScroll;
        string responseFromServer;
        float postProgress;
        bool postFlag;
        bool autoPath = true;
        string _directoryPath, fileName, bodyName = "Voice", voiceName = Voice.Clara.ToString(), take = "0";
        UnityEngine.Object currentAudioClip, lastAudioClip;
        Texture2D audioWaveForm, disabledWaveForm, audioSlider, previewClip;
        float scrubber = 0;
        bool updateScrubber;
        Texture button_play, button_pause, button_stop;
        float editorDeltaTime = 0f, lastTimeSinceStartup = 0f;
        bool fileExists;
        bool foldRequirements = true, foldTrimmer = false, foldJoiner = false, foldEqualizer = false;
        AudioClip clipToTrim, clipToClone;
        float trimMin, trimMax;
        string trimmedClipFileName;
        bool trimFileExists;
        string combinedClipFileName;
        string invoice;
        bool combineFileExists;
        [SerializeField]
        List<AudioClip> audioJoinList = new List<AudioClip>();
        AudioClip clipToEqualize;
        float volume, pitch;
        float[] bandFreqs = { 2400f, 2800f, 3200f, 3600f, 4000f, 8000f };
        List<float> gains = new List<float> { 1, 1, 1, 1, 1, 1 };
        string equalizedClipFileName;
        bool equalizeFileExists;
        bool previewVoices = false;
        int selGridMV = -1;
        int lastSelGridMV = -1;
        string[] previewXVoicesString = { "Clara", "Tamara", "Alice", "Anya", "Anne", "Ayla", "Brianna", "Greta", "Harriet", "Sophia",
    "Tamsin", "Tanya", "Violet", "Drew", "Basir", "Dino", "Royce", "Victor", "Abe", "Adam",
    "Balder", "Cade", "Damon", "Gil", "Ian", "Kaz", "Ludwig", "Sam", "Troy", "Vince", "Zack",
    "Noah", "Maya", "Una", "Lina", "Chance", "Sophie", "Camille", "Daisy", "Grace", "Lily",
    "Zoe", "Nell", "Bree", "Alexa", "Alma", "Rose", "Ike", "Phil", "Dajam", "Wolf", "Adrian",
    "Kamal", "Eugene", "Finn", "Xander", "Louie", "Mark"};
        void InitStyles()
        {
            initDone = true;
            StatesLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(),
                padding = new RectOffset(),
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
        }
        //string sid = "860vrnpvul3wxy", hid = "runpod", port = "5000", ext = "net", protocol = "https://";


        void Awake()
        {
#if UNITY_2022_1_OR_NEWER
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
#endif
            voiceName = voice.ToString();
            fileName = $"{voiceName}_{bodyName}_{take}";
            fileExists = false;
            _directoryPath = "Assets/VoiceGPT/Voices";
            var info = new DirectoryInfo(_directoryPath);
            var fileInfo = info.GetFiles();
            foreach (string file in System.IO.Directory.GetFiles(_directoryPath))
            {
                if ($"{_directoryPath}\\{fileName}.wav" == file.ToString())
                {
                    fileExists = true;
                }
            }
            invoice = PlayerPrefs.GetString("VoiceGPT_Invoice");
        }
        // create menu item and window
        [MenuItem("Window/VoiceGPT")]
        static void Init()
        {
            VoiceGPTEditor window = (VoiceGPTEditor)EditorWindow.GetWindow(typeof(VoiceGPTEditor));
            window.titleContent.text = "VoiceGPT";
            window.minSize = new Vector2(350, 300);
            running = true;
        }
        void OnGUI()
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            if (!initDone)
                InitStyles();
            GUIStyle style = new GUIStyle("WhiteLargeLabel");
            GUIStyle sectionTitle = style;
            GUIStyle subStyle = new GUIStyle("Label");
            subStyle.fontSize = 10;
            subStyle.normal.textColor = Color.white;
            sectionTitle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            GUIStyle headStyle = new GUIStyle("BoldLabel");
            headStyle.fontSize = 18;
            headStyle.normal.textColor = Color.white;
            EditorGUILayout.BeginHorizontal();
            Texture logo = (Texture)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/Logo.png", typeof(Texture));
            Texture infoToolTip = (Texture)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/Info.png", typeof(Texture));
            Texture2D disabledWaveForm = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/DisabledWaveform.png", typeof(Texture));
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("            VoiceGPT   ", headStyle);
            EditorGUILayout.LabelField("                  Version 2.0", subStyle);
            EditorGUILayout.EndVertical();
            GUI.DrawTexture(new Rect(10, 3, 45, 45), logo, ScaleMode.StretchToFill, true, 10.0F);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            GUILayout.BeginVertical("window");
            EditorGUILayout.LabelField("Voice Generator", sectionTitle);
            var tempCenter = GUILayoutUtility.GetLastRect().center.x;
            EditorGUILayout.Space(10);
            model = (Model)EditorGUILayout.EnumPopup(new GUIContent("Model", infoToolTip, "Select the text-to-speech (TTS) model file to use."), model);
            // if (model == Model.VoiceGPT_X)
            // {
            //     invoice = EditorGUILayout.TextField(new GUIContent("Invoice Number  ", infoToolTip, "Enter Invoice number. Invoice numbers start with \"IN\" and are 14 characters long. You can find them under Order History on the store. For a more detailed explaination, please refer to the documentation."), invoice);
            //     GUILayout.FlexibleSpace();
            //     GUILayout.BeginHorizontal();
            //     EditorGUILayout.LabelField("");
            //     if (GUILayout.Button("Verify", GUILayout.MaxWidth(48), GUILayout.MaxHeight(17)))
            //     {
            //         if (invoice.Length != 14)
            //             Debug.Log("Invalid Invoice Number. Please check your invoice number and try again. Please look into the documentation on how you can get your invoice number. An invoice number is exactly 14 characters long.");

            //         else if (invoice[0] != 'I')
            //             Debug.Log("Order Number / Invalid Input in Invoice Field detected. Please use your Invoice number. Please look into the documentation on how you can get your invoice number.");
            //         else
            //             this.StartCoroutine(Verify($"{protocol}{sid}-{port}.proxy.{hid}.{ext}/verify", "{\"invoice\":\"" + invoice + "\"}"));
            //     }
            //     if (GUILayout.Button("Save", GUILayout.MaxWidth(48), GUILayout.MaxHeight(17)))
            //         SaveInvoice();
            //     GUILayout.EndHorizontal();
            // }
            // else if (model == Model.VoiceGPT_Local)
            // {
                foldRequirements = FoldOuts.FoldOut("Pre-Requisites", foldRequirements);
                if (foldRequirements)
                {
                    EditorStyles.textArea.wordWrap = true;
                    EditorGUILayout.LabelField("Please make sure you have Python Scripting v7.0.1 installed.", EditorStyles.boldLabel);
                    if (GUILayout.Button("Open Project Settings"))
                    {
                        SettingsService.OpenProjectSettings("Project/Python Scripting");
                    }
                    GUILayout.Label(" - Click on Launch Terminal\n - Type in the following:", EditorStyles.boldLabel);
                    EditorGUILayout.TextField("pip install Assets/VoiceGPT/Models/vgpt-0.1.6-py3-none-any.whl", EditorStyles.textArea);
                    GUILayout.Label("After successful installation, proceed with voice generation. \nRefer to the bundled offline installation guide for any issues.");
                }
            //}
            EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);
            EditorStyles.textArea.wordWrap = true;

            // Replacing newlines, quotes and backslashes that previously tipped off an error in generation
            // if (model == Model.VoiceGPT_X)
            // {
            //     text = Regex.Replace(text, @"\\(?!n|"")", "");
            //     text = Regex.Replace(text, "(?<!n)\n", "\\n");
            //     text = Regex.Replace(text, "(?<!\\\\)\"", "\\\"");
            // }
            if (model == Model.VoiceGPT_Local)
            {
                text = Regex.Replace(text, @"\r?\n", "  ");
                text = Regex.Replace(text, "(?<!n)\n", "\\n");
                text = Regex.Replace(text, "(?<!\\\\)\"", "\\\"");
            }

            text = EditorGUILayout.TextArea(text, EditorStyles.textArea, GUILayout.Height(80));
            EditorGUILayout.BeginHorizontal();
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
            styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
            styleError.normal.textColor = Color.red;
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            // if (model == Model.VoiceGPT_X)
            //     charLimit = split ? 500 : 250;
            // else
            if (model == Model.VoiceGPT_Local)
                charLimit = 1000;
            if (charLimit - text.Length >= 0)
                EditorGUILayout.LabelField($"{charLimit - text.Length} char", style, GUILayout.MaxWidth(80));
            else
                EditorGUILayout.LabelField($"{charLimit - text.Length} char", styleError);
            // if (model == Model.VoiceGPT_X)
            //     if (GUILayout.Button("Status", GUILayout.MaxWidth(48), GUILayout.MaxHeight(17)))
            //         this.StartCoroutine(Status($"{protocol}{sid}-{port}.proxy.{hid}.{ext}/status", "{\"invoice\":\"" + invoice + "\"}"));
            GUILayout.EndHorizontal();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Voice Model Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(useVoiceCloning == true);
            voice = (Voice)EditorGUILayout.EnumPopup(new GUIContent("Voice", infoToolTip, "Selects the voice by name to use for the given model. Choose from a variety of different voices and find the best fit for your character."), voice);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                voiceName = voice.ToString();
                fileName = $"{voiceName}_{bodyName}_{take}";
            }
            // if (model == Model.VoiceGPT_X)
            //     language = (Language)EditorGUILayout.EnumPopup(new GUIContent("Language", infoToolTip, "Select the language you want to generate the audio in. English, as of now, has the best quality and performance."), language);
            // else
            if (model == Model.VoiceGPT_Local)
                locallanguage = (LocalLanguage)EditorGUILayout.EnumPopup(new GUIContent("Language", infoToolTip, "Select the language you want to generate the audio in. English, as of now, has the best quality and performance."), locallanguage);
            // if (model == Model.VoiceGPT_X)
            //     split = EditorGUILayout.Toggle(new GUIContent("Segment Semantics", infoToolTip, "Split text into sentences"), split);
            // else
            if (model == Model.VoiceGPT_Local)
            {
                cfgScale = EditorGUILayout.Slider(new GUIContent("CFG Scale", infoToolTip, "Adherence to the voice file input"), cfgScale, 0, 1);
                EditorGUILayout.BeginHorizontal("Box");
                EditorGUI.BeginDisabledGroup(!enableEScale);
                eScale = EditorGUILayout.IntSlider(new GUIContent("Emotional Variation", infoToolTip, "Experimental! Higher values exhibit more emotional state."), eScale, 1, 10);
                EditorGUI.EndDisabledGroup();
                enableEScale = EditorGUILayout.ToggleLeft("", enableEScale, GUILayout.MaxWidth(10));
                EditorGUILayout.EndHorizontal();
                steps = EditorGUILayout.IntSlider(new GUIContent("Steps", infoToolTip, "Diffusion Steps: Higher the number, higher the quality but at the cost of time"), steps, 2, 30);
            }
            EditorGUILayout.Space(10);
            previewVoices = FoldOuts.FoldOut("VoiceGPT Preview Voices", previewVoices);
            if (previewVoices)
            {
                GUILayout.BeginHorizontal("Box");
                selGridMV = GUILayout.SelectionGrid(selGridMV, previewXVoicesString, 3, GUILayout.MinWidth(100));
                if (selGridMV != lastSelGridMV)
                {
                    StopAllClips();
                    PlayClip((AudioClip)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Voices/Preview Voices/" + previewXVoicesString[selGridMV] + ".wav", typeof(AudioClip)), 0, false);
                    lastSelGridMV = selGridMV;
                    voice = (Voice)Enum.Parse(typeof(Voice), previewXVoicesString[selGridMV].Replace(" ", "_"));
                    voiceName = voice.ToString();
                    fileName = $"{voice}_{bodyName}_{take}";
                }
                GUILayout.EndHorizontal();

            }
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Voice Cloning", EditorStyles.boldLabel);
            useVoiceCloning = EditorGUILayout.Toggle(new GUIContent("Enable Voice Cloning", infoToolTip, "Enables the Voice Cloning field and disables the voice field."), useVoiceCloning);
            EditorGUI.BeginDisabledGroup(useVoiceCloning == false);
            GUILayout.BeginHorizontal();
            clipToClone = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Clone Voice", infoToolTip, "Select an audio file you wish to clone. Make sure it is in the .wav format and is at least 3-9 seconds long. A lomger audio file does not produce a better result. For maximum quality, aim for an audio file between 6-15 seconds."), clipToClone, typeof(AudioClip), true);
            if (GUILayout.Button("Active Clip", GUILayout.MaxWidth(80)) && Selection.activeObject != null && Selection.activeObject.GetType().Equals(typeof(AudioClip)))
                clipToClone = (AudioClip)Selection.activeObject;
            if (GUILayout.Button("×", EditorStyles.boldLabel, GUILayout.MaxWidth(20)))
                clipToClone = null;
            GUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            voiceName = EditorGUILayout.TextField(new GUIContent("File Name", infoToolTip, "Automatically assigns the file name based on the selected voice. Additionally, increments the take field by +1 upon voice processing"), voiceName);
            bodyName = EditorGUILayout.TextField(bodyName);
            take = EditorGUILayout.TextField(take);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{voiceName}_{bodyName}_{take}.wav", style);
            fileName = $"{voiceName}_{bodyName}_{take}";
            if (EditorGUI.EndChangeCheck())
            {
                //Check all files for name existence

                fileExists = false;
                var info = new DirectoryInfo(_directoryPath);
                var fileInfo = info.GetFiles();
                foreach (string file in System.IO.Directory.GetFiles(_directoryPath))
                {
                    if ($"{_directoryPath}\\{fileName}.wav" == file.ToString())
                    {
                        fileExists = true;
                    }
                }
            }

            if (fileExists)
            {
                styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
                styleError.normal.textColor = Color.red;
                EditorGUILayout.LabelField(new GUIContent("[Overwrite Name]", infoToolTip, "This file name already exists. Clicking on generate will overwrite and replace the current file. Proceed with caution."), styleError, GUILayout.Width(100));
            }
            else
            {
                styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
                styleError.normal.textColor = Color.green;
                EditorGUILayout.LabelField(new GUIContent("[Available Name]", infoToolTip, "This file name is available to use."), styleError, GUILayout.Width(100));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(autoPath == true);
            if (autoPath)
                _directoryPath = EditorGUILayout.TextField("Voices Folder", "Assets/VoiceGPT/Voices");
            else
                _directoryPath = EditorGUILayout.TextField("Voices Folder", _directoryPath);
            if (GUILayout.Button(". . /", GUILayout.MaxWidth(50)))
                _directoryPath = EditorUtility.OpenFolderPanel("", "", "");
            EditorGUI.EndDisabledGroup();
            autoPath = EditorGUILayout.ToggleLeft("Auto", autoPath, GUILayout.MaxWidth(50));

            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginDisabledGroup(text == "" || useVoiceCloning && clipToClone == null || postFlag);
            if (GUILayout.Button("Generate Voice", GUILayout.Height(30)))
            {
                // if (model == Model.VoiceGPT_X)
                // {
                //     invoice = invoice.Replace(" ", "");
                //     if (invoice.Length != 14)
                //         Debug.Log("Invalid Invoice Number. Please check your invoice number and try again. Please look into the documentation on how you can get your invoice number. An invoice number is exactly 14 characters long.");

                //     else if (invoice[0] != 'I')
                //         Debug.Log("Order Number / Invalid Input in Invoice Field detected. Please use your Invoice number. Please look into the documentation on how you can get your invoice number.");


                //     else
                //     {
                //         postFlag = true;
                //         postProgress = 0;

                //         if (useVoiceCloning == false || clipToClone == null)
                //             this.StartCoroutine(Post($"{protocol}{sid}-{port}.proxy.{hid}.{ext}/data", "{\"prompt\":\"" + $"{text}" + "\",\"language\":\"" + $"{language}" + "\",\"invoice\":\"" + $"{invoice}" + "\",\"voice\":\"" + $"{voice.ToString().Replace('_', ' ')}" + "\",\"split\":\"" + $"{split}" + "\",\"clone\":\"" + $"" + "\"}"));

                //         else
                //         {
                //             string audioAssetPath = AssetDatabase.GetAssetPath(clipToClone);
                //             string extension = Path.GetExtension(audioAssetPath);
                //             if (extension.ToLower() != ".wav")
                //             {
                //                 Debug.Log($"[Voice Cloning] - Please select a .wav file. The file you have selected is of the format {extension}.");
                //                 postProgress = 1;
                //                 postFlag = false;
                //             }
                //             else
                //             {
                //                 string b64wav = Convert.ToBase64String(File.ReadAllBytes(AssetDatabase.GetAssetPath(clipToClone)));
                //                 this.StartCoroutine(Post($"{protocol}{sid}-{port}.proxy.{hid}.{ext}/data", "{\"prompt\":\"" + $"{text}" + "\",\"language\":\"" + $"{language}" + "\",\"invoice\":\"" + $"{invoice}" + "\",\"voice\":\"" + $"" + "\",\"split\":\"" + $"{split}" + "\",\"clone\":\"" + $"{b64wav}" + "\"}"));
                //             }

                //         }
                //     }
                // }
                if (model == Model.VoiceGPT_Local)
                {
                    string filePath = Path.Combine("Assets/VoiceGPT/Models/config.txt");

                    try
                    {
                        using (StreamWriter writer = new StreamWriter(filePath))
                        {
                            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
                            writer.WriteLine($"Model = \"{Application.dataPath}/VoiceGPT/Models/Model.pth\"");
                            writer.WriteLine($"Config = \"{Application.dataPath}/VoiceGPT/Models/ModelConfig.yml\"");
                            writer.WriteLine($"ASRModel = \"{Application.dataPath}/VoiceGPT/Models/ASRModel.pth\"");
                            writer.WriteLine($"ASRConfig = \"{Application.dataPath}/VoiceGPT/Models/ASRConfig.yml\"");
                            writer.WriteLine($"F0Model = \"{Application.dataPath}/VoiceGPT/Models/F0Model.t7\"");
                            writer.WriteLine($"BERTModel = \"{Application.dataPath}/VoiceGPT/Models/BERTModel.t7\"");
                            writer.WriteLine($"BERTConfig = \"{Application.dataPath}/VoiceGPT/Models/BERTConfig.yml\"");
                            writer.WriteLine("_text = \"\"\"" + text + "\"\"\"");
                            if (useVoiceCloning)
                                writer.WriteLine("_targetVoice = \"" + Application.dataPath.Substring(0, Application.dataPath.Length - 6) + AssetDatabase.GetAssetPath(clipToClone) + "\"");
                            else
                                writer.WriteLine("_targetVoice = \"" + $"{Application.dataPath}/VoiceGPT/Voices/Preview Voices/" + voiceName.Replace("_", " ") + ".wav\"");
                            writer.WriteLine($"_outputPath = \"{Application.dataPath}/VoiceGPT/Voices/{fileName}" + ".wav\"");
                            writer.WriteLine($"_eScale = {eScale}");
                            writer.WriteLine($"_alpha = {1 - cfgScale}");
                            writer.WriteLine($"_beta = {cfgScale}");
                            writer.WriteLine($"_steps = {steps}");
                            string pythonFormatBool = enableEScale ? "True" : "False";
                            writer.WriteLine("_enableEScale = " + pythonFormatBool);


                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("Error writing variables to file: " + e.Message);
                    }
                    PythonRunner.RunFile(@"Assets/VoiceGPT/Models/main.py");
                    AssetDatabase.Refresh();
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<AudioClip>($"{_directoryPath}\\{fileName}.wav");
                    take = (int.Parse(take) + 1).ToString();
                }
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.Space(10);
            Rect loading = GUILayoutUtility.GetRect(9, 9);
            if (postFlag)
            {
                Repaint();
                EditorGUI.ProgressBar(loading, Mathf.Sqrt(++postProgress) * 0.009f, "");
            }
            GUILayout.EndVertical();
            GUILayout.Space(10);
            currentAudioClip = Selection.activeObject;
            EditorGUI.BeginDisabledGroup(Selection.activeObject == null || !Selection.activeObject.GetType().Equals(typeof(AudioClip)) || currentAudioClip == null);
            GUILayout.BeginVertical("window");
            EditorGUILayout.LabelField(new GUIContent("Preview", infoToolTip, "The preview section helps you preview the sound files without leaving this interface. To access it, single-click on a file in the project and hover over this panel. You will see this section enabled. Scrub the playhead to preview different sections of the audio."), sectionTitle);
            GUILayout.Space(100);


            if (Selection.activeObject != null && Selection.activeObject.GetType().Equals(typeof(AudioClip)) && lastAudioClip != currentAudioClip)
            {
                AudioClip sound = (AudioClip)Selection.activeObject;
                audioWaveForm = PaintWaveformSpectrum(sound, Screen.width / 4, 100, new Color(1, 0.55f, 0), false, 0);

                scrubber = 0;
                audioSlider = PaintWaveformSpectrum(sound, Screen.width / 4, 100, new Color(1, 1, 1), true, scrubber / sound.length);
            }

            lastAudioClip = currentAudioClip;


            if (Selection.activeObject != null && Selection.activeObject.GetType().Equals(typeof(AudioClip)) && currentAudioClip != null)
            {
                GUI.DrawTexture(new Rect(tempCenter - (Screen.width / 4), GUILayoutUtility.GetLastRect().y, Screen.width / 2, 100), audioWaveForm, ScaleMode.StretchToFill, true, 4);
                GUI.DrawTexture(new Rect(tempCenter - (Screen.width / 4), GUILayoutUtility.GetLastRect().y, Screen.width / 2, 100), audioSlider, ScaleMode.StretchToFill, true, 4);
                AudioClip sound = (AudioClip)Selection.activeObject;
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(sound.name.ToString() + ", " + sound.frequency.ToString() + "Hz, " + sound.length.ToString().Substring(0, 4) + "s", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                this.button_play = (Texture)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/Play.png", typeof(Texture));
                this.button_pause = (Texture)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/Pause.png", typeof(Texture));
                this.button_stop = (Texture)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/Stop.png", typeof(Texture));

                if (GUILayout.Button(new GUIContent(button_play), GUILayout.Width(25), GUILayout.Height(25)))
                {
                    if (!updateScrubber)
                    {
                        PlayClip((AudioClip)currentAudioClip, Mathf.CeilToInt((scrubber / sound.length) * sound.samples), false);
                        if (Mathf.Approximately(scrubber, sound.length))
                            scrubber = 0;
                    }
                    updateScrubber = true;
                }
                if (GUILayout.Button(new GUIContent(button_pause), GUILayout.Width(25), GUILayout.Height(25)))
                {
                    StopAllClips();
                    updateScrubber = false;
                }
                if (GUILayout.Button(new GUIContent(button_stop), GUILayout.Width(25), GUILayout.Height(25)))
                {
                    StopAllClips();
                    updateScrubber = false;
                    scrubber = 0;
                    audioSlider = PaintWaveformSpectrum(sound, Screen.width / 4, 100, new Color(1, 1, 1), true, scrubber / sound.length);
                }
                if (!updateScrubber)
                    lastTimeSinceStartup = 0f;
                if (scrubber > sound.length)
                {
                    updateScrubber = false;
                }
                if (updateScrubber)
                {
                    if (lastTimeSinceStartup == 0f)
                        lastTimeSinceStartup = (float)EditorApplication.timeSinceStartup;
                    editorDeltaTime = (float)EditorApplication.timeSinceStartup - lastTimeSinceStartup;
                    lastTimeSinceStartup = (float)EditorApplication.timeSinceStartup;
                    scrubber += editorDeltaTime;
                    audioSlider = PaintWaveformSpectrum(sound, Screen.width / 4, 100, new Color(1, 1, 1), true, scrubber / sound.length);

                    //Prevent edge case for the sound clip looping if the scrubber is played while it is at the end of the playhead (~16000 samples)
                    if (sound.samples - Mathf.CeilToInt((scrubber / sound.length) * sound.samples) < 100)
                        StopAllClips();

                    Repaint();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

            }

            else
            {
                GUI.DrawTexture(new Rect(tempCenter - 100, GUILayoutUtility.GetLastRect().y, 200, 100), disabledWaveForm, ScaleMode.ScaleToFit, true, 1.5f);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select an audio file from the project", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                this.button_play = (Texture)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/Play.png", typeof(Texture));
                this.button_pause = (Texture)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/Pause.png", typeof(Texture));
                this.button_stop = (Texture)AssetDatabase.LoadAssetAtPath("Assets/VoiceGPT/Editor/Resources/Stop.png", typeof(Texture));
                GUILayout.Button(new GUIContent(button_play), GUILayout.Width(25), GUILayout.Height(25));
                GUILayout.Button(new GUIContent(button_pause), GUILayout.Width(25), GUILayout.Height(25));
                GUILayout.Button(new GUIContent(button_stop), GUILayout.Width(25), GUILayout.Height(25));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();


            }
            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Playhead");

            if (Selection.activeObject != null && Selection.activeObject.GetType().Equals(typeof(AudioClip)))
            {
                AudioClip clip = (AudioClip)Selection.activeObject;
                EditorGUI.BeginChangeCheck();
                scrubber = EditorGUILayout.Slider(scrubber, 0, clip.length, GUILayout.Width(120));
                if (EditorGUI.EndChangeCheck())
                {
                    audioSlider = PaintWaveformSpectrum(clip, Screen.width / 4, 100, new Color(1, 1, 1), true, scrubber / clip.length);
                    updateScrubber = false;
                    StopAllClips();
                }
            }
            else
                scrubber = EditorGUILayout.Slider(scrubber, 0, 1, GUILayout.Width(120));
            GUILayout.Label("s");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUI.EndDisabledGroup();
            GUILayout.BeginVertical("window");
            EditorGUILayout.LabelField("Audio Utility", sectionTitle);
            GUILayout.Space(10);

            GUILayout.Space(10);

            foldTrimmer = FoldOuts.FoldOut("Audio Trimmer", foldTrimmer);
            if (foldTrimmer)
            {
                EditorGUI.BeginChangeCheck();
                GUILayout.BeginHorizontal();
                clipToTrim = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Clip To Trim", infoToolTip, "Select an audio file you wish to trim. Once selected, use the slider to cut portions of the audio. When satisfied, save the audio by entering a valid name for the audio file. Click on \"Active Clip\" button to select the clip active in the project. To remove the selection, simply click on the x button on the right side of the clip selection field."), clipToTrim, typeof(AudioClip), true);

                if (GUILayout.Button("Active Clip", GUILayout.MaxWidth(80)) && Selection.activeObject != null && Selection.activeObject.GetType().Equals(typeof(AudioClip)))
                {
                    clipToTrim = (AudioClip)Selection.activeObject;
                    trimMax = 1;
                }

                if (GUILayout.Button("×", EditorStyles.boldLabel, GUILayout.MaxWidth(20)))
                {
                    clipToTrim = null;
                    previewClip = null;
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(120);
                if (EditorGUI.EndChangeCheck() && clipToTrim != null)
                {
                    previewClip = PaintWaveformSpectrum(clipToTrim, Screen.width / 4, 100, new Color(1, 0.55f, 0), false, 0);
                }
                float previewClipRectX = GUILayoutUtility.GetLastRect().x;
                EditorGUI.BeginDisabledGroup(previewClip == null);
                if (previewClip != null)
                {
                    float trimLastRectY = GUILayoutUtility.GetLastRect().y + 20;
                    GUI.DrawTexture(new Rect(tempCenter - Screen.width / 4, trimLastRectY, Screen.width / 2, 100), previewClip, ScaleMode.StretchToFill, true, 1);
                    GUI.DrawTexture(new Rect(tempCenter - Screen.width / 4, trimLastRectY, Screen.width / 2, 100), PaintWaveformSpectrum(clipToTrim, Screen.width / 4, 100, new Color(0, 0, 0), true, trimMin), ScaleMode.StretchToFill, true, 1);
                    GUI.DrawTexture(new Rect(tempCenter - Screen.width / 4, trimLastRectY, Screen.width / 2, 100), PaintWaveformSpectrum(clipToTrim, Screen.width / 4, 100, new Color(0, 0, 0), true, trimMax, true), ScaleMode.StretchToFill, true, 1);
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.MinMaxSlider(ref trimMin, ref trimMax, 0, 1, GUILayout.MaxWidth(Screen.width * 0.705f));
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Start:");
                    EditorGUILayout.FloatField(trimMin * clipToTrim.length, GUILayout.MaxWidth(50));
                    GUILayout.Label("s");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("End:");
                    EditorGUILayout.FloatField(trimMax * clipToTrim.length, GUILayout.MaxWidth(50));
                    GUILayout.Label("s");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent(button_play), GUILayout.Width(25), GUILayout.Height(25)))
                    {
                        AudioClip ac = AudioClip.Create("Temp", Mathf.CeilToInt(trimMax * clipToTrim.samples) - Mathf.CeilToInt(trimMin * clipToTrim.samples), clipToTrim.channels, clipToTrim.frequency, false);
                        float[] samples = new float[Mathf.CeilToInt(trimMax * clipToTrim.samples) - Mathf.CeilToInt(trimMin * clipToTrim.samples)];
                        clipToTrim.GetData(samples, Mathf.CeilToInt(trimMin * clipToTrim.samples));
                        ac.SetData(samples, 0);
                        if (!Directory.Exists(_directoryPath + "/Temp_data")) Directory.CreateDirectory(_directoryPath + "/Temp_data");
                        WaveUtils.Save("TempTrim", ac, _directoryPath + "/Temp_data", false);
                        AssetDatabase.Refresh();

                        AudioClip temp = (AudioClip)AssetDatabase.LoadAssetAtPath(_directoryPath + "/Temp_data/TempTrim.wav", typeof(AudioClip));
                        //System.Reflection cannot play audio samples stored in local variables hence the method above 
                        PlayClip(temp, 0, false);
                    }
                    if (GUILayout.Button(new GUIContent(button_stop), GUILayout.Width(25), GUILayout.Height(25)))
                    {
                        StopAllClips();
                    }
                    if (GUILayout.Button("Reset", GUILayout.Width(50), GUILayout.Height(25)))
                    {
                        trimMin = 0;
                        trimMax = 1;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    EditorGUI.BeginChangeCheck();
                    trimmedClipFileName = EditorGUILayout.TextField("File Name", trimmedClipFileName);
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{trimmedClipFileName}.wav", style);
                    if (EditorGUI.EndChangeCheck())
                    {
                        //Check all files for name existence
                        fileName = $"{trimmedClipFileName}";

                        trimFileExists = false;
                        var info = new DirectoryInfo(_directoryPath);
                        var fileInfo = info.GetFiles();
                        foreach (string file in System.IO.Directory.GetFiles(_directoryPath))
                        {
                            if ($"{_directoryPath}\\{fileName}.wav" == file.ToString())
                            {
                                trimFileExists = true;
                            }
                        }
                    }

                    if (trimFileExists)
                    {
                        styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
                        styleError.normal.textColor = Color.red;
                        EditorGUILayout.LabelField($"[Overwrite Name]", styleError, GUILayout.Width(100));
                    }
                    else
                    {
                        styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
                        if (trimmedClipFileName == "" || trimmedClipFileName == null)
                        {
                            styleError.normal.textColor = Color.red;
                            EditorGUILayout.LabelField($"[Cannot be Empty]", styleError, GUILayout.Width(100));
                        }
                        else
                        {
                            styleError.normal.textColor = Color.green;
                            EditorGUILayout.LabelField($"[Available Name]", styleError, GUILayout.Width(100));
                        }
                    }
                    GUILayout.EndHorizontal();
                    EditorGUI.BeginDisabledGroup(trimmedClipFileName == "" || trimmedClipFileName == null);
                    if (GUILayout.Button("Save Trimmed Audio", GUILayout.Height(30)))
                    {
                        AudioClip ac = AudioClip.Create("Temp", Mathf.CeilToInt(trimMax * clipToTrim.samples) - Mathf.CeilToInt(trimMin * clipToTrim.samples), clipToTrim.channels, clipToTrim.frequency, false);
                        float[] samples = new float[Mathf.CeilToInt(trimMax * clipToTrim.samples) - Mathf.CeilToInt(trimMin * clipToTrim.samples)];
                        clipToTrim.GetData(samples, Mathf.CeilToInt(trimMin * clipToTrim.samples));
                        ac.SetData(samples, 0);
                        WaveUtils.Save(trimmedClipFileName, ac, _directoryPath, false);
                        AssetDatabase.Refresh();
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.Space(10);

                }
                else
                {
                    GUI.DrawTexture(new Rect(tempCenter - Screen.width * 0.25f, GUILayoutUtility.GetLastRect().y + 20, Screen.width * 0.5f, 100), disabledWaveForm, ScaleMode.ScaleToFit, true, 1.5f);
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.MinMaxSlider(ref trimMin, ref trimMax, 0, 1, GUILayout.MaxWidth(Screen.width * 0.725f));
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Start:");
                    EditorGUILayout.FloatField(trimMin * 0, GUILayout.MaxWidth(50));
                    GUILayout.Label("s");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("End:");
                    EditorGUILayout.FloatField(trimMax * 1, GUILayout.MaxWidth(50));
                    GUILayout.Label("s");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Button(new GUIContent(button_play), GUILayout.Width(25), GUILayout.Height(25));
                    GUILayout.Button(new GUIContent(button_stop), GUILayout.Width(25), GUILayout.Height(25));
                    GUILayout.Button("Reset", GUILayout.Width(50), GUILayout.Height(25));
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Button("Save Trimmed Audio", GUILayout.Height(30));
                }

                EditorGUI.EndDisabledGroup();

            }
            foldJoiner = FoldOuts.FoldOut("Audio Joiner", foldJoiner);
            if (foldJoiner)
            {
                string[] selectionOfAudioClips = Selection.assetGUIDs;
                EditorGUI.BeginDisabledGroup(selectionOfAudioClips.Length < 2);
                ScriptableObject target = this;
                SerializedObject so = new SerializedObject(target);
                SerializedProperty stringsProperty = so.FindProperty("audioJoinList");
                GUILayout.BeginHorizontal();

                EditorGUILayout.PropertyField(stringsProperty, new GUIContent("Audio Clips to Join", infoToolTip, "Select two or more audio files you wish to combine. Select the audio files from the project and click on \"Set Selected\" to auto populate the queue with the selected files. Please note that you cannot manually assign clips using the editor, you may only use the Set Selected Button to assign clips in this version of the asset. You can rearrange the audio clips in the hierarchy by dragging the clips. Once satisfied with the arrangement of the clips, enter a suitable name and save the file. You can clear the queue using the x button on the right of the Set Selected Button."), true, GUILayout.MaxWidth(300));
                so.ApplyModifiedProperties();
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent("Set Selected", null, "Select two or more clips in the project to enable button")))
                {
                    audioJoinList.Clear();
                    for (int i = 0; i < selectionOfAudioClips.Length; i++)
                    {
                        audioJoinList.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(selectionOfAudioClips[i])));
                    }

                }
                EditorGUI.EndDisabledGroup();
                bool clearList = false;
                if (GUILayout.Button("×", EditorStyles.boldLabel, GUILayout.MaxWidth(20)))
                {
                    clearList = true;
                }
                GUILayout.EndHorizontal();
                bool anyClipNull = false;
                float combinedTime = 0;
                for (int i = 0; i < audioJoinList.Count; i++)
                {
                    if (audioJoinList[i] == null)
                        anyClipNull = true;
                }
                if (!anyClipNull)
                {
                    for (int i = 0; i < audioJoinList.Count; i++)
                    {
                        combinedTime += audioJoinList[i].length;
                        GUILayout.Label(audioJoinList[i].length.ToString() + "s", GUILayout.Height(18));
                    }
                }
                EditorGUI.BeginDisabledGroup(stringsProperty.arraySize == 0);
                GUILayout.Label(combinedTime.ToString().Length > 5 ? combinedTime.ToString().Substring(0, 5) + "s [Total]" : "Total Time", EditorStyles.boldLabel, GUILayout.Height(18));
                EditorGUI.EndDisabledGroup();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                if (clearList)
                    audioJoinList.Clear();

                EditorGUI.BeginDisabledGroup(audioJoinList.Count < 2 || anyClipNull);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent(button_play), GUILayout.Width(25), GUILayout.Height(25)))
                {
                    int totalSamplesCount = 0;

                    for (int i = 0; i < audioJoinList.Count; i++)
                        totalSamplesCount += audioJoinList[i].samples;


                    AudioClip ac = AudioClip.Create("Temp", totalSamplesCount, audioJoinList[0].channels, audioJoinList[0].frequency, false);

                    float[] concatenatedSamples = audioJoinList.Select(audioClip =>
                    {
                        float[] samples = new float[audioClip.samples * audioClip.channels];
                        audioClip.GetData(samples, 0);
                        return samples;
                    })
                    .SelectMany(x => x)
                    .ToArray();

                    ac.SetData(concatenatedSamples, 0);
                    if (!Directory.Exists(_directoryPath + "/Temp_data")) Directory.CreateDirectory(_directoryPath + "/Temp_data");
                    WaveUtils.Save("TempCombine", ac, _directoryPath + "/Temp_data", false);
                    AssetDatabase.Refresh();

                    AudioClip temp = (AudioClip)AssetDatabase.LoadAssetAtPath(_directoryPath + "/Temp_data/TempCombine.wav", typeof(AudioClip));
                    PlayClip(temp, 0, false);

                }
                if (GUILayout.Button(new GUIContent(button_stop), GUILayout.Width(25), GUILayout.Height(25)))
                {
                    StopAllClips();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();


                EditorGUI.BeginChangeCheck();
                combinedClipFileName = EditorGUILayout.TextField("File Name", combinedClipFileName);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{combinedClipFileName}.wav", style);
                if (EditorGUI.EndChangeCheck())
                {
                    //Check all files for name existence
                    fileName = $"{combinedClipFileName}";

                    combineFileExists = false;
                    var info = new DirectoryInfo(_directoryPath);
                    var fileInfo = info.GetFiles();
                    foreach (string file in System.IO.Directory.GetFiles(_directoryPath))
                    {
                        if ($"{_directoryPath}\\{fileName}.wav" == file.ToString())
                        {
                            combineFileExists = true;
                        }
                    }
                }

                if (combineFileExists)
                {
                    styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
                    styleError.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"[Overwrite Name]", styleError, GUILayout.Width(100));
                }
                else
                {
                    styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
                    if (combinedClipFileName == "" || combinedClipFileName == null)
                    {
                        styleError.normal.textColor = Color.red;
                        EditorGUILayout.LabelField($"[Cannot be Empty]", styleError, GUILayout.Width(100));
                    }
                    else
                    {
                        styleError.normal.textColor = Color.green;
                        EditorGUILayout.LabelField($"[Available Name]", styleError, GUILayout.Width(100));
                    }
                }
                GUILayout.EndHorizontal();



                EditorGUI.BeginDisabledGroup(combinedClipFileName == "" || combinedClipFileName == null);
                if (GUILayout.Button("Save Combined Audio", GUILayout.Height(30)))
                {
                    int totalSamplesCount = 0;

                    for (int i = 0; i < audioJoinList.Count; i++)
                        totalSamplesCount += audioJoinList[i].samples;


                    AudioClip ac = AudioClip.Create("Temp", totalSamplesCount, audioJoinList[0].channels, audioJoinList[0].frequency, false);

                    float[] concatenatedSamples = audioJoinList.Select(audioClip =>
                    {
                        float[] samples = new float[audioClip.samples * audioClip.channels];
                        audioClip.GetData(samples, 0);
                        return samples;
                    })
                    .SelectMany(x => x)
                    .ToArray();

                    ac.SetData(concatenatedSamples, 0);
                    WaveUtils.Save(combinedClipFileName, ac, _directoryPath, false);
                    AssetDatabase.Refresh();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndDisabledGroup();
                GUILayout.Space(10);

            }


            foldEqualizer = FoldOuts.FoldOut("Audio Equalizer", foldEqualizer);
            if (foldEqualizer)
            {
                GUILayout.BeginHorizontal();
                clipToEqualize = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Clip to Equalize", infoToolTip, "Select an audio file you wish to equalize. You can adjust the sliders to make the voice loud, low, bassy or shrill. Once satisfied with the changes, enter a suitable name and save the file. You can reset the settings for the equalizer using the reset button."), clipToEqualize, typeof(AudioClip), true);

                if (GUILayout.Button("Active Clip", GUILayout.MaxWidth(80)) && Selection.activeObject != null && Selection.activeObject.GetType().Equals(typeof(AudioClip)))
                {
                    clipToEqualize = (AudioClip)Selection.activeObject;
                }

                if (GUILayout.Button("×", EditorStyles.boldLabel, GUILayout.MaxWidth(20)))
                {
                    clipToEqualize = null;
                }

                GUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(clipToEqualize == null);
                volume = EditorGUILayout.Slider("Gain Volume", volume, -10, 10, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField("dB", style, GUILayout.MaxWidth(20));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                pitch = EditorGUILayout.Slider("Pitch", pitch, -12, 12);
                EditorGUILayout.LabelField("ST", style, GUILayout.MaxWidth(20));
                EditorGUILayout.EndHorizontal();
                sectionTitle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Parametric EQ", sectionTitle);
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int i = 0; i < 6; i++)
                {
                    GUILayout.BeginVertical();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(" ", GUILayout.MaxWidth((bandFreqs[i].ToString().Length - 2) * (bandFreqs[i].ToString().Length - 1))); // Done for allignment purposes
                    gains[5 - i] = GUILayout.VerticalSlider(gains[5 - i], 0.5f, 1.5f, GUILayout.Height(100));
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Label(bandFreqs[i].ToString());
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent(button_play), GUILayout.Width(25), GUILayout.Height(25)))
                {

                    float[] samples = new float[clipToEqualize.samples * clipToEqualize.channels];
                    clipToEqualize.GetData(samples, 0);

                    int numBands = 6;
                    float[] bandQs = { 1f, 1f, 1f, 1f, 1f, 1f };

                    for (int i = 0; i < numBands; i++)
                    {
                        float freq = bandFreqs[i];
                        float q = bandQs[i];
                        float gain = gains[i];

                        float w0 = 2 * Mathf.PI * freq / clipToEqualize.frequency;
                        float alpha = Mathf.Sin(w0) / (2 * q);

                        float b0 = 1 + alpha * gain;
                        float b1 = -2 * Mathf.Cos(w0);
                        float b2 = 1 - alpha * gain;
                        float a0 = 1 + alpha / gain;
                        float a1 = -2 * Mathf.Cos(w0);
                        float a2 = 1 - alpha / gain;

                        float x1 = 0;
                        float x2 = 0;
                        float y1 = 0;
                        float y2 = 0;

                        for (int j = 0; j < samples.Length; j++)
                        {
                            float x0 = samples[j];
                            float y0 = (b0 / a0) * x0 + (b1 / a0) * x1 + (b2 / a0) * x2
                                       - (a1 / a0) * y1 - (a2 / a0) * y2;

                            x2 = x1;
                            x1 = x0;
                            y2 = y1;
                            y1 = y0;

                            samples[j] = y0;
                        }
                    }

                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] *= Mathf.Pow(10, volume / 20); //Decibel Conversion
                    }

                    // Apply the pitch shift to the sample data
                    float[] pitchedSamples = new float[Mathf.CeilToInt(samples.Length * Mathf.Pow(2, -pitch / 12))];
                    for (int i = 0; i < pitchedSamples.Length; i++)
                    {
                        float oldIndex = (float)i / Mathf.Pow(2, -pitch / 12); //Semitone Conversion
                        int index = Mathf.FloorToInt(oldIndex);
                        float t = oldIndex - index;

                        if (index >= samples.Length - 1)
                        {
                            pitchedSamples[i] = samples[samples.Length - 1];
                        }
                        else
                        {
                            pitchedSamples[i] = Mathf.Lerp(samples[index], samples[index + 1], t);
                        }
                    }


                    AudioClip ac = AudioClip.Create("Temp", pitchedSamples.Length, clipToEqualize.channels, clipToEqualize.frequency, false);
                    ac.SetData(pitchedSamples, 0);
                    if (!Directory.Exists(_directoryPath + "/Temp_data")) Directory.CreateDirectory(_directoryPath + "/Temp_data");
                    WaveUtils.Save("TempEqualize", ac, _directoryPath + "/Temp_data", false);
                    AssetDatabase.Refresh();
                    AudioClip temp = (AudioClip)AssetDatabase.LoadAssetAtPath(_directoryPath + "/Temp_data/TempEqualize.wav", typeof(AudioClip));
                    PlayClip(temp, 0, false);

                }
                if (GUILayout.Button(new GUIContent(button_stop), GUILayout.Width(25), GUILayout.Height(25)))
                {
                    StopAllClips();
                }
                if (GUILayout.Button("Reset", GUILayout.Width(50), GUILayout.Height(25)))
                {
                    volume = 0;
                    pitch = 0;
                    for (int i = 0; i < 6; i++)
                        gains[i] = 1;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();


                EditorGUI.BeginChangeCheck();
                equalizedClipFileName = EditorGUILayout.TextField("File Name", equalizedClipFileName);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{equalizedClipFileName}.wav", style);
                if (EditorGUI.EndChangeCheck())
                {
                    //Check all files for name existence
                    fileName = $"{equalizedClipFileName}";

                    equalizeFileExists = false;
                    var info = new DirectoryInfo(_directoryPath);
                    var fileInfo = info.GetFiles();
                    foreach (string file in System.IO.Directory.GetFiles(_directoryPath))
                    {
                        if ($"{_directoryPath}\\{fileName}.wav" == file.ToString())
                        {
                            equalizeFileExists = true;
                        }
                    }
                }

                if (equalizeFileExists)
                {
                    styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
                    styleError.normal.textColor = Color.red;
                    EditorGUILayout.LabelField($"[Overwrite Name]", styleError, GUILayout.Width(100));
                }
                else
                {
                    styleError = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 10 };
                    if (equalizedClipFileName == "" || equalizedClipFileName == null)
                    {
                        styleError.normal.textColor = Color.red;
                        EditorGUILayout.LabelField($"[Cannot be Empty]", styleError, GUILayout.Width(100));
                    }
                    else
                    {
                        styleError.normal.textColor = Color.green;
                        EditorGUILayout.LabelField($"[Available Name]", styleError, GUILayout.Width(100));
                    }
                }
                GUILayout.EndHorizontal();



                EditorGUI.BeginDisabledGroup(equalizedClipFileName == "" || equalizedClipFileName == null);
                if (GUILayout.Button("Save Equalized Audio", GUILayout.Height(30)))
                {
                    float[] samples = new float[clipToEqualize.samples * clipToEqualize.channels];
                    clipToEqualize.GetData(samples, 0);

                    int numBands = 6;
                    float[] bandQs = { 1f, 1f, 1f, 1f, 1f, 1f };

                    for (int i = 0; i < numBands; i++)
                    {
                        float freq = bandFreqs[i];
                        float q = bandQs[i];
                        float gain = gains[i];

                        float w0 = 2 * Mathf.PI * freq / clipToEqualize.frequency;
                        float alpha = Mathf.Sin(w0) / (2 * q);

                        float b0 = 1 + alpha * gain;
                        float b1 = -2 * Mathf.Cos(w0);
                        float b2 = 1 - alpha * gain;
                        float a0 = 1 + alpha / gain;
                        float a1 = -2 * Mathf.Cos(w0);
                        float a2 = 1 - alpha / gain;

                        float x1 = 0;
                        float x2 = 0;
                        float y1 = 0;
                        float y2 = 0;

                        for (int j = 0; j < samples.Length; j++)
                        {
                            float x0 = samples[j];
                            float y0 = (b0 / a0) * x0 + (b1 / a0) * x1 + (b2 / a0) * x2 - (a1 / a0) * y1 - (a2 / a0) * y2;

                            x2 = x1;
                            x1 = x0;
                            y2 = y1;
                            y1 = y0;


                            samples[j] = y0;
                        }
                    }

                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] *= Mathf.Pow(10, volume / 20); //Decibel Conversion
                    }

                    // Apply the pitch shift to the sample data
                    float[] pitchedSamples = new float[Mathf.CeilToInt(samples.Length * Mathf.Pow(2, -pitch / 12))];
                    for (int i = 0; i < pitchedSamples.Length; i++)
                    {
                        float oldIndex = (float)i / Mathf.Pow(2, -pitch / 12); //Semitone Conversion
                        int index = Mathf.FloorToInt(oldIndex);
                        float t = oldIndex - index;

                        if (index >= samples.Length - 1)
                        {
                            pitchedSamples[i] = samples[samples.Length - 1];
                        }
                        else
                        {
                            pitchedSamples[i] = Mathf.Lerp(samples[index], samples[index + 1], t);
                        }
                    }


                    AudioClip ac = AudioClip.Create("Temp", pitchedSamples.Length, clipToEqualize.channels, clipToEqualize.frequency, false);
                    ac.SetData(pitchedSamples, 0);
                    WaveUtils.Save(equalizedClipFileName, ac, _directoryPath, false);
                    AssetDatabase.Refresh();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndDisabledGroup();
                GUILayout.Space(10);

            }
            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();



        }

        private void SaveInvoice()
        {
            PlayerPrefs.SetString("VoiceGPT_Invoice", invoice);
        }

        public Texture2D PaintWaveformSpectrum(AudioClip audio, int width, int height, Color col, bool slider, float sliderValue, bool cutRevered = false)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float[] samples = new float[audio.samples];
            float[] waveform = new float[width];
            audio.GetData(samples, 0);
            int packSize = (audio.samples / width) + 1;
            int s = 0;
            for (int i = 0; i < audio.samples; i += packSize)
            {
                waveform[s] = Mathf.Abs(samples[i]);
                s++;
            }


            for (int i = 1; i < waveform.Length; i++)
            {
                var start = (i - 2 > 0 ? i - 2 : 0);
                var end = (i + 2 < waveform.Length ? i + 2 : waveform.Length);

                float sum = 0;

                for (int j = start; j < end; j++)
                {
                    sum += waveform[j];
                }

                var avg = sum / (end - start);
                waveform[i] = avg;

            }


            //Transparent BG
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
            if (!slider)
            {
                for (int x = 0; x < waveform.Length; x = x + 2)
                {
                    for (int y = 0; y <= waveform[x] * height; y++)
                    {
                        tex.SetPixel(x, (height / 2) + y, col);
                        tex.SetPixel(x, (height / 2) - y, col);
                    }
                }
            }
            else
            {
                for (int x = 0; x < waveform.Length; x = x + 2)
                {
                    for (int y = 0; y <= waveform[x] * height; y++)
                    {
                        if (cutRevered)
                        {
                            if (x > waveform.Length * sliderValue)
                            {
                                tex.SetPixel(x, (height / 2) + y, col);
                                tex.SetPixel(x, (height / 2) - y, col);
                            }
                        }
                        else
                        {
                            if (x < waveform.Length * sliderValue)
                            {
                                tex.SetPixel(x, (height / 2) + y, col);
                                tex.SetPixel(x, (height / 2) - y, col);
                            }
                        }
                    }
                }
            }
            tex.Apply();

            return tex;
        }

        public static void PlayClip(AudioClip clip, int startSample = 0, bool loop = false)
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;

            Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            MethodInfo method = audioUtilClass.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null
            );

            method.Invoke(
                null,
                new object[] { clip, startSample, loop }
            );
        }

        public static void StopAllClips()
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;

            Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            MethodInfo method = audioUtilClass.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { },
                null
            );

            method.Invoke(
                null,
                new object[] { }
            );
        }


        IEnumerator Post(string url, string bodyJsonString)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            postProgress = 1;
            postFlag = false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 500)
                    Debug.Log("There was an error in generating the voice. Please check your invoice/order number and try again or check the documentation / FAQs for more information. If you are certain that the invoice number is correct and you have gone through the FAQs, please check if an update is available to the asset (Window > Package Manager > (Search) VoiceGPT > Update Asset).");

                if (request.responseCode == 400)
                    Debug.Log("Likely error in text field: Please check your prompt for quotes (\"\") and line breaks at the end of the prompt. There could also be special formatting in your text. Please remove any special formatting by pasting as plain text in a notepad and then pasting the text here. Inclusion of any special formatting or illegal characters will result in an error such as this. For best results, please use a combination of letters, periods and commas and make sure there are no line breaks in between or at the end. If you must use quotes or line breaks, please prepend them with a backslash. Please do not press enter in the text field before clicking on generate. Please refer to the FAQs for more detailed information.");

                if (request.responseCode == 413)
                    Debug.Log("Voice file size is too big. Please use a voice clip that is 20MB or less. Only 6 seconds of .wav file is required.");

            }
            else
            {
                if (request.responseCode == 400)
                {
                    Debug.Log("Error in text field: Please check your prompt for quotes (\"\") and line breaks at the end of the prompt. There could also be special formatting in your text. Please remove any special formatting by pasting as plain text in a notepad and then pasting the text here. Inclusion of any special formatting or illegal characters will result in an error such as this. For best results, please use a combination of letters, periods and commas and make sure there are no line breaks in between or at the end. If you must use quotes or line breaks, please prepend them with a backslash. Please do not press enter in the text field before clicking on generate. Please refer to the FAQs for more detailed information.");
                }
                if (request.downloadHandler.text == "Invalid Response")
                    Debug.Log("Invalid Invoice/Order Number. Please check your invoice number and try again. You may be using your order number instead of your invoice number. Please look into the documentation on how you can get your invoice number. An invoice number is exactly 14 characters long. If you have, at any point in time, received a successful verification for your invoice number, then this error is not due to invalidity of invoice. Please contact us at info@aikodex.com so we can assist you.");
                else if (request.downloadHandler.text == "Limit Reached")
                    Debug.Log("It seems that you may have reached the limit. To check your character usage, please click on the Status button. Please wait until 30th/31st of the month to get a renewed character count. Thank you for using VoiceGPT.");
                else
                {
                    byte[] soundBytes = System.Convert.FromBase64String(request.downloadHandler.text);
                    File.WriteAllBytes($"{_directoryPath}/{fileName}.wav", soundBytes);
                    AssetDatabase.Refresh();
                    Selection.activeObject = (AudioClip)AssetDatabase.LoadMainAssetAtPath($"{_directoryPath}/{fileName}.wav");
                    Debug.Log($"<color=green>Inference Successful: </color>Please find the audio clip named {fileName} in {_directoryPath}");
                    take = (int.Parse(take) + 1).ToString();
                }
            }

            request.Dispose();
        }
        IEnumerator Status(string url, string bodyJsonString)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            postProgress = 1;
            postFlag = false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 429)
                {
                    Debug.Log("This error typically occurs if you try to verify your invoice more than once in a given period of time. If you have, at any point in time ever, received a confirmation of the verification of the invoice number, you need not click on this button again. You can keep the number pasted in the field, enter text into the given field that you'd like to convert to voice and click on the generate voice button.");
                }
            }
            else
            {
                if (request.downloadHandler.text == "Invalid Invoice Number")
                    Debug.Log("You do not have any generations or your invoice/order number is invalid. Please click on verify to verify your purchase. Once confirmed, you need not verify your invoice. For more information, please check the documentation or the FAQs.");
                else
                    Debug.Log("You have used " + request.downloadHandler.text + " characters.");
            }

            request.Dispose();
        }
        IEnumerator Verify(string url, string bodyJsonString)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            postProgress = 1;
            postFlag = false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 429)
                {
                    Debug.Log("This error typically occurs if you try to verify your invoice more than once in a given period of time. If you have, at any point in time ever, received a confirmation of the verification of the invoice number, you need not click on this button again. You can keep the number pasted in the field, enter text into the given field that you'd like to convert to voice and click on the generate voice button.");
                }
                else
                {
                    Debug.Log("Invoice/Order number verification unsuccessful.");
                }
            }
            else
            {
                if (request.downloadHandler.text == "Not Verified")
                    Debug.Log("Invoice/Order number verification unsuccessful. Please check your invoice/order number and try again or contact the publisher on the email given in the documentation.");
                else
                {
                    if (invoice.Length == 13)
                        Debug.Log("Your order number is verified, however, please use your invoice number to access the voice generator. Your invoice number typically gets generated in 2-4 hours after your purchase. It will be listed beside your Order number. An invoice number looks like IN010X0XXXXXXX, a 14 digit number you can find on the \"My Orders\" page on the asset store page.");
                    else
                        Debug.Log("Your invoice is verified. Thank you for choosing VoiceGPT! You can proceed to use the voice generator now. If you wish, you can save the invoice number so you do not need to re-enter it. You do not need to verify this invoice again.");
                }
            }
            request.Dispose();
        }
    }
}
