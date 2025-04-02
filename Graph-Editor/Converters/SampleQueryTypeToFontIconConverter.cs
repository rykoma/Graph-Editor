using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Graph_Editor.Data.SampleQuery.SampleQueryItem;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;

namespace Graph_Editor.Converters
{
    class SampleQueryTypeToFontIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SampleQueryType sampleQueryType)
            {
                return sampleQueryType switch
                {
                    SampleQueryType.Category => "\uED42",
                    SampleQueryType.Query => "\uE7C3",
                    _ => "\uE7C3"
                };
            }
            return "\uE7C3";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return "\uE7C3";
        }
    }
}
