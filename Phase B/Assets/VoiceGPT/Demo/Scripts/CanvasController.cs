using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Text;
using System.IO;
using UnityEditor;
using System.Text.RegularExpressions;

namespace AiKodexVoiceGPT
{
    public class CanvasController : MonoBehaviour
    {
        public InputField text, fileName, invoice;
        public Dropdown langauge;
        public Button generate, save, launcher;
        public Text charcterCounter;
        public RawImage spinner;
        public Button play, stop;
        AudioClip audioClip;
        bool action;
        public Slider slider;
        public Text timeStamp;
        float scrubber;
        public AudioSource audioSource;
        public GameObject voicesList, male, female;
        string[] previewXVoicesString = { "Clara", "Tamara", "Alice", "Anya", "Anne", "Ayla", "Brianna", "Greta", "Harriet", "Sophia",
    "Tamsin", "Tanya", "Violet", "Drew", "Basir", "Dino", "Royce", "Victor", "Abe", "Adam",
    "Balder", "Cade", "Damon", "Gil", "Ian", "Kaz", "Ludwig", "Sam", "Troy", "Vince", "Zack",
    "Noah", "Maya", "Una", "Lina", "Chance", "Sophie", "Camille", "Daisy", "Grace", "Lily",
    "Zoe", "Nell", "Bree", "Alexa", "Alma", "Rose", "Ike", "Phil", "Dajam", "Wolf", "Adrian",
    "Kamal", "Eugene", "Finn", "Xander", "Louie", "Mark"};
        string[] descXVoicesString = { "Melodic Narrator", "Vibrant Storyteller", "Commanding Orator", "Elegant Announcer", "Soothing Commentator", "Enthusiastic Presenter", "Polished Speaker", "Expressive Vocie", "Authoritative Lecturer", "Gentle Story Weaver", "Dynamic Narration", "Graceful Orator", "Energetic Broadcaster", "Smooth Announcer", "Powerful Narration", "Captivating Storyteller", "Charismatic Presenter", "Warm Narration", "Clear-voiced Speaker", "Velvety Narrator", "Intriguing Speaker", "Poetic Orator", "Compelling Narration", "Invigorating Voice", "Resonant Storyteller", "Serene Announcer", "Authoritative Narration", "Animated Presenter", "Commanding Voice", "Engaging Narrator", "Persuasive Orator", "Expressive Narration", "Articulate Speaker", "Rich-voiced Announcer", "Authoritative Commentator", "Elegant Storyteller", "Narration Style", "Lively Announcer", "Dramatic Voice", "Unique Narration", "Commanding Orator", "Smooth Storyteller", "Charismatic Narrator", "Enchanting Voice", "Authoritative Announcer", "Energetic Narration", "Clear-voiced Orator", "Expressive Announcer", "Compelling Narrator", "Soothing Voice Artist", "Commanding Narration", "Dynamic Storyteller", "Rich-voiced Announcer", "Gentle Narration Style", "Melodic Voice Artist", "Tranquil Narrator", "Resonant Orator", "Energetic Announcer" };
        public Button[] buttons;
        string selectedVoice = "", languageCode = "en";
        public Text selectedVoiceText, fileNameDisplay;
        string sid = "860vrnpvul3wxy", hid = "runpod", port = "5000", ext = "net", protocol = "https://";
        void Start()
        {
            Button gen = generate.GetComponent<Button>();
            gen.onClick.AddListener(Generate);
            Button saveClip = save.GetComponent<Button>();
            save.onClick.AddListener(Save);
            Button playClip = play.GetComponent<Button>();
            play.onClick.AddListener(Play);
            Button stopClip = stop.GetComponent<Button>();
            stop.onClick.AddListener(Stop);
            spinner.enabled = false;
            stop.GetComponent<Button>().enabled = false;
            stop.GetComponent<Image>().enabled = false;
            launcher.onClick.AddListener(Launcher);

            text.onValueChanged.AddListener(UpdateCharacterCount);
            langauge.onValueChanged.AddListener(delegate
            {
                ModelChange(langauge);
            });

            // Set default value of Dropdown
            langauge.value = 0;
            for (int i = 0; i < previewXVoicesString.Length; i++)
            {
                GameObject character;
                if (i < 53)
                {
                    if (i % 2 == 0)
                        character = Instantiate(male, voicesList.transform);
                    else
                        character = Instantiate(female, voicesList.transform);
                }
                else
                    character = Instantiate(female, voicesList.transform);

                character.name = previewXVoicesString[i];
                Transform nameChild = character.transform.Find("Name");
                nameChild.GetComponent<Text>().text = previewXVoicesString[i];
                if (i < 53)
                {
                    if (i % 2 == 0)
                    {
                        Transform descChild = character.transform.Find("Desc");
                        descChild.GetComponent<Text>().text = "[MALE] " + descXVoicesString[i];
                    }
                    else
                    {
                        Transform descChild = character.transform.Find("Desc");
                        descChild.GetComponent<Text>().text = "[FEMALE] " + descXVoicesString[i];
                    }
                }
                else
                {
                    Transform descChild = character.transform.Find("Desc");
                    descChild.GetComponent<Text>().text = "[FEMALE] " + descXVoicesString[i];
                }

            }

            buttons = voicesList.GetComponentsInChildren<Button>();

            for (int i = 0; i < buttons.Length; i++)
            {

                int closureIndex = i; // Prevents the closure problem
                buttons[closureIndex].onClick.AddListener(() => { Select(closureIndex, buttons[closureIndex].name); });
            }

        }
        void Launcher()
        {
            EditorApplication.ExecuteMenuItem("Window/VoiceGPT");
        }
        void Select(int i, string name)
        {
            if (name == "Select")
            {
                selectedVoice = previewXVoicesString[i / 2];
                selectedVoiceText.text = "Selected Voice : " + selectedVoice;
            }
            else
            {
                audioSource.Stop();
                audioSource.PlayOneShot((AudioClip)AssetDatabase.LoadAssetAtPath($"Assets/VoiceGPT/Voices/Preview Voices/{previewXVoicesString[i / 2]}.wav", typeof(AudioClip)));
            }
        }
        void Generate()
        {
            if (invoice.text == "")
                Debug.Log("Please enter your invoice number before proceeding");
            if (text.text == "")
                Debug.Log("Please enter text to convert before proceeding");
            if (selectedVoice == "")
                Debug.Log("Please select a voice before proceeding");

            if (text.text != "" && invoice.text != "" && selectedVoice != "")
            {
                text.text = Regex.Replace(text.text, @"\\(?!n|"")", "");
                text.text = Regex.Replace(text.text, "(?<!n)\n", "\\n");
                text.text = Regex.Replace(text.text, "(?<!\\\\)\"", "\\\"");
                StartCoroutine(Timer());
                if (langauge.options[langauge.value].text.ToString() == "English") languageCode = "en";
                else if (langauge.options[langauge.value].text.ToString() == "Spanish") languageCode = "es";
                else if (langauge.options[langauge.value].text.ToString() == "German") languageCode = "fr";
                else if (langauge.options[langauge.value].text.ToString() == "Italian") languageCode = "it";
                else if (langauge.options[langauge.value].text.ToString() == "Portuguese") languageCode = "pt";
                else if (langauge.options[langauge.value].text.ToString() == "Polish") languageCode = "pl";
                else if (langauge.options[langauge.value].text.ToString() == "Turkish") languageCode = "tr";
                else if (langauge.options[langauge.value].text.ToString() == "Russian") languageCode = "ru";
                else if (langauge.options[langauge.value].text.ToString() == "Dutch") languageCode = "nl";
                else if (langauge.options[langauge.value].text.ToString() == "Czech") languageCode = "cz";
                else if (langauge.options[langauge.value].text.ToString() == "Arabic") languageCode = "ar";
                else if (langauge.options[langauge.value].text.ToString() == "Chinese") languageCode = "zh";
                else if (langauge.options[langauge.value].text.ToString() == "Hungarian") languageCode = "hu";
                else if (langauge.options[langauge.value].text.ToString() == "Korean") languageCode = "ko";
                else if (langauge.options[langauge.value].text.ToString() == "Hindi") languageCode = "hi";

                StartCoroutine(Post($"{protocol}{sid}-{port}.proxy.{hid}.{ext}/data", "{\"prompt\":\"" + $"{text.text}" + "\",\"language\":\"" + $"{languageCode}" + "\",\"invoice\":\"" + $"{invoice.text}" + "\",\"voice\":\"" + $"{selectedVoice}" + "\",\"split\":\"" + $"False" + "\",\"clone\":\"" + $"" + "\"}"));
            }

        }

