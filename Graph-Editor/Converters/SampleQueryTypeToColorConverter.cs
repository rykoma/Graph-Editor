using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Graph_Editor.Data.SampleQuery.SampleQueryItem;

namespace Graph_Editor.Converters
{
    internal class SampleQueryTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SampleQueryType sampleQueryType)
            {
                return sampleQueryType switch
                {
                    SampleQueryType.Category => new SolidColorBrush(ColorHelper.FromArgb(255, 247, 229, 130)),
                    SampleQueryType.Query => new SolidColorBrush(Colors.Black),
                    _ => new SolidColorBrush(Colors.Black)
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
