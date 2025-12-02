using Graph_Editor.Data.ExecutionRecord;
using Graph_Editor.Data.SampleQuery;
using Graph_Editor.Pages.MainEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Graph_Editor
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        )]
    [JsonSerializable(typeof(List<ExecutionRecord>))]
    [JsonSerializable(typeof(SampleQueryItem))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
