using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.GlobalSetting
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GlobalSettingGeneralPage : Page
    {
        public GlobalSettingGeneralPage()
        {
            this.InitializeComponent();

            // Load current settings

            // Load Request and Response Logging setting
            ToggleSwitch_EnableRequestAndResponseLogging.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingEnabled, false);
            ToggleSwitch_EnableRequestAndResponseLogging.Toggled += ToggleSwitch_EnableRequestAndResponseLogging_Toggled;

            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultRequestAndResponseLoggingFolderPath = Path.Join(documentsPath, "Graph Editor");
            TextBlock_LogFolderPathValue.Text = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingFolderPath, defaultRequestAndResponseLoggingFolderPath);

            ToggleSwitch_DisableRequestAndResponseLoggingWhenAppRestart.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_DisableRequestAndResponseLoggingWhenAppRestart, true);
            ToggleSwitch_DisableRequestAndResponseLoggingWhenAppRestart.Toggled += ToggleSwitch_DisableRequestAndResponseLoggingWhenAppRestart_Toggled;

            ToggleSwitch_ExcludeAuthorizationHeader.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_ExcludeAuthorizationHeader, true);
            ToggleSwitch_ExcludeAuthorizationHeader.Toggled += ToggleSwitch_ExcludeAuthorizationHeader_Toggled;

            NumberBox_RequestAndResponseLoggingRetentionDays.Value = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingRetentionDays, 30);
            NumberBox_RequestAndResponseLoggingRetentionDays.ValueChanged += NumberBox_RequestAndResponseLoggingRetentionDays_ValueChanged;

            // Load supported languages
            string currentDisplayLanguageSetting = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_DisplayLanguageOverride, string.Empty);

            int selectedLanguageIndex = 0;

            for (int i = 0; i < GraphEditorApplication.SupportedLanguages.Length; i++)
            {
                ComboBox_Language.Items.Add(new ComboBoxItem() { Content = GraphEditorApplication.SupportedLanguages[i].Language, Tag = GraphEditorApplication.SupportedLanguages[i].Locale });

                if (currentDisplayLanguageSetting == GraphEditorApplication.SupportedLanguages[i].Locale)
                {
                    selectedLanguageIndex = i;
                }
            }

            ComboBox_Language.SelectedIndex = selectedLanguageIndex;
            ComboBox_Language.SelectionChanged += ComboBox_Language_SelectionChanged;

            // Load Auto Redirect setting
            ToggleSwitch_AllowAutoRedirect.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.MainEditorLoggingHandler_AllowAutoRedirect, true);
            ToggleSwitch_AllowAutoRedirect.Toggled += ToggleSwitch_AllowAutoRedirect_Toggled;

            // Load Max Execution Record Count setting
            NumberBox_MaxExecutionRecordCount.Value = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_MaxExecutionRecordCount, GraphEditorApplication.DefaultMaxExecutionRecordCount);
            NumberBox_MaxExecutionRecordCount.ValueChanged += NumberBox_MaxExecutionRecordCount_ValueChanged;

            // Load encode settings
            CheckBox_EncodePlus.IsChecked = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_EncodePlusCharacter, true);
            CheckBox_EncodePlus.Checked += CheckBox_EncodePlus_Checked;
            CheckBox_EncodePlus.Unchecked += CheckBox_EncodePlus_Unchecked;

            CheckBox_EncodeSharp.IsChecked = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_EncodeSharpCharacter, false);
            CheckBox_EncodeSharp.Checked += CheckBox_EncodeSharp_Checked;
            CheckBox_EncodeSharp.Unchecked += CheckBox_EncodeSharp_Unchecked;

            // Load request confirmation settings
            ToggleSwitch_EnableRequestConfirmation_GET.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_GET, false);
            ToggleSwitch_EnableRequestConfirmation_GET.Toggled += ToggleSwitch_EnableRequestConfirmation_GET_Toggled;

            ToggleSwitch_EnableRequestConfirmation_POST.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_POST, false);
            ToggleSwitch_EnableRequestConfirmation_POST.Toggled += ToggleSwitch_EnableRequestConfirmation_POST_Toggled;

            ToggleSwitch_EnableRequestConfirmation_PUT.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_PUT, false);
            ToggleSwitch_EnableRequestConfirmation_PUT.Toggled += ToggleSwitch_EnableRequestConfirmation_PUT_Toggled;

            ToggleSwitch_EnableRequestConfirmation_PATCH.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_PATCH, false);
            ToggleSwitch_EnableRequestConfirmation_PATCH.Toggled += ToggleSwitch_EnableRequestConfirmation_PATCH_Toggled;

            ToggleSwitch_EnableRequestConfirmation_DELETE.IsOn = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_DELETE, true);
            ToggleSwitch_EnableRequestConfirmation_DELETE.Toggled += ToggleSwitch_EnableRequestConfirmation_DELETE_Toggled;
        }

        private void ToggleSwitch_EnableRequestAndResponseLogging_Toggled(object sender, RoutedEventArgs e)
        {
            // Save the logging setting
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingEnabled, ToggleSwitch_EnableRequestAndResponseLogging.IsOn);
        }

        private void ToggleSwitch_DisableRequestAndResponseLoggingWhenAppRestart_Toggled(object sender, RoutedEventArgs e)
        {
            // Save the setting
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_DisableRequestAndResponseLoggingWhenAppRestart, ToggleSwitch_DisableRequestAndResponseLoggingWhenAppRestart.IsOn);
        }

        private void ToggleSwitch_ExcludeAuthorizationHeader_Toggled(object sender, RoutedEventArgs e)
        {
            // Save the setting
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_ExcludeAuthorizationHeader, ToggleSwitch_ExcludeAuthorizationHeader.IsOn);
        }

        private void NumberBox_RequestAndResponseLoggingRetentionDays_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // Ensure the value is an integer between -1 and 365
            int value = (int)sender.Value;
            if (value < -1)
            {
                value = -1;
            }
            else if (value > 365)
            {
                value = 365;
            }
            sender.Value = value;

            // Save the setting
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingRetentionDays, value);
        }

        private async void Button_PickLogFolderPath_Click(object sender, RoutedEventArgs e)
        {
            // Create a folder picker
            FolderPicker openPicker = new Windows.Storage.Pickers.FolderPicker();

            // Retrieve the window handle (HWND) of the main window.
            var window = (Application.Current as App)?.MainWindowAccessor;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            // Initialize the folder picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            // Set options
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add("*");
            
            // Open the picker for the user to pick a folder
            StorageFolder folder = await openPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                TextBlock_LogFolderPathValue.Text = folder.Path;

                // Save the log folder path setting
                GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingFolderPath, TextBlock_LogFolderPathValue.Text);
            }
        }

        private void ComboBox_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedLanguage = (ComboBox_Language.SelectedItem as ComboBoxItem).Tag.ToString();
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_DisplayLanguageOverride, selectedLanguage);

            // Override primary display language.
            // https://learn.microsoft.com/en-us/windows/uwp/app-resources/localize-strings-ui-manifest
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = selectedLanguage;
        }

        private void ToggleSwitch_AllowAutoRedirect_Toggled(object sender, RoutedEventArgs e)
        {
            // Save the auto redirection setting
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.MainEditorLoggingHandler_AllowAutoRedirect, ToggleSwitch_AllowAutoRedirect.IsOn);
        }

        private void NumberBox_MaxExecutionRecordCount_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (double.IsNaN(sender.Value))
            {
                sender.Value = GraphEditorApplication.DefaultMaxExecutionRecordCount; // Reset to default value
            }

            // Ensure the value is an integer between 0 and 100
            int value = (int)sender.Value;
            if (value < 0)
            {
                value = 0;
            }
            else if (value > 100)
            {
                value = 100;
            }
            sender.Value = value;

            // Save the setting
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_MaxExecutionRecordCount, value);
        }

        private void CheckBox_EncodePlus_Checked(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_EncodePlusCharacter, true);
        }

        private void CheckBox_EncodePlus_Unchecked(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_EncodePlusCharacter, false);
        }

        private void CheckBox_EncodeSharp_Checked(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_EncodeSharpCharacter, true);
        }

        private void CheckBox_EncodeSharp_Unchecked(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_EncodeSharpCharacter, false);
        }

        private void ToggleSwitch_EnableRequestConfirmation_GET_Toggled(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_GET, ToggleSwitch_EnableRequestConfirmation_GET.IsOn);
        }

        private void ToggleSwitch_EnableRequestConfirmation_POST_Toggled(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_POST, ToggleSwitch_EnableRequestConfirmation_POST.IsOn);
        }

        private void ToggleSwitch_EnableRequestConfirmation_PUT_Toggled(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_PUT, ToggleSwitch_EnableRequestConfirmation_PUT.IsOn);
        }

        private void ToggleSwitch_EnableRequestConfirmation_PATCH_Toggled(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_PATCH, ToggleSwitch_EnableRequestConfirmation_PATCH.IsOn);
        }

        private void ToggleSwitch_EnableRequestConfirmation_DELETE_Toggled(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_DELETE, ToggleSwitch_EnableRequestConfirmation_DELETE.IsOn);
        }
    }
}
