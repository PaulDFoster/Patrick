using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Billy
{
    class IntentResult
    {
        public bool result = false;
        public string TTS = "";

        public IntentResult(bool b, string s)
        {
            result = b;
            TTS = s;
        }
    }
}
