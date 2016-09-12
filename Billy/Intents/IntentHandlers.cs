//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
//
// Microsoft Cognitive Services (formerly Project Oxford): https://www.microsoft.com/cognitive-services

//
// Microsoft Cognitive Services (formerly Project Oxford) GitHub:
// https://github.com/Microsoft/ProjectOxford-ClientSDK

//
// Copyright (c) Microsoft Corporation
// All rights reserved.
//
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

/*
 * Example of a class that handles intents routed to it via an IntentRouter
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
//using Microsoft.ProjectOxford.Luis;
using Microsoft.Cognitive.LUIS;
using System.Net.Http;
using System.Net;
using Windows.System.Threading;
using Windows.Devices.Pwm;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Billy
{
    //
    // Example Intent method
    //
    //[IntentHandler(0.65, Name = "Joke")]
    //public Task<bool> TellAJoke(LuisResult result, object context)
    //{
    //    //get our method of output from the context (in our case, writeline function to console)
    //    var write = (WriteLineFunc)context;

    //    //loop until the dialog response status is finished
    //    while (result.DialogResponse != null &&
    //        string.Compare(result.DialogResponse.Status, DialogStatus.Finished, StringComparison.OrdinalIgnoreCase) != 0)
    //    {
    //        //write the prompt of the dialog to the user and then let the user Reply with the answer
    //        write(result.DialogResponse.Prompt);
    //        var userInput = Console.ReadLine();
    //        result = result.Reply(userInput);
    //    }
    //    //finally when there is no more missing parameters, write the result of whatever operation was desired with its entities

    //    //get the entities from the result
    //    List<Entity> entities = result.GetAllEntities();

    //    write($"not static Booking flight for the following found entities: ");
    //    foreach (var e in entities)
    //    {
    //        write(e.Name + " :" + e.Value);
    //    }

    //    return Task.FromResult(true);
    //}

    class IntentHandlers
    {

        #region Utilities
        private static string StripHTML(string HTMLText, bool decode = true)
        {
            Regex reg = new Regex("<[^>]+>", RegexOptions.IgnoreCase);
            var stripped = reg.Replace(HTMLText, "");
            return decode ? System.Net.WebUtility.HtmlDecode(stripped) : stripped;
        }

        static string UppercaseWords(string value)
        {
            char[] array = value.ToCharArray();
            // Handle the first letter in the string.
            if (array.Length >= 1)
            {
                if (char.IsLower(array[0]))
                {
                    array[0] = char.ToUpper(array[0]);
                }
            }
            // Scan through the letters, checking for spaces.
            // ... Uppercase the lowercase letters following spaces.
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i - 1] == ' ')
                {
                    if (char.IsLower(array[i]))
                    {
                        array[i] = char.ToUpper(array[i]);
                    }
                }
            }
            return new string(array);
        }

        ThreadPoolTimer timer;
        double ClockwisePulseLength = 1;
        double CounterClockwisePulseLength = 2;
        double RestingPulseLegnth = 0;
        double currentPulseLength = 0;
        double secondPulseLength = 0;
        int iteration = 0;
        PwmPin motorPin;
        PwmController pwmController;

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            if (iteration > 4)
            {
                timer.Cancel();
                motorPin.Stop();
                motorPin.Dispose();
                return;
            }
            iteration++;

            System.Diagnostics.Debug.WriteLine(iteration);

            if(iteration==1 || iteration == 3)
            {
                currentPulseLength = ClockwisePulseLength;
                secondPulseLength = CounterClockwisePulseLength;
            }
            if(iteration==2 || iteration == 4)
            {
                currentPulseLength = CounterClockwisePulseLength;
                secondPulseLength = ClockwisePulseLength;
            }

            double desiredPercentage = currentPulseLength / (1000.0 / pwmController.ActualFrequency);
            motorPin.SetActiveDutyCyclePercentage(desiredPercentage);
        }

        #endregion

        //[IntentHandler(0.65, Name = "Joke")]
        //public async Task<bool> TellAJoke(LuisResult result, object context, object speak)
        //{
        //    var speech = (App.SpeechFunc)speak;

        //    return true;
        //}

        [IntentHandler(0.40, Name = "Rotate")]
        public async Task<bool> Rotate(LuisResult result, object context, object speak)
        {
            var speech = (App.SpeechFunc)speak;

            var c = (Context)context;

            if (c.IsIoTCore)
            {
                speech("Feel the burn baby!");

                pwmController = (await PwmController.GetControllersAsync(PwmSoftware.PwmProviderSoftware.GetPwmProvider()))[0];
                pwmController.SetDesiredFrequency(50);

                try
                {
                    motorPin = pwmController.OpenPin(26);
                }

                catch
                { }

                motorPin.SetActiveDutyCyclePercentage(RestingPulseLegnth);
                motorPin.Start();
                iteration = 0;
                timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromSeconds(1));
            }
            else
                speech("I am a fully functioning PC, not a robot.");
            return true;
        }

        [IntentHandler(0.65, Name = "Time")]
        public async Task<bool> TellTime(LuisResult result, object context, object speak)
        {
            var speech = (App.SpeechFunc)speak;

            string res = DateTime.Now.TimeOfDay.ToString();
            speech(res);

            return true;
        }

        [IntentHandler(0.65, Name = "Weather")]
        public async Task<bool> Weather(LuisResult result, object context, object speak)
        {
            var speech = (App.SpeechFunc)speak;

            HttpClient client = new HttpClient();
            string uri = "http://api.openweathermap.org/data/2.5/weather?appid=1e580b1b403f9cd044477e9e15479e9e&units=metric&q=Cambridge,UK";

            HttpResponseMessage ressp = await client.GetAsync(new Uri(uri, UriKind.Absolute));

            string res = await ressp.Content.ReadAsStringAsync();

            Billy.OpenWeatherMap.OpenWeatherMap weather = JsonConvert.DeserializeObject<Billy.OpenWeatherMap.OpenWeatherMap>(res);

            string msg = "Today it will be " + weather.weather[0].description + " with a maximum temperature of " + weather.main.temp_max;

            speech(msg);

            return true;
        }

        [IntentHandler(0.65, Name = "toggleswitch")]
        public async Task<bool> ToggleSwitch(LuisResult result, object context, object speak)
        {
            var speech = (App.SpeechFunc)speak;
            var intentContext = (Billy.Context)context;

            try
            {
                await intentContext.switchConsumer.SetValueAsync(!intentContext.lightStatus);
                intentContext.lightStatus = !intentContext.lightStatus;
                speech("sure thing!");
            }
            catch
            {
                //
                speech("eeek! failure");
                return false;
            }

            return true;
        }

        [IntentHandler(0.65, Name = "SwitchOff")]
        public async Task<bool> SwitchOffLights(LuisResult result, object context, object speak)
        {
            var intentContext = (Billy.Context)context;
            var speech = (App.SpeechFunc)speak;
            string s = "";

            if (!intentContext.lightStatus)
            {
                s = "The lights are already off";
            }
            else
            {
                await intentContext.switchConsumer.SetValueAsync(false);
                intentContext.lightStatus = false;
                s = "lights out"; // should have a generate response routine to hit here.
            }

            speech(s);

            return true;
        }

        [IntentHandler(0.65, Name ="SwitchOn")]
        public async Task<bool> SwitchLightsOn(LuisResult result, object context, object speak)
        {
            // get our method of output from the speak
            // get out service context from context.
            var intentContext = (Billy.Context)context;
            var speech = (App.SpeechFunc)speak;

            string s;
            if (intentContext.lightStatus)
            {
                s = "The lights are already on";
            }
            else
            {
                await intentContext.switchConsumer.SetValueAsync(true);
                intentContext.lightStatus = true;
                s = "my pleasure"; // should have a generate response routine to hit here.
            }

            speech(s);

            return true;


        }

        // 0.65 is the confidence score required by this intent in order to be activated
        // Only picks out a single entity value
        [IntentHandler(0.65, Name = "Joke")]
        public async Task<bool> TellAJoke(LuisResult result, object context, object speak)
        {
            var speech = (App.SpeechFunc)speak;

            HttpClient client = new HttpClient();
            string uri = "http://ppjokeservice.azurewebsites.net/api/jokes";

            HttpResponseMessage ressp = await client.GetAsync(new Uri(uri, UriKind.Absolute));

            string res = await ressp.Content.ReadAsStringAsync();

            speech(res);

            return true;
        }

        [IntentHandler(0.65, Name = "Lookup")]
        public async Task<bool> WikipediaLookup(LuisResult result, object context, object speak)
        {
            string s;
            var speech = (App.SpeechFunc)speak;

            //get the entities from the result
            List<Microsoft.Cognitive.LUIS.Entity> entities = result.GetAllEntities();

            if (entities.Count>0)
            {
                s = "I'll lookup " + entities[0].Value;
            }
            else
            {
                s = "looking it up";
            }

            speech(s);

            string query = result.OriginalQuery;
            // See if we can work out the look up topic from the query parameters
            query = query.Replace("lookup", "");
            query = query.Replace("look up", "");
            query = query.TrimStart(' ');
            System.Diagnostics.Debug.WriteLine(query);

            HttpClient client = new HttpClient();

            //query = UppercaseWords(query);

            string urie = WebUtility.UrlEncode(query);


            //string uri = "https://en.wikipedia.org/w/api.php?action=query&titles=" + urie + "&prop=revisions&rvprop=content&format=json";
            string uri = "https://en.wikipedia.org/w/api.php?format=json&action=query&prop=extracts&exintro=&explaintext=&titles=" + urie;

            HttpResponseMessage ressp = await client.GetAsync(new Uri(uri, UriKind.Absolute));

            string res = await ressp.Content.ReadAsStringAsync();

            int start = 0;
            int end = 0;

            start = res.IndexOf("missing");
            if (start > 0)
            {
                speech("Topic not found");
                return false;
            }

            start = res.IndexOf("extract");
            if (start > 0)
            {
                end = res.IndexOf("}}}}");
            }
            else
            {

                speech( "Topic not found");
                return false;
            }

            start = start + 10;
            string basic = res.Substring(start);// 10 = extract":"

            basic = basic.Replace("'", "");
            basic = basic.Replace("[", "");
            basic = basic.Replace("]", "");
            basic = basic.Replace("\n", "");
            basic = basic.Replace(".", " ");
            basic = basic.Replace("}", "");
            basic = basic.Replace("\\", "");
            speech(basic);

            return true;
        }

    }
}
