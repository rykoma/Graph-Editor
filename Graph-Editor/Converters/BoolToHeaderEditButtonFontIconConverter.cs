using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Converters
{
    public class BoolToHeaderEditButtonFontIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                if (boolValue)
                {
                    return new FontIcon()
                    {
                        Glyph = "\uE70F" // Edit icon
                    };
                }
                else
                {
                    return new FontIcon()
                    {
                        Glyph = "\uE74E" // Save icon
                    };
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
