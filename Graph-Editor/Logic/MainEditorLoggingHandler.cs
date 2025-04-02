using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Graph_Editor.Logic
{
    internal class MainEditorLoggingHandler : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_EncodePlusCharacter, true) == true)
            {
                request.RequestUri = new Uri(request.RequestUri.ToString().Replace("+", "%2B"));
            }

            if (GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_EncodeSharpCharacter, false) == true)
            {
                request.RequestUri = new Uri(request.RequestUri.ToString().Replace("#", "%23"));
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"{DateTime.UtcNow.ToString("o")}, Request, {request.Method.ToString()} {request.RequestUri}");
            sb.AppendLine($"Version: {request.Version}");
            sb.AppendLine("Headers:");
            sb.AppendLine("{");
            foreach (var header in request.Headers)
            {
                if (header.Key == "Authorization")
                {
                    continue;
                }

                sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            if (request.Content != null)
            {
                sb.AppendLine($"  Content-Type: {request.Content.Headers.ContentType.ToString()}");
                sb.AppendLine("}");
                sb.AppendLine("Body:");
                sb.AppendLine("{");
                sb.AppendLine($"  {await request.Content.ReadAsStringAsync()}");
            }
            sb.AppendLine("}");
            
            var response = await base.SendAsync(request, cancellationToken);

            sb.AppendLine($"{DateTime.UtcNow.ToString("o")}, Response, StatusCode: {(int)response.StatusCode}");
            sb.AppendLine($"Version: {request.Version}");
            sb.AppendLine("Headers:");
            sb.AppendLine("{");
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            if (response.Content != null)
            {
                if (response.Content.Headers != null && response.Content.Headers.ContentType != null)
                {
                    sb.AppendLine($"  Content-Type: {response.Content.Headers.ContentType.ToString()}");
                }
                sb.AppendLine("}");
                sb.AppendLine("Body:");
                sb.AppendLine("{");
                sb.AppendLine($"  {await response.Content.ReadAsStringAsync()}");
            }
            sb.AppendLine("}");

            if (GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingEnabled, false) == true)
            {
                WriteLog(sb);
            }

            // Custom redirect handling

            int numericStatusCode = (int)response.StatusCode;

            if (numericStatusCode >= 300 && numericStatusCode <= 399)
            {
                // Redirect
                if (GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.MainEditorLoggingHandler_AllowAutoRedirect, true) == true)
                {
                    // Auto redirect is enabled

                    if (response.Headers.Location != null)
                    {
                        // Redirect to the location
                        request.RequestUri = response.Headers.Location;
                        response = await SendAsync(request, cancellationToken);
                    }
                }
                
            }

            return response;
        }

        private bool WriteLog(StringBuilder Message)
        {
            // Write log.

            Message.AppendLine("");

            string logFilePath = "";

            string settingLofFilePath = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingFolderPath, "");

            if (!Directory.Exists(settingLofFilePath))
            {
                // Specified log folder path is not exsisting.
                return false;
            }

            logFilePath = Path.Combine(settingLofFilePath, "MainEditorLog_" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");

            try
            {
                using (StreamWriter sw = new(logFilePath, true, Encoding.UTF8))
                {
                    sw.Write(Message.ToString());
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
    }
}
