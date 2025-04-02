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
    public sealed partial class AccessTokenWizardMethodSelectionPage : Page
    {
        public AccessTokenWizardMethodSelectionPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            RadioButtons_Method.SelectedIndex = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.AccessTokenWizardMethodSelectionPage__RadioButtons_Method__SelectedIndex, 0);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            GraphEditorApplication.SaveSetting(GraphEditorApplication.Settings.AccessTokenWizardMethodSelectionPage__RadioButtons_Method__SelectedIndex, RadioButtons_Method.SelectedIndex);
        }

        private void RadioButton_BuiltIn_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            (this.Parent as Frame).Navigate(GetNextPageType());
        }

        private void RadioButton_AppOnlySecret_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            (this.Parent as Frame).Navigate(GetNextPageType());

        }

        public Type GetNextPageType()
        {
            // Get the tag property of selected radio button.
            RadioButton selectedRadioButton = RadioButtons_Method.SelectedItem as RadioButton;
            string selectedMethod = "Graph_Editor.Pages.AccessTokenWizard." + selectedRadioButton.Tag;

            return Type.GetType(selectedMethod);
        }
    }
}
