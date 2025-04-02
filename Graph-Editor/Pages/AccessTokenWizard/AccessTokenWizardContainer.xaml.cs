using Graph_Editor.Data.EditorAccessToken;
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
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.AccessTokenWizard
{
    public sealed partial class AccessTokenWizardContainer : Page
    {
        public AccessTokenWizardContainer()
        {
            this.InitializeComponent();

            contentFrame.Navigate(typeof(AccessTokenWizardWelcomePage));
            Button_Previous.IsEnabled = false;
            Button_Cancel.IsEnabled = false;
        }

        private void Button_Next_Click(object sender, RoutedEventArgs e)
        {
            NavigateToNextPage();
        }

        private void Button_Previous_Click(object sender, RoutedEventArgs e)
        {
            // Hide ProgressRing
            ProgressRing_AuthRing.IsActive = false;
            ProgressRing_AuthRing.Visibility = Visibility.Collapsed;
            EnableUI();

            Page currentPage = contentFrame.Content as Page;

            contentFrame.GoBack();

            if (currentPage is AccessTokenWizardMethodSelectionPage)
            {
                Button_Previous.IsEnabled = false;
                Button_Cancel.IsEnabled = false;
            }
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Hide ProgressRing
            ProgressRing_AuthRing.IsActive = false;
            ProgressRing_AuthRing.Visibility = Visibility.Collapsed;
            EnableUI();

            // Navigate to the welcome page
            contentFrame.Navigate(typeof(AccessTokenWizardWelcomePage));
            Button_Previous.IsEnabled = false;
            Button_Cancel.IsEnabled = false;
        }

        private async void NavigateToNextPage()
        {
            Page currentPage = contentFrame.Content as Page;

            if (currentPage is AccessTokenWizardWelcomePage)
            {
                // Welcome page
                // Navigate to the next page
                contentFrame.Navigate(typeof(AccessTokenWizardMethodSelectionPage));
                Button_Previous.IsEnabled = true;
                Button_Cancel.IsEnabled = true;
            }
            else if (currentPage is AccessTokenWizardMethodSelectionPage)
            {
                // Access token acquire method selection page
                contentFrame.Navigate((currentPage as AccessTokenWizardMethodSelectionPage).GetNextPageType());
            }
            else if (currentPage is AccessTokenWizardBuiltInPage)
            {
                // Built-in access token acquire method page
                // Acquire the access token using MSAL.

                // Show ProgressRing
                ProgressRing_AuthRing.IsActive = true;
                ProgressRing_AuthRing.Visibility = Visibility.Visible;
                DisableUI();

                var result = await (currentPage as AccessTokenWizardBuiltInPage).AcquireAccessTokenAsync();

                // Hide ProgressRing
                ProgressRing_AuthRing.IsActive = false;
                ProgressRing_AuthRing.Visibility = Visibility.Collapsed;
                EnableUI();

                if (result != null)
                {
                    // Access token was acquired.
                    EditorAccessToken.Instance.AuthenticationResult = result;

                    // Navigate to the main editor of main window
                    GraphEditorApplication.NavigateToMainEditor();
                    GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardContainer", "Message_AccessTokenAcquired"));
                }
            }
            else if (currentPage is AccessTokenWizardClientSecretPage)
            {
                // App only client secret page
                // Acquire the access token using MSAL.

                // Show ProgressRing
                ProgressRing_AuthRing.IsActive = true;
                ProgressRing_AuthRing.Visibility = Visibility.Visible;
                DisableUI();

                var result = await (currentPage as AccessTokenWizardClientSecretPage).AcquireAccessTokenAsync();

                // Hide ProgressRing
                ProgressRing_AuthRing.IsActive = false;
                ProgressRing_AuthRing.Visibility = Visibility.Collapsed;
                EnableUI();

                if (result != null)
                {
                    // Access token was acquired.
                    EditorAccessToken.Instance.AuthenticationResult = result;

                    // Navigate to the main editor of main window
                    GraphEditorApplication.NavigateToMainEditor();
                    GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardContainer", "Message_AccessTokenAcquired"));
                }
            }
        }

        private void DisableUI()
        {
            contentFrame.IsEnabled = false;
            Button_Next.IsEnabled = false;
        }

        private void EnableUI()
        {
            contentFrame.IsEnabled = true;
            Button_Next.IsEnabled = true;
        }
    }
}
