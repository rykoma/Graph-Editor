using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Data.ExecutionRecord
{
    public class ExecutionRecord
    {
        public ExecutionRecord() { }

        public RequestRecord Request { get; set; }
        public ResponseRecord Response { get; set; }

        public string CreateSimpleSummary()
        {
            var summary = new StringBuilder();

            if (Request == null)
            {
                summary.AppendLine("Request: null");
            }
            else
            {
                summary.AppendLine($"{Request.Method} {Request.Url}");
            }

            if (Response == null)
            {
                summary.AppendLine("Response: null");
            }
            else
            {
                summary.AppendLine($"{(int)Response.StatusCode} {Response.StatusCode.ToString()}");
            }

            return summary.ToString();
        }

        public string CreateFullDetails()
        {
            var details = new StringBuilder();

            // Request
            if (Request != null)
            {
                // Request method and URL
                details.AppendLine($"{Request.Method} {Request.Url}");

                // Request headers
                foreach (var header in Request.Headers)
                {
                    details.AppendLine($"{header.Key}: {header.Value}");
                }

                // Request body
                if (!string.IsNullOrEmpty(Request.Body))
                {
                    details.AppendLine();
                    details.AppendLine(Request.Body);
                }
            }
            else
            {
                details.AppendLine("Request: null");
            }

            // Blank line between request and response
            details.AppendLine();

            // Response
            if (Response != null)
            {
                // Response status code
                details.AppendLine($"{(int)Response.StatusCode} {Response.StatusCode.ToString()}");

                // Response headers
                foreach (var header in Response.Headers)
                {
                    details.AppendLine($"{header.Key}: {header.Value}");
                }

                // Response body
                switch (Response.DisplayMode)
                {
                    case ResponseRecord.ResponseBodyDisplayMode.Json:
                        details.AppendLine();
                        GraphEditorApplication.TryParseJson(Response.BodyString, out string parsedJsonStringResult);
                        details.AppendLine(GraphEditorApplication.RemoveProblematicCharacters(parsedJsonStringResult));
                        break;
                    case ResponseRecord.ResponseBodyDisplayMode.PlainText:
                    case ResponseRecord.ResponseBodyDisplayMode.Csv:
                        if (string.IsNullOrEmpty(Response.BodyString) == false)
                        {
                            details.AppendLine();
                            details.AppendLine(Response.BodyString);
                        }
                        break;
                    case ResponseRecord.ResponseBodyDisplayMode.Image:
                        details.AppendLine();
                        details.AppendLine(Response.Base64EncodedBinaryBody);
                        break;
                    default:
                        details.AppendLine();
                        details.AppendLine("Unknown response body format.");
                        break;
                }
            }
            else
            {
                details.AppendLine("Response: null");
            }

            return details.ToString();
        }
    }
}
