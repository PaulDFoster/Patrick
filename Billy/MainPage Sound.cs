using Microsoft.Cognitive.LUIS;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Media.Devices;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Billy
{
    public sealed partial class MainPage : Page
    {
        private StringBuilder dictatedTextBuilder = new StringBuilder();

        // defaults before device language scanning occurs
        public static Language host_language = new Language("en-US");
        public static string voiceMatchLanguageCode = "en";
        private string inLanguageSpecificCode = "en";
        private string outLanguageSpecificCode = "en";

        // Call sign under RPI3 IOT Core preview needs to be uncapitalised. Not normal.
        private string callSign = "patrick";

        private SpeechRecognizer speechRecognizer;
        private SpeechSynthesizer synthesizer;
        private AudioDeviceController microphone;
        Windows.Media.Capture.MediaCapture captureDev;

        private static uint HResultPrivacyStatementDeclined = 0x80045509;

        private string toSpeak;
        private bool isListening;

        public bool IsReadyToRecognize => speechRecognizer != null;

        public async void InitRecogAndSyn()
        {
            InitializeRecognizer(host_language);
            await InitializeSynthesizer();
            //await StartRecognition();
        }

        private async void InitializeRecognizer(Language recognizerLanguage, Language speechLanguage = null)
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
                ShowDeadEyes();
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
                ShowDeadEyes();
            }

            // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
            // some recognized phrases occur, or the garbage rule is hit. HypothesisGenerated fires during recognition, and
            // allows us to provide incremental feedback based on what the user's currently saying.
            speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
            speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            speechRecognizer.HypothesisGenerated += SpeechRecognizer_HypothesisGenerated;
        }

        private void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("Recognition Result Generated");
        }

        private void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("Recognition Session Complete");
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
                ShowDeadEyes();
            }
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
                if (speechRecognizer != null)
                {
                    recognitionOperation = speechRecognizer.RecognizeAsync();
                    speechRecognitionResult = await recognitionOperation;

                    if (speechRecognitionResult.Status == SpeechRecognitionResultStatus.Success)
                    {
                        // BUG: Sometimes hits success repetitively and does not listen for input
                        Debug.WriteLine(speechRecognitionResult.Text);
                        if (speechRecognitionResult.Text.Length > 0)
                            ProcessSpeech(speechRecognitionResult.Text);
                    }

                }
                else
                    await System.Threading.Tasks.Task.Delay(250);

                Blink.Begin();



            }
        }

        private async void ProcessSpeech(string s)
        {
            //
            // BUG: If command is finished with Patrick the following code removes all preable and then patrick, resulting in an empty string to try and say
            //
            //
            try
            {
                s = s.Substring(s.IndexOf(callSign)); // commands must start with Patrick. Removes chatter before commands
            }
            catch (Exception e)
            {
                // no patrick found
                Debug.WriteLine("No Patrick found");
            }

            if (s.Contains(callSign))
            {
                string q = s.Replace(callSign, ""); //Removed the space at the end of Patrick
                if (q.Length == 0) return; //Quick fix for BUG

                // Set up our custom context for the intent handlers
                App.SpeechFunc speakresponse = this.Speak;

                // Generate the base API url for the LUIS application

                IntentHandlers ih = new IntentHandlers();

                // Set up an intent router using the IntentHandlers class to process intents
                //using (var router = IntentRouter.Setup<IntentHandlers>(appid, appkey))
                using (var router = IntentRouter.Setup(appid, appkey, ih))

                {
                    try
                    {
                        var handled = await router.Route(q, context, speakresponse);
                        if (!handled) speakresponse("I'm sorry. I am not able to do that at this time.");
                    }
                    catch (Exception ex)
                    {

                    }
                }

                return;
            }

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
