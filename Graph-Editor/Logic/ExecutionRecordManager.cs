using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Graph_Editor.Data.ExecutionRecord;

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
    }
}
