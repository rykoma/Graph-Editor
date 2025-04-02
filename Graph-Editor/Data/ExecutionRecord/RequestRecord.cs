using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Data.ExecutionRecord
{
    public class RequestRecord
    {
        public RequestRecord() { }

        public DateTime DateTime { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public bool IsBinaryBody { get; set; }
        public string Body { get; set; }
        public string FileName { get; set; }
    }
}
