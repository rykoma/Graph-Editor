using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Graph_Editor.Data.ExecutionRecord
{
    public class ExecutionRecordList : IList<ExecutionRecord>
    {
        private List<ExecutionRecord> _records = new List<ExecutionRecord>();

        public ExecutionRecordList() { }

        public ExecutionRecordList(IEnumerable<ExecutionRecord> records)
        {
            _records = new List<ExecutionRecord>(records);
        }

        public ExecutionRecord this[int index] { get => _records[index]; set => _records[index] = value; }

        public int Count { get => _records.Count; }

        public bool IsReadOnly { get => false; }

        public void Add(ExecutionRecord item) => _records.Add(item);

        public void Clear()
        {
            _records.Clear();
        }

        public bool Contains(ExecutionRecord item)
        {
            return _records.Contains(item);
        }

        public void CopyTo(ExecutionRecord[] array, int arrayIndex)
        {
            _records.CopyTo(array, arrayIndex);
        }

        public IEnumerator<ExecutionRecord> GetEnumerator()
        {
            return _records.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _records.GetEnumerator();
        }

        public int IndexOf(ExecutionRecord item)
        {
            return _records.IndexOf(item);
        }

        public void Insert(int index, ExecutionRecord item)
        {
            _records.Insert(index, item);
        }

        public bool Remove(ExecutionRecord item)
        {
            return _records.Remove(item);
        }

        public ExecutionRecordList Sort(Comparison<ExecutionRecord> comparison)
        {
            _records.Sort(comparison);
            return this;
        }

        public void RemoveAt(int index)
        {
            _records.RemoveAt(index);
        }
        
        public void SaveAsJsonFile()
        {
            // Save as JSON file

            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultRequestAndResponseLoggingFolderPath = Path.Join(documentsPath, "Graph Editor");

            // Create the directory if it doesn't exist
            if (!Directory.Exists(defaultRequestAndResponseLoggingFolderPath))
            {
                Directory.CreateDirectory(defaultRequestAndResponseLoggingFolderPath);
            }

            string path = Path.Join(defaultRequestAndResponseLoggingFolderPath, "ExecutionRecordList.json");

            var json = JsonSerializer.Serialize(_records, typeof(List<ExecutionRecord>), SourceGenerationContext.Default);

            int maxRetries = 3;
            int delay = 1000; // in milliseconds
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    File.WriteAllText(path, json);
                    break; // Success, exit the loop
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    // Log the exception if needed
                    System.Diagnostics.Debug.WriteLine($"Attempt {attempt + 1} failed: {ex.Message}");
                    System.Threading.Thread.Sleep(delay); // Wait before retrying
                }
            }
        }

        internal static ExecutionRecordList CreateFromJson(string Json)
        {
            var records = JsonSerializer.Deserialize(Json, typeof(List<ExecutionRecord>), SourceGenerationContext.Default) as List<ExecutionRecord>;
            var recordList = new ExecutionRecordList();
            if (records != null)
            {
                recordList._records = records;
            }
            return recordList;
        }
    }
}
