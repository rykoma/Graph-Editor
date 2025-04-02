using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Data.ExecutionRecord
{
    public class ResponseRecord
    {
        public ResponseRecord() { }

        public DateTime DateTime { get; set; }

        public HttpStatusCode StatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string ContentType { get; set; }
        public string BodyString { get; set; }
        public string Base64EncodedBinaryBody { get; set; }
    }
}
