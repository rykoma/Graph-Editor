using Graph_Editor.Data.MicrosoftGraphScope;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.GlobalSetting
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GlobalSettingCustomScopePage : Page
    {
        private ObservableCollection<MicrosoftGraphScope> filteredScopes { get; set; }

        public GlobalSettingCustomScopePage()
        {
            this.InitializeComponent();

            filteredScopes = new ObservableCollection<MicrosoftGraphScope>(MicrosoftGraphScope.CustomScopes);
            ListView_Scopes.ItemsSource = filteredScopes;
        }

        private void TextBox_NewScope_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable the add button if the text box is not empty and not the text box text is just white space and not already in the list
            Button_Add.IsEnabled = !string.IsNullOrWhiteSpace(TextBox_NewScope.Text) && !filteredScopes.Any(scope => scope.Name == TextBox_NewScope.Text.Trim());
        }

        private void TextBox_NewScope_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                AddCustomScope();
            }
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            AddCustomScope();
        }

        private void Button_RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            // Remove all selected scopes from the list
            // Copy the selected scopes to a new list to avoid modifying the original list while iterating
            List<MicrosoftGraphScope> selectedScopes = new(ListView_Scopes.SelectedItems.Cast<MicrosoftGraphScope>());
            foreach (MicrosoftGraphScope scope in selectedScopes)
            {
                filteredScopes.Remove(scope);
            }

            // Save the new scope list to the settings
            MicrosoftGraphScope.SaveCustomScopes(new List<MicrosoftGraphScope>(filteredScopes));
        }

        private void Button_RemoveNotRequired_Click(object sender, RoutedEventArgs e)
        {
            // Remove all scopes that are already in the well-known scopes list from the list
            List<MicrosoftGraphScope> notRequiredScopes = new();
            foreach (MicrosoftGraphScope scope in filteredScopes)
            {
                if (MicrosoftGraphScope.WellKnownScopes.Any(wellKnownScope => wellKnownScope.Name == scope.Name))
                {
                    notRequiredScopes.Add(scope);
                }
            }

            foreach (MicrosoftGraphScope scope in notRequiredScopes)
            {
                filteredScopes.Remove(scope);
            }

            // Save the new scope list to the settings
            MicrosoftGraphScope.SaveCustomScopes(new List<MicrosoftGraphScope>(filteredScopes));
        }

        private void Button_RemoveAll_Click(object sender, RoutedEventArgs e)
        {
            // Remove all scopes from the list
            filteredScopes.Clear();

            // Save the new scope list to the settings
            MicrosoftGraphScope.SaveCustomScopes(new List<MicrosoftGraphScope>(filteredScopes));
        }

        private void AddCustomScope()
        {
            if (!Button_Add.IsEnabled)
            {
                return;
            }

            // Add the new scope to the list
            filteredScopes.Add(new MicrosoftGraphScope(TextBox_NewScope.Text.Trim(), false, true));
            filteredScopes = new ObservableCollection<MicrosoftGraphScope>(filteredScopes.OrderBy(scope => scope.Name));
            ListView_Scopes.ItemsSource = filteredScopes;

            // Save the new scope list to the settings
            MicrosoftGraphScope.SaveCustomScopes(new List<MicrosoftGraphScope>(filteredScopes));

            // Clear the text box
            TextBox_NewScope.Text = "";
        }
    }
}
