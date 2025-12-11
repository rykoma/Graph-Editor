using Graph_Editor.Data.ExecutionRecord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Graph_Editor.Logic
{
    internal static class ExecutionRecordManager
    {
        internal static ExecutionRecordList ExecutionRecordList
        {
            get
            {
                // Load from JSON file
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string defaultRequestAndResponseLoggingFolderPath = Path.Join(documentsPath, "Graph Editor");
                string path = Path.Join(defaultRequestAndResponseLoggingFolderPath, "ExecutionRecordList.json");

                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var tempList = ExecutionRecordList.CreateFromJson(json);

                        // Sort by date
                        return tempList.Sort((x, y) => y.Request.DateTime.CompareTo(x.Request.DateTime));
                    }
                    catch
                    {
                        return new ExecutionRecordList();
                    }
                }
                else
                {
                    return new ExecutionRecordList();
                }
            }
        }

        internal static void AddExecutionRecord(ExecutionRecord record)
        {
            var list = ExecutionRecordList;
            int maxCount = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_MaxExecutionRecordCount, GraphEditorApplication.DefaultMaxExecutionRecordCount);

            // Remove the oldest record if the list is full
            if (list.Count >= maxCount)
            {
                list.RemoveAt(list.Count - 1);
            }

            list.Add(record);
            list.SaveAsJsonFile();
        }

        internal static void SaveExecutionRecordList(ExecutionRecordList list)
        {
            list.SaveAsJsonFile();
        }

        internal static bool TryParseClipboardTextToRequestRecord(out RequestRecord requestRecord)
        {
            requestRecord = null;

            try
            {
                string clipboardText;

                var dataPackageView = Clipboard.GetContent();
                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    clipboardText = dataPackageView.GetTextAsync().AsTask().Result;
                }
                else
                {
                    clipboardText = string.Empty;
                }

                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    return false;
                }

                var lines = clipboardText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                // Parse method and URL
                string[] firstLineParts = lines[0].Split(' ');
                var method = firstLineParts[0];
                var url = string.Join(' ', firstLineParts.Skip(1));

                if (!url.StartsWith("https://graph.microsoft.com/") && url.StartsWith("/"))
                {
                    url = "https://graph.microsoft.com/v1.0" + url;
                }

                // Check the format of the first line
                var isValidFormat = (method == "GET" || method == "POST" || method == "PUT" || method == "PATCH" || method == "DELETE") && url.StartsWith("https://");
                if (!isValidFormat)
                {
                    return false;
                }
                
                // Parse headers
                var headers = new Dictionary<string, string>();
                int i = 1;
                for (; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        break;
                    }
                    var headerParts = lines[i].Split(new[] { ':' }, 2);
                    if (headerParts.Length == 2)
                    {
                        string headerName = headerParts[0].Trim();
                        string headerValue = headerParts[1].Trim();

                        if (headerName.ToLower() == "content-type" && headerValue.ToLower() == "application/json")
                        {
                            // Skip the Content-Type header
                            continue;
                        }

                        if (headerName.ToLower() == "authorization" && headerValue.ToLower().StartsWith("bearer "))
                        {
                            // Skip the Bearer token
                            continue;
                        }

                        headers[headerName] = headerValue;
                    }
                }

                // Parse body
                var body = string.Join("\n", lines.Skip(i + 1));

                // Create RequestRecord
                requestRecord = new RequestRecord
                {
                    DateTime = DateTime.Now,
                    Method = method,
                    Url = url,
                    Headers = headers,
                    IsBinaryBody = false,
                    Body = body,
                    FileName = string.Empty
                };

                return true;
            }
            catch
            {
                // Ignore errors
                return false;
            }
        }
    }
}
