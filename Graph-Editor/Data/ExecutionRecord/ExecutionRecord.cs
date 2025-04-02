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
    }
}
