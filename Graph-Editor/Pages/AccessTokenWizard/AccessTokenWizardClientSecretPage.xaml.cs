using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Identity.Client;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.AccessTokenWizard
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AccessTokenWizardClientSecretPage : Page
    {
        public AccessTokenWizardClientSecretPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            TextBox_TenantName.Text = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.AccessTokenWizardClientSecretPage__TextBox_TenantName__Text, "");
            TextBox_ApplicationId.Text = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.AccessTokenWizardClientSecretPage__TextBox_ApplicationId__Text, "");
            PasswordBox_ClientSecret.Password = GraphEditorApplication.GetConfidentialSetting(GraphEditorApplication.Settings.AccessTokenWizardClientSecretPage__PasswordBox_ClientSecret__Password, "");
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Do not save app information at this time. Save it when the app has successfully acquired the access token.
        }

        public async Task<AuthenticationResult> AcquireAccessTokenAsync()
        {
            // Acquire the access token using MSAL.

            var app = ConfidentialClientApplicationBuilder
                .Create(TextBox_ApplicationId.Text)                
                .WithTenantId(TextBox_TenantName.Text)
                .WithClientSecret(PasswordBox_ClientSecret.Password)
                .Build();

            AuthenticationResult result = null;

            try
            {
                result = await app.AcquireTokenForClient(new string[] { "https://graph.microsoft.com/.default" })
                    .ExecuteAsync();

                // Save the tenant name, application ID and client credential, because the app has successfully acquired the access token.
                GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.AccessTokenWizardClientSecretPage__TextBox_TenantName__Text, TextBox_TenantName.Text);
                GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.AccessTokenWizardClientSecretPage__TextBox_ApplicationId__Text, TextBox_ApplicationId.Text);
                GraphEditorApplication.SaveConfidentialSetting(GraphEditorApplication.Settings.AccessTokenWizardClientSecretPage__PasswordBox_ClientSecret__Password, PasswordBox_ClientSecret.Password);
            }
            catch (MsalClientException ex)
            {
                if (ex.ErrorCode == "authentication_canceled")
                {
                    // Inform the user that the authentication was canceled using InforBar.
                    InforBar_AuthStatus.Message = GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardClientSecretPage", "Message_AuthenticationCanceled");
                    InforBar_AuthStatus.Severity = InfoBarSeverity.Warning;
                    InforBar_AuthStatus.IsOpen = true;
                }
                else
                {
                    // Handle other MSAL client exceptions
                    InforBar_AuthStatus.Message = $"{GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardClientSecretPage", "Message_UnexpectedMsalException")}: {ex.Message}";
                    InforBar_AuthStatus.Severity = InfoBarSeverity.Error;
                    InforBar_AuthStatus.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                // Handle non-MSAL exceptions
                InforBar_AuthStatus.Message = $"{GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardClientSecretPage", "Message_UnexpectedMsalException")}: {ex.Message}";
                InforBar_AuthStatus.Severity = InfoBarSeverity.Error;
                InforBar_AuthStatus.IsOpen = true;
            }

            return result;
        }

        private void MenuFlyoutItem_Copy_Click(object sender, RoutedEventArgs e)
        {
            var message = InforBar_AuthStatus.Message;
            if (!string.IsNullOrEmpty(message))
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(message);
                Clipboard.SetContent(dataPackage);
            }
        }
    }
}
