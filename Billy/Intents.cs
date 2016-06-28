using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using Windows.System.Threading;
using Windows.Devices.Pwm;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Billy //builtin.intent
{
    public interface IIntent
    {
        Task<string> Execute();
        List<Entity> entities { get; set; }
        string query { get; set; }
        Context context { get; set; }

    }

    public class Intent
    {
        public string intent { get; set; }
        public double score { get; set; }
    }

    public class Entity
    {
        public string entity { get; set; }
        public string type { get; set; }
        public int startIndex { get; set; }
        public int endIndex { get; set; }
        public double score { get; set; }
    }

    public class LUCIS
    {
        public string query { get; set; }
        public List<Intent> intents { get; set; }
        public List<Entity> entities { get; set; }
    }

    public class intentBase
        {
        public string query { get; set; }
        public List<Entity> entities { get; set; }
        public Context context { get; set; }

        public static void Create(){ }

    }

    public class Lookup : intentBase,IIntent
    {
        public Lookup()
        {
            Debug.WriteLine("builtin.intent.Lookup");
            entities = null;
            query = "";
        }

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

        public async Task<string> Execute()
        {
            string s;
            if (this.entities != null)
            {
                s = "I'll lookup " + this.entities[0].entity;

                //    Prep look up topic here
            }
            else
            {
                s = "looking it up";
            }
            // See if we can work out the look up topic from the query parameters
            query = query.Replace("lookup", "");
            query = query.Replace("look up", "");
            query = query.TrimStart(' ');
            Debug.WriteLine(query);

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
                return "Topic not found";

            start = res.IndexOf("extract");
            if (start > 0)
            {
                end = res.IndexOf("}}}}");
            }
            else
            {
                return "Topic not found";
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
            return basic;
        }
    }

    public class SwitchOn : intentBase, IIntent
    {
        public SwitchOn()
        {
            Debug.WriteLine("builtin.intent.SwitchOn");
            entities = null;
        }

        public async Task<string> Execute()
        {
            string s;
            if (context.lightStatus)
            {
                s = "The lights are already on";
            }
            else
            {
                await context.switchConsumer.SetValueAsync(true);
                context.lightStatus = true;
                s = "my pleasure"; // should have a generate response routine to hit here.
            }
            return s;
        }
    }

    public class Rotate : intentBase, IIntent
    {
        ThreadPoolTimer timer;
        double ClockwisePulseLength = 1;
        double CounterClockwisePulseLegnth = 2;
        double RestingPulseLegnth = 0;
        double currentPulseLength = 0;
        double secondPulseLength = 0;
        int iteration = 0;
        PwmPin motorPin;
        PwmController pwmController;

        public Rotate()
        {
            Debug.WriteLine("builtin.intent.Rotate");
            entities = null;
        }

        public async Task<string> Execute()
        {
            string s;

            s = "I'm looking around, and around, and around. oooooooh!";
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

            timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromSeconds(2));

            return s;
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            if(iteration>18)
            {
                timer.Cancel();
                motorPin.Stop();
                return;
            }
            iteration++;
            if (iteration % 3 == 0)
            {
                currentPulseLength = ClockwisePulseLength;
                secondPulseLength = CounterClockwisePulseLegnth;
            }
            else if (iteration % 3 == 1)
            {
                currentPulseLength = CounterClockwisePulseLegnth;
                secondPulseLength = ClockwisePulseLength;
            }
            else
            {
                currentPulseLength = 0;
                secondPulseLength = 0;
            }

            double desiredPercentage = currentPulseLength / (1000.0 / pwmController.ActualFrequency);
            motorPin.SetActiveDutyCyclePercentage(desiredPercentage);
        }
    }
    public class SwitchOff : intentBase, IIntent
    {
        public SwitchOff()
        {
            Debug.WriteLine("builtin.intent.SwitchOff");
            entities = null;
        }

        public async Task<string> Execute()
        {

            return await SwitchOffLights();
        }

        private async Task<string> SwitchOffLights()
        {
            string s = "";

            if (!context.lightStatus)
            {
                s = "The lights are already off";
            }
            else
            {
                await context.switchConsumer.SetValueAsync(false);
                context.lightStatus = false;
                s = "lights out"; // should have a generate response routine to hit here.
            }

            return s;
        }
    }

    public class toggleswitch : intentBase, IIntent
    {
        public toggleswitch()
        {
            Debug.WriteLine("builtin.intent.toggleswitch");
            entities = null;
        }

        public async Task<string> Execute()
        {
            ToggleLights();
            return "sure thing";
        }

        private async void ToggleLights()
        {
            try
            {
                await context.switchConsumer.SetValueAsync(!context.lightStatus);
                context.lightStatus = !context.lightStatus;
            }
            catch
            {
                //
            }
        }
    }

    public class none : intentBase,IIntent
    {
        public none()
        {
            Debug.WriteLine("builtin.intent.none");
            entities = null;
        }

        public async Task<string> Execute()
        {
            if (entities == null)
                Debug.WriteLine("No entities");
            else
                Debug.WriteLine("Entities");
            return "executed";
        }
    }


}

