using Graph_Editor.Data.SampleQuery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Graph_Editor.Logic
{
    internal static class SampleQueryLoader
    {
        internal static SampleQueryItem LoadBuiltInSampleQueryData()
        {
            // Load from resource JSON file

            try
            {
                string json = string.Empty;
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Graph_Editor.Data.SampleQuery.BuiltInSampleQuery.json";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }

                var records = JsonSerializer.Deserialize(json, typeof(SampleQueryItem), SourceGenerationContext.Default) as SampleQueryItem;
                return records;
            }
            catch
            {
                return null;
            }
        }

        internal static SampleQueryItem LoadCustomSampleQueryData()
        {
            // Load from JSON file
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultRequestAndResponseLoggingFolderPath = Path.Join(documentsPath, "Graph Editor");
            string path = Path.Join(defaultRequestAndResponseLoggingFolderPath, "CustomSampleQuery.json");

            var json = File.ReadAllText(path);

            var records = JsonSerializer.Deserialize(json, typeof(SampleQueryItem), SourceGenerationContext.Default) as SampleQueryItem;
            return records;
        }

        internal static List<string> SampleUrls(SampleQueryItem RootItem)
        {
            var urls = new List<string>();

            if (RootItem != null)
            {
                if (RootItem.Url != null)
                {
                    if (RootItem.Url.StartsWith("https://graph.microsoft.com/v1.0/me"))
                    {
                        // Add normalized URL
                        urls.Add(RootItem.Url.Replace("https://graph.microsoft.com/v1.0/me", "https://graph.microsoft.com/v1.0/users/${UserObjectId}"));
                    }
                    else
                    {
                        urls.Add(RootItem.Url);
                    }
                }
            }

            if (RootItem.Children != null && RootItem.Children.Count >= 1)
            {
                foreach (var child in RootItem.Children)
                {
                    urls.AddRange(SampleUrls(child));
                }

            }

            urls.Add("https://graph.microsoft.com/v1.0/me");

            // Sort and return unique urls only
            return urls.Distinct().OrderBy(url => url).ToList();
        }
    }
}
