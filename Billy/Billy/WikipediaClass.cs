using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Billy
{

        public class Normalized
        {
            public string from { get; set; }
            public string to { get; set; }
        }

        public class page
        {
            public int pageid { get; set; }
            public int ns { get; set; }
            public string title { get; set; }
            public string extract { get; set; }
        }

        public class Pages
        {
            public page page { get; set; }
        }

        public class Query
        {
            public List<Normalized> normalized { get; set; }
            public Pages pages { get; set; }
        }

        public class WikipediaClass
        {
            public string batchcomplete { get; set; }
            public Query query { get; set; }
        }

}
