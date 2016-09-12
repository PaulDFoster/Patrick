using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.AllJoyn;
using com.microsoft.ZWaveBridge.SwitchBinary.Switch;
using System.Reflection;
using Microsoft.Cognitive.LUIS;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Billy
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        Context context = new Context();

        // Speech events may come in on a thread other than the UI thread, keep track of the UI thread's
        // dispatcher, so we can update the UI in a thread-safe manner.
        private CoreDispatcher dispatcher;

        // Intent function factory cache
        // static storage
        //private static Dictionary<string, Func<IIntent>> InstanceCreateCache = new Dictionary<string, Func<IIntent>>();

        public MainPage()
        {

            context.IsIoTCore = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.IoT";
            // Test for UWP on IOT Core or Windows PC

            if (context.IsIoTCore == true)
            {
                host_language = new Language("en-US"); // US for Pi, GB for Laptop;
                tft = new AdaFruitTFT();
            }
            else
                host_language = new Language("en-US"); // should be en-GB


            this.InitializeComponent();

            AllJoynBusAttachment switchBusAttachment = new AllJoynBusAttachment();
            SwitchWatcher switchWatcher = new SwitchWatcher(switchBusAttachment);
            switchWatcher.Added += SwitchWatcher_Added;
            switchWatcher.Start();

            System.Diagnostics.Debug.WriteLine("1");
            SetupMicrophone();
            System.Diagnostics.Debug.WriteLine("2");
            InitRecogAndSyn();
            System.Diagnostics.Debug.WriteLine("3");
            ListenForPatrick();
            System.Diagnostics.Debug.WriteLine("4");
            //InitializeCameraAsync(); // Need to work out how to get the video stream seperate from the video so can control individually for speech and face.
            System.Diagnostics.Debug.WriteLine("5");
        }

        // searches for the class, initiates it (calls factory method) and returns the instance
        // TODO: add a lot of error handling!
        //IIntent CreateCachableIIntent(string className)
        //{
        //    if (!InstanceCreateCache.ContainsKey(className))
        //    {
        //        try
        //        {
        //            // get the type (several ways exist, this is an eays one)
        //            Type type = Type.GetType(className);

        //            // NOTE: this can be tempting, but do NOT use the following, because you cannot 
        //            // create a delegate from a ctor and will loose many performance benefits
        //            //ConstructorInfo constructorInfo = type.GetConstructor(Type.EmptyTypes);

        //            // works with public instance/static methods
        //            MethodInfo mi = type.GetMethod("Create");
        //            // the "magic", turn it into a delegate
        //            var createInstanceDelegate = (Func<IIntent>)mi.CreateDelegate(typeof(Func<IIntent>),mi);
        //            // store for future reference
        //            InstanceCreateCache.Add(className, createInstanceDelegate);
        //        }
        //        catch (Exception e)
        //        {
        //            ShowDeadEyes();
        //            Debug.WriteLine(e.Message);
        //        }
        //    }

        //    return InstanceCreateCache[className].Invoke();

        //}

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
                // Wink Left
                WinkLeft.Begin();
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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

            if (context.IsIoTCore)
            {
                SetLCD();
            }
        }


    }
}
