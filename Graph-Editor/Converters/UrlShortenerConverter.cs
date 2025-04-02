using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Converters
{
    class UrlShortenerConverter : IValueConverter
    {
        public object Convert(object Value, Type TargetType, object Parameter, string Language)
        {
            if (Value is string url && url.StartsWith("https://graph.microsoft.com/"))
            {
                return url.Replace("https://graph.microsoft.com/", "");
            }
            return Value;
        }

        public object ConvertBack(object Value, Type TargetType, object Parameter, string Language)
        {
            throw new NotImplementedException();
        }
    }
}
