using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
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

        [JsonIgnore]
        public ResponseBodyDisplayMode DisplayMode
        {
            get
            {
                if (string.IsNullOrEmpty(ContentType))
                {
                    return ResponseBodyDisplayMode.PlainText;
                }
                else if (string.Compare(ContentType, "application/json", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return ResponseBodyDisplayMode.Json;
                }
                else if (string.Compare(ContentType, "text/plain", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return ResponseBodyDisplayMode.PlainText;
                }
                else if (string.Compare(ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return ResponseBodyDisplayMode.PlainText;
                }
                else if (ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return ResponseBodyDisplayMode.Image;
                }
                else
                {
                    return ResponseBodyDisplayMode.PlainText;
                }

            }
        }

        public enum ResponseBodyDisplayMode
        {
            Json,
            PlainText,
            Image
        }
    }
}
