using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            
            // TODO: We should do this based on user settings.
            //Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en";
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Migrate all settings to GUID keys (runs once)
            GraphEditorApplication.MigrateAllSettingsToGuidKeys();

            // Load request and response logging disabled setting
            if (GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_DisableRequestAndResponseLoggingWhenAppRestart, true) && GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingEnabled, false))
            {
                // Disable request and response logging
                GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingEnabled, false);
            }

            // Load request and response logging retention period setting
            int retentionPeriod = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingRetentionDays, 30);

            if (retentionPeriod >= 0)
            {
                // Delete old request and response logs
                try
                {
                    GraphEditorApplication.DeleteOldRequestAndResponseLogs(retentionPeriod);
                }
                catch
                {
                    // Ignore any exceptions
                }
            }
            
            MainWindowAccessor = new MainWindow();
            MainWindowAccessor.ExtendsContentIntoTitleBar = true; // Hide default title bar
            MainWindowAccessor.Activate();
        }

        public MainWindow MainWindowAccessor;
    }
}