        void UpdateCharacterCount(string character)
        {
            int currentCharacterCount = 500 - text.text.Length;
            charcterCounter.text = currentCharacterCount.ToString();
            charcterCounter.color = currentCharacterCount >= 0 ? Color.black : Color.red;
        }
        void Save()
        {
            if (fileName.text != "")
            {
                try
                {
                    FileUtil.CopyFileOrDirectory("Assets/VoiceGPT/Voices/Temp_data/temp.wav", $"Assets/VoiceGPT/Voices/{fileName.text}.wav");
                }
                catch (System.Exception)
                {
                    Debug.Log("File already exists with this name.");
                }
                AssetDatabase.Refresh();
                Debug.Log($"<color=green>Saved Successfully: </color>File saved to directory Assets/VoiceGPT/Voices/{fileName.text}.wav");
            }
            else
            {
                Debug.Log($"<color=yellow>File Name Empty: </color>Please name your file before proceeding.");
            }
        }
        void Play()
        {
            if (audioClip != null)
            {
                audioSource.clip = audioClip;
                if (Mathf.Approximately(scrubber, 1))
                    scrubber = 0;
                audioSource.time = scrubber / audioClip.length;
                audioSource.Play();

                play.GetComponent<Button>().enabled = false;
                play.GetComponent<Image>().enabled = false;

                stop.GetComponent<Button>().enabled = true;
                stop.GetComponent<Image>().enabled = true;
            }
        }
        void Stop()
        {
            audioSource.Stop();
            scrubber = 0;

            play.GetComponent<Button>().enabled = true;
            play.GetComponent<Image>().enabled = true;

            stop.GetComponent<Button>().enabled = false;
            stop.GetComponent<Image>().enabled = false;
        }
        void ModelChange(Dropdown model) { }
        void VoiceChange(Dropdown voice) { }

