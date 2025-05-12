using Graph_Editor.Data.MicrosoftGraphScope;
using Microsoft.Identity.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.AccessTokenWizard
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AccessTokenWizardBuiltInPage : Page
    {
        private ObservableCollection<MicrosoftGraphScope> filteredScopes { get; set; }

        private bool userClickedListView = false; // Indicates whether the user clicked the ListView_Scopes.

        private List<MicrosoftGraphScope> userSelectedScopes = new List<MicrosoftGraphScope>(); // Cache the selected scopes while the user is using filter.

        public AccessTokenWizardBuiltInPage()
        {
            this.InitializeComponent();
            filteredScopes = new ObservableCollection<MicrosoftGraphScope>(MicrosoftGraphScope.WellKnownAndCustomScopes);
            ListView_Scopes.ItemsSource = filteredScopes;

            var lastSelectedScopes = GraphEditorApplication.GetSetting<string>(GraphEditorApplication.Settings.AccessTokenWizardBuiltInPage_LastSelectedScopes, string.Empty);

            // "lastSelectedScopes" is a space separated string that contains the last selected scopes. Convert it to a dictionary.
            var lastSelectedScopesDictionary = new Dictionary<string, string>();

            if (lastSelectedScopes != string.Empty)
            {
                foreach (var scope in lastSelectedScopes.Split(' '))
                {
                    lastSelectedScopesDictionary[scope] = scope;
                }

                foreach (var scope in filteredScopes)
                {
                    if (lastSelectedScopesDictionary.ContainsKey(scope.Name))
                    {
                        ListView_Scopes.SelectedItems.Add(scope);
                        userSelectedScopes.Add(scope);

                        StringBuilder scoepsString = new StringBuilder();

                        foreach (var selectedScope in userSelectedScopes)
                        {
                            scoepsString.Append(" " + selectedScope.Name);
                        }

                        TextBlock_Scopes.Text = scoepsString.ToString().Trim();
                    }
                }
            }
        }

        private void ListView_Scopes_ItemClick(object sender, ItemClickEventArgs e)
        {
            userClickedListView = true;
        }

        private void ListView_Scopes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the selected scopes based on the user's selection.

            if (userClickedListView)
            {
                foreach (var selectedScope in e.AddedItems)
                {
                    userSelectedScopes.Add(selectedScope as MicrosoftGraphScope);
                }

                foreach (var removedScope in e.RemovedItems)
                {
                    userSelectedScopes.Remove(removedScope as MicrosoftGraphScope);
                }

                StringBuilder scoepsString = new StringBuilder();

                foreach (var selectedScope in userSelectedScopes)
                {
                    scoepsString.Append(" " + selectedScope.Name);
                }

                TextBlock_Scopes.Text = scoepsString.ToString().Trim();

                userClickedListView = false;
            }
        }

        private void Button_UnelectAllScopes_Click(object sender, RoutedEventArgs e)
        {
            // Programmatically unselect all items in ListView_Scopes.
            userClickedListView = true;
            ListView_Scopes.SelectedItems.Clear();
        }


        private void Button_SelectDefaultScopes_Click(object sender, RoutedEventArgs e)
        {
            // Select scopes which IsDefaultScope is true, and unselect others.
            foreach (var scope in filteredScopes)
            {
                if (scope.IsDefaultScope)
                {
                    userClickedListView = true;
                    ListView_Scopes.SelectedItems.Add(scope);
                }
                else
                {
                    userClickedListView = true;
                    ListView_Scopes.SelectedItems.Remove(scope);
                }
            }
        }

        private void TextBox_ScopeFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Filter the items of ListView_Scopes based on the text.
            var filterText = (sender as TextBox).Text.ToLower();

            var filtered = MicrosoftGraphScope.WellKnownAndCustomScopes.Where(scope => scope.Name.Contains(filterText, StringComparison.InvariantCultureIgnoreCase));

            RemoveNonMatchingScope(filtered);
            AddBackScope(filtered);

            // Sort filteredScopes based on the Name property.
            filteredScopes = new ObservableCollection<MicrosoftGraphScope>(filteredScopes.OrderBy(scope => scope.Name));
            ListView_Scopes.ItemsSource = filteredScopes;

            // Restore the selected items
            foreach (var selectedItem in userSelectedScopes)
            {
                ListView_Scopes.SelectedItems.Add(selectedItem);
            }
        }

        private void RemoveNonMatchingScope(IEnumerable<MicrosoftGraphScope> filteredData)
        {
            for (int i = filteredScopes.Count - 1; i >= 0; i--)
            {
                var item = filteredScopes[i];
                // If scope is not in the filtered argument list, remove it from the ListView's source.
                if (!filteredData.Contains(item))
                {
                    filteredScopes.Remove(item);
                }
            }
        }

        private void AddBackScope(IEnumerable<MicrosoftGraphScope> filteredData)
        {
            foreach (var item in filteredData)
            {
                // If scope in filtered list is not currently in ListView's source collection, add it back in
                if (!filteredScopes.Contains(item))
                {
                    filteredScopes.Add(item);
                }
            }
        }

        public async Task<AuthenticationResult> AcquireAccessTokenAsync()
        {
            // Reset the InforBar.
            InforBar_AuthStatus.IsOpen = false;

            if (TextBlock_Scopes.Text.Trim() == string.Empty)
            {
                // Inform the user that no scope is selected using InforBar.
                InforBar_AuthStatus.Message = GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardBuiltInPage", "Message_NoScopeSelected");
                InforBar_AuthStatus.Severity = InfoBarSeverity.Warning;
                InforBar_AuthStatus.IsOpen = true;
                return null;
            }

            // Acquire the access token using MSAL.
            string[] scopes = TextBlock_Scopes.Text.Split(' ');

            var app = PublicClientApplicationBuilder.Create("16a9b02e-9150-49af-b17b-5d18dd79bf1c")
                .WithDefaultRedirectUri()
                .Build();

            AuthenticationResult result = null;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60 seconds timeout

            try
            {
                // Use the system browser for authentication.
                // We do not use .WithUseEmbeddedWebView(true) because it uses .NET Framework System.Windows.Forms which does not work well with .NET's PublishTrimmed.
                result = await app.AcquireTokenInteractive(scopes)
                    .WithSystemWebViewOptions(new SystemWebViewOptions())
                    .ExecuteAsync(cts.Token);

                // Save the selected scopes to the local settings.
                GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.AccessTokenWizardBuiltInPage_LastSelectedScopes, TextBlock_Scopes.Text);
            }
            catch (TaskCanceledException)
            {
                // Handle timeout
                InforBar_AuthStatus.Message = GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardBuiltInPage", "Message_Timeout");
                InforBar_AuthStatus.Severity = InfoBarSeverity.Error;
                InforBar_AuthStatus.IsOpen = true;
            }
            catch (MsalClientException ex)
            {
                if (ex.ErrorCode == "authentication_canceled")
                {
                    // Inform the user that the authentication was canceled using InforBar.
                    InforBar_AuthStatus.Message = GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardBuiltInPage", "Message_AuthenticationCanceled");
                    InforBar_AuthStatus.Severity = InfoBarSeverity.Warning;
                    InforBar_AuthStatus.IsOpen = true;
                }
                else
                {
                    // Handle other MSAL client exceptions
                    InforBar_AuthStatus.Message = $"{GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardBuiltInPage", "Message_UnexpectedMsalException")}: {ex.Message}";
                    InforBar_AuthStatus.Severity = InfoBarSeverity.Error;
                    InforBar_AuthStatus.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                // Handle non-MSAL exceptions
                InforBar_AuthStatus.Message = $"{GraphEditorApplication.GetResourceString("Pages.AccessTokenWizard.AccessTokenWizardBuiltInPage", "Message_UnexpectedException")}: {ex.Message}";
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
