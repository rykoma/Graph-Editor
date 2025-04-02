using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Data.EditorAccessToken
{
    internal class EditorAccessToken
    {
        private static EditorAccessToken _instance;
        public static EditorAccessToken Instance => _instance ?? (_instance = new EditorAccessToken());

        public AuthenticationResult AuthenticationResult { get; set; }
    }
}