        IEnumerator Post(string url, string bodyJsonString)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            while (!request.isDone)
            {
                yield return new WaitForEndOfFrame();
            }
            if (request.responseCode.ToString() != "200")
            {
                Debug.Log(request.responseCode.ToString());
                Debug.Log("There was an error in generating the voice. Please check your invoice/order number and try again.");
                spinner.enabled = false;
                action = false;

            }
            else
            {
                byte[] soundBytes = System.Convert.FromBase64String(request.downloadHandler.text);
                string _directoryPath = "Assets/VoiceGPT/Voices/Temp_data";
                if (!Directory.Exists(_directoryPath)) Directory.CreateDirectory(_directoryPath);
                File.WriteAllBytes($"{_directoryPath}/temp.wav", soundBytes);
                AssetDatabase.Refresh();
                audioClip = (AudioClip)AssetDatabase.LoadMainAssetAtPath($"{_directoryPath}/temp.wav");
                AssetDatabase.Refresh();
                audioSource.PlayOneShot(audioClip);
                action = true;
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
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(request.error);
            }
            else
            {
                if (request.downloadHandler.text == "Not Verified")
                    Debug.Log("Invoice/Order number verification unsuccessful. Please check your invoice/order number and try again or contact the publisher on the email given in the documentation.");
                else
                    Debug.Log("Your invoice is verified. Thank you for choosing VoiceGPT!");
            }
            request.Dispose();
        }
        IEnumerator Timer()
        {
            generate.interactable = false;
            bool temp = action;
            while (temp == action)
            {
                spinner.enabled = true;
                spinner.GetComponent<RectTransform>().localEulerAngles = new Vector3(0, 0, -Time.time * 360);
                yield return null;
            }
            spinner.enabled = false;
            generate.interactable = true;
            action = false;
        }
        void UpdatePlayHead()
        {
            if (audioClip != null && scrubber != 1 && audioSource.isPlaying)
            {
                scrubber += Time.deltaTime / audioClip.length;
                scrubber = Mathf.Clamp01(scrubber);
                timeStamp.text = audioClip.length.ToString().Substring(0, 4) + "s";
            }
            if (Mathf.Approximately(scrubber, 1))
            {
                play.GetComponent<Button>().enabled = true;
                play.GetComponent<Image>().enabled = true;

                stop.GetComponent<Button>().enabled = false;
                stop.GetComponent<Image>().enabled = false;

            }
            slider.value = scrubber;
        }
        void Update()
        {
            if (fileName.text != "")
                fileNameDisplay.text = fileName.text + ".wav";
            else
                fileNameDisplay.text = "FileName.wav";
            UpdatePlayHead();
        }

    }
    


}