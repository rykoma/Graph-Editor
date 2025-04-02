using Microsoft.Identity.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WASDK = Microsoft.WindowsAppSDK;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.About
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AboutContainer : Page
    {
        public AboutContainer()
        {
            this.InitializeComponent();
            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            string buildType = "Release";

#if DEBUG
            buildType = "Debug";
#endif

            var appVersion = Windows.ApplicationModel.Package.Current.Id.Version;
            TextBlock_Version.Text = "Version " + appVersion.Major + "." + appVersion.Minor + "." + appVersion.Build + " (" + buildType + " Build)";

            var msalVersion = typeof(PublicClientApplication).Assembly.GetName().Version.ToString();
            TextBlock_MsalVersion.Text = $"MSAL Version: {msalVersion}";

            var windowsAppSdkVersion = string.Format("{0}.{1}", WASDK.Release.Major, WASDK.Release.Minor);
            TextBlock_WindowsAppSdkVersion.Text = $"Windows App SDK Version: {windowsAppSdkVersion}";
        }
    }
}
