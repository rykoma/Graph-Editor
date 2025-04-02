using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Converters
{
    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dateTime)
            {
                // Convert to local time
                var localDateTime = dateTime.ToLocalTime();

                // Get culture
                string currentDisplayLanguageSetting = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_DisplayLanguageOverride, string.Empty);

                if (currentDisplayLanguageSetting == string.Empty)
                {
                    currentDisplayLanguageSetting = CultureInfo.CurrentCulture.Name;
                }

                // Convert to string
                CultureInfo culture;

                try
                {
                    culture = new CultureInfo(currentDisplayLanguageSetting);
                }
                catch
                {
                    culture = new CultureInfo("en-US");
                }

                return localDateTime.ToString(culture);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
