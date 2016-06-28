using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.Devices.AllJoyn;
using com.microsoft.ZWaveBridge.SwitchBinary.Switch;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;
//using builtin.intent;
using Newtonsoft.Json;
using System.Reflection;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Billy
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //SwitchConsumer switchConsumer;
        //bool lightStatus = false;
        //bool lightsAvailable = false;

        Context context = new Context();

        // Speech events may come in on a thread other than the UI thread, keep track of the UI thread's
        // dispatcher, so we can update the UI in a thread-safe manner.
        private CoreDispatcher dispatcher;

        private bool isListening = false;
        private StringBuilder dictatedTextBuilder = new StringBuilder();

        // defaults before device language scanning occurs
        public static Language host_language = new Language("en-US");
        public static string voiceMatchLanguageCode = "en";
        private string inLanguageSpecificCode = "en";
        private string outLanguageSpecificCode = "en";

        private SpeechRecognizer speechRecognizer;
        private SpeechSynthesizer synthesizer;
        private AudioDeviceController microphone;
        Windows.Media.Capture.MediaCapture captureDev;

        private static uint HResultPrivacyStatementDeclined = 0x80045509;

        private string toSpeak;

        public bool IsReadyToRecognize => speechRecognizer != null;

        // Intent function factory cache
        // static storage
        private static Dictionary<string, Func<IIntent>> InstanceCreateCache = new Dictionary<string, Func<IIntent>>();


        public MainPage()
        {


            // Test for UWP on IOT Core or Windows PC
            var api = "Windows.Devices.I2c.I2cDevice";
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent(api))
                host_language = new Language("en-US"); // US for Pi, GB for Laptop;
            else
                host_language = new Language("en-GB");


            this.InitializeComponent();

            AllJoynBusAttachment switchBusAttachment = new AllJoynBusAttachment();
            SwitchWatcher switchWatcher = new SwitchWatcher(switchBusAttachment);
            switchWatcher.Added += SwitchWatcher_Added;
            switchWatcher.Start();

            SetupMicrophone();

            InitRecogAndSyn();

            ListenForPatrick();

        }

        private async void SetupMicrophone()
        {
            // Set default microphone here, so we can get the usb mic not the webcam mic
            // Enables camera operations with PO

            captureDev = new Windows.Media.Capture.MediaCapture();
            await captureDev.InitializeAsync();
            microphone = captureDev.AudioDeviceController;
        }

        private async void ListenForPatrick()
        {
            IAsyncOperation<SpeechRecognitionResult> recognitionOperation;
            SpeechRecognitionResult speechRecognitionResult;

            while (true)
            {
                recognitionOperation = speechRecognizer.RecognizeAsync();
                speechRecognitionResult = await recognitionOperation;

                if (speechRecognitionResult.Status == SpeechRecognitionResultStatus.Success)
                {
                    // BUG: Sometimes hits success repetitively and does not listen for input
                    Debug.WriteLine(speechRecognitionResult.Text);
                    if(speechRecognitionResult.Text.Length>0)
                        Speak(await ProcessSpeech(speechRecognitionResult.Text));
                }

            }
        }

        private async Task<string> ProcessSpeech(string s)
        {
            //
            // BUG: If command is finished with Patrick the following code removes all preable and then patrick, resulting in an empty string to try and say
            //
            //
            try
            {
                s = s.Substring(s.IndexOf("patrick")); // commands must start with Patrick. Removes chatter before commands
            }
            catch (Exception e)
            {
                // no patrick found
                Debug.WriteLine("No Patrick found");
            }

            if (s.Contains("patrick"))
            {
                // Call LUIS with value of s here

                // Take action based on LUIS intent returned

                //  Look up serice locally or set this up remotely and just return the action
                HttpClient client = new HttpClient();

                string q = s.Replace("patrick", ""); //Removed the space at the end of Patrick


                if (q.Length == 0) return ""; //Quick fix for BUG


                string urie = WebUtility.UrlEncode(q);

                string uri = "https://api.projectoxford.ai/luis/v1/application?id=c996e5b8-a6d7-4d4e-ab78-2de73a2b09cc&subscription-key=4bd17b8ac139489996440aee38e5c34a&q=" + urie;

                HttpResponseMessage ressp = await client.GetAsync(new Uri(uri, UriKind.Absolute)); 

                string res = await ressp.Content.ReadAsStringAsync();
                Debug.WriteLine(res);

                // Decode JSON
                // query; intents; entities
                LUCIS m = JsonConvert.DeserializeObject<LUCIS>(res);
                //IIntent intent = CreateCachableIIntent(m.intents[0].intent);
                //intent.entities = m.entities;
                try
                {
                    System.Type objType = System.Type.GetType("Billy." + m.intents[0].intent);
                    dynamic intent = System.Activator.CreateInstance(objType);
                    if (m.entities.Count > 0)
                        intent.entities = m.entities;
                    else
                        intent.entities = null;
                    intent.query = m.query;
                    intent.context = context;

                    return await intent.Execute();
                }
                catch
                {
                    // Generic catch for missing intent or intent failure
                    // TODO:
                    return "I'm sorry. I am not able to do that at this time.";
                }
            }
            else
            {
                Debug.WriteLine("No Patrick command found");
                return "";
            }
        }

        // searches for the class, initiates it (calls factory method) and returns the instance
        // TODO: add a lot of error handling!
        IIntent CreateCachableIIntent(string className)
        {
            if (!InstanceCreateCache.ContainsKey(className))
            {
                try
                {
                    // get the type (several ways exist, this is an eays one)
                    Type type = Type.GetType(className);

                    // NOTE: this can be tempting, but do NOT use the following, because you cannot 
                    // create a delegate from a ctor and will loose many performance benefits
                    //ConstructorInfo constructorInfo = type.GetConstructor(Type.EmptyTypes);

                    // works with public instance/static methods
                    MethodInfo mi = type.GetMethod("Create");
                    // the "magic", turn it into a delegate
                    var createInstanceDelegate = (Func<IIntent>)mi.CreateDelegate(typeof(Func<IIntent>),mi);
                    // store for future reference
                    InstanceCreateCache.Add(className, createInstanceDelegate);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }

            return InstanceCreateCache[className].Invoke();

        }

        private async void SwitchWatcher_Added(SwitchWatcher sender, AllJoynServiceInfo args)
        {

            SwitchJoinSessionResult joinSessionResult = await SwitchConsumer.JoinSessionAsync(args, sender);
            if (joinSessionResult.Status == AllJoynStatus.Ok)
            {
                context.switchConsumer = joinSessionResult.Consumer;
                context.lightsAvailable = true;
                
                SwitchGetValueResult x = await context.switchConsumer.GetValueAsync();
                context.lightStatus = (bool) x.Value;

                Speak("Things network online");
            }
        }

        public async void InitRecogAndSyn()
        {
            await InitializeRecognizer(host_language);
            await InitializeSynthesizer();
            //await StartRecognition();
        }

        private async Task InitializeRecognizer(Language recognizerLanguage, Language speechLanguage = null)
        {
            //Default spoken language to first non-recognizer language
            speechLanguage = speechLanguage ??
                             SpeechRecognizer.SupportedGrammarLanguages.FirstOrDefault(
                                 l => l.LanguageTag == recognizerLanguage.LanguageTag);

            if (speechLanguage == null)
            {
                checkError.Visibility = Visibility.Visible;
                errorCheck.Visibility = Visibility.Visible;
                errorCheck.Text = "No alternate languages installed";
                return;
            }

            //Set recognition and spoken languages based on choice and alternates
            voiceMatchLanguageCode = Abbreviated(speechLanguage.LanguageTag);
            inLanguageSpecificCode = recognizerLanguage.LanguageTag;
            outLanguageSpecificCode = speechLanguage.LanguageTag;

            if (speechRecognizer != null)
            {
                //speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                //speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;
                speechRecognizer.Dispose();
                speechRecognizer = null;
            }

            speechRecognizer = new SpeechRecognizer(recognizerLanguage);

            SpeechRecognitionCompilationResult result = await speechRecognizer.CompileConstraintsAsync();
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                checkError.Visibility = Visibility.Visible;
                errorCheck.Visibility = Visibility.Visible;
                errorCheck.Text = "Recognition Failed!";
            }

            // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
            // some recognized phrases occur, or the garbage rule is hit. HypothesisGenerated fires during recognition, and
            // allows us to provide incremental feedback based on what the user's currently saying.
            //speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
            //speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            speechRecognizer.HypothesisGenerated += SpeechRecognizer_HypothesisGenerated;
        }
        public async Task InitializeSynthesizer()
        {
            if (synthesizer == null)
            {
                synthesizer = new SpeechSynthesizer();
            }

            isListening = false;
            dispatcher = this.Dispatcher;

            // select the language display
            var voices = SpeechSynthesizer.AllVoices;
            foreach (VoiceInformation voice in voices)
            {
                if (voice.Language.Contains(voiceMatchLanguageCode))
                {
                    if (voice.Gender == VoiceGender.Male)
                    {
                        synthesizer.Voice = voice;
                        break;
                    }
                }
            }

            // Check Microphone Plugged in
            bool permissionGained = await AudioCapturePermissions.RequestMicrophoneCapture();
            if (!permissionGained)
            {
                this.dictationTextBox.Text = "Requesting Microphone Capture Fails; Make sure Microphone is plugged in";
            }
        }

        private async void btnStartTalk_Click(object sender, RoutedEventArgs e)
        {
            //if (!isListening)
            //{
            //    await StartRecognition();
            //}
            //else
            //{
            //    await StopRecognition();
            //}
        }

        private void btnClearText_Click(object sender, RoutedEventArgs e)
        {
            btnClearText.IsEnabled = false;
            dictatedTextBuilder.Clear();
            dictationTextBox.Text = "";
        }

        private string Abbreviated(string languageTag)
        {
            var index = languageTag.IndexOf('-');
            return index > -1 ? languageTag.Substring(0, index) : languageTag;
        }

        /// <summary>
        /// Handle events fired when a result is generated. Check for high to medium confidence, and then append the
        /// string to the end of the stringbuffer, and replace the content of the textbox with the string buffer, to
        /// remove any hypothesis text that may be present.
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>

        /// <summary>
        /// While the user is speaking, update the textbox with the partial sentence of what's being said for user feedback.
        /// </summary>
        /// <param name="sender">The recognizer that has generated the hypothesis</param>
        /// <param name="args">The hypothesis formed</param>
        private async void SpeechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            string hypothesis = args.Hypothesis.Text;

            // Update the textbox with the currently confirmed text, and the hypothesis combined.
            string textboxContent = dictatedTextBuilder.ToString() + " " + hypothesis + " ...";

            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                dictationTextBox.Text = textboxContent;
                btnClearText.IsEnabled = true;
            });
        }

        private async void Speak(string s)
        {
            SpeechSynthesisStream stream = await synthesizer.SynthesizeTextToStreamAsync(s);
            var ignored2 = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {

                MuteMicrophone(true);
                media.SetSource(stream, stream.ContentType);
                media.Play();
                // Unmute microphone in Media_Ended
                
            });
        }

        private void dictationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(dictationTextBox, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer))
                {
                    continue;
                }

                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }
        
        private void MuteMicrophone(bool status)
        {
            // Mute Microphone
            microphone.Muted = status;
        }

        private void media_MediaEnded(object sender, RoutedEventArgs e)
        {
            MuteMicrophone(false);
        }

        private void media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // An empty media fails ie no patrick found causes empty media.
            MuteMicrophone(false);
        }
    }
}
