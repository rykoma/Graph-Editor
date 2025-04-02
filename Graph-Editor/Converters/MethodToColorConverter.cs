using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Converters
{
    public class MethodToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string method)
            {
                bool isDarkMode = IsDarkMode();

                return method switch
                {
                    "GET" => new SolidColorBrush(isDarkMode ? Colors.Aqua : Colors.Blue),
                    "POST" => new SolidColorBrush(isDarkMode ? Colors.LightGreen : Colors.DarkGreen),
                    "PUT" => new SolidColorBrush(isDarkMode ? Colors.Plum : Colors.Purple),
                    "PATCH" => new SolidColorBrush(isDarkMode ? Colors.LightSalmon : Colors.DarkOrange),
                    "DELETE" => new SolidColorBrush(isDarkMode ? Colors.LightCoral : Colors.DarkRed),
                    _ => new SolidColorBrush(isDarkMode ? Colors.White : Colors.Black),
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return new SolidColorBrush(Colors.Black);
        }

        private bool IsDarkMode()
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var backgroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            
            // Use the R+G+B value to determine if the background is dark or light
            return backgroundColor.R + backgroundColor.G + backgroundColor.B < 255 * 3 / 2;
        }
    }
}
