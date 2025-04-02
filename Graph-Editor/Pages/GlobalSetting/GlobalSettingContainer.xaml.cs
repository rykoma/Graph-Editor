using Graph_Editor.Pages.AccessTokenWizard;
using Graph_Editor.Pages.MainEditor;
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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.GlobalSetting
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GlobalSettingContainer : Page
    {
        // Cache of pages of NavigationView
        private Dictionary<string, Page> pageCache = [];

        public GlobalSettingContainer()
        {
            this.InitializeComponent();

            // Create cached pages.
            string[] globalSettingPages = new string[]
            {
                "GlobalSettingGeneralPage",
                "GlobalSettingCustomScopePage",
                "GlobalSettingAppLibraryPage"
            };

            foreach (string globalSettingPage in globalSettingPages)
            {
                string pageName = "Graph_Editor.Pages.GlobalSetting." + globalSettingPage;

                if (!pageCache.TryGetValue(globalSettingPage, out Page page))
                {
                    var pageType = Type.GetType(pageName);
                    page = (Page)Activator.CreateInstance(pageType);
                    pageCache[globalSettingPage] = page;
                }
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Show General page
            NavigationView_GlobalSetting.SelectedItem = NavigationView_GlobalSetting.MenuItems[0];
        }

        private void NavigationView_GlobalSetting_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                string selectedItemTag = ((string)selectedItem.Tag);
                string pageName = "Graph_Editor.Pages.GlobalSetting." + selectedItemTag;

                pageCache.TryGetValue(selectedItemTag, out Page page);

                Frame_GlobalSetting.Content = page;
            }
        }
    }
}
