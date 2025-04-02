using Graph_Editor.Data.ExecutionRecord;
using Graph_Editor.Data.SampleQuery;
using Graph_Editor.Pages.About;
using Graph_Editor.Pages.AccessTokenWizard;
using Graph_Editor.Pages.ExecutionRecordViewer;
using Graph_Editor.Pages.GlobalSetting;
using Graph_Editor.Pages.MainEditor;
using Microsoft.UI;
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
using Windows.Storage.Provider;
using Windows.UI.ViewManagement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor
{
    public sealed partial class MainWindow : Window
    {
        // Cache of pages of NavigationView
        private Dictionary<string, Page> pageCache = new();

        private UISettings uiSettings;

        public MainWindow()
        {
            this.InitializeComponent();

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Set the icon for the window
            if (appWindow != null)
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icon.ico");
                appWindow.SetIcon(iconPath);
            }

            // Initialize UISettings and subscribe to ColorValuesChanged event
            uiSettings = new UISettings();
            uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
        }

        private void UiSettings_ColorValuesChanged(UISettings sender, object args)
        {
            // Update the foreground color of all TextBlock elements on the UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var element in FindVisualChildren<TextBlock>(this.Content))
                {
                    var binding = element.GetBindingExpression(TextBlock.ForegroundProperty);
                    if (binding != null)
                    {
                        element.SetBinding(TextBlock.ForegroundProperty, binding.ParentBinding);
                    }
                }
            });
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.CodeActivated || args.WindowActivationState == WindowActivationState.PointerActivated)
            {
                // Remove event handler to run this code block only once.
                this.Activated -= Window_Activated;

                NavigateToMainEditor();
            }
        }

        private void MainNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                sender.Header = "";
                sender.AlwaysShowHeader = false;
                contentFrame.Navigate(typeof(GlobalSettingContainer));
            }
            else if (args.SelectedItem != null)
            {
                NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;
                if (selectedItem != null)
                {
                    string selectedItemTag = ((string)selectedItem.Tag);

                    switch (selectedItemTag)
                    {
                        case "MainEditor.MainEditorContainer":
                            sender.Header = "";
                            sender.AlwaysShowHeader = false;

                            // Navigate to the main editor page using the cache
                            string pageName = "Graph_Editor.Pages." + selectedItemTag;

                            if (!pageCache.TryGetValue(pageName, out Page page))
                            {
                                var pageType = Type.GetType(pageName);
                                page = (Page)Activator.CreateInstance(pageType);
                                pageCache[pageName] = page;
                            }

                            contentFrame.Content = page;

                            break;
                        case "ExecutionRecordViewer.ExecutionRecordViewer":
                            sender.Header = GraphEditorApplication.GetResourceString("MainWindow", "Message_ExecutionRecordViewerHeader");
                            sender.AlwaysShowHeader = true;

                            // Navigate to the access token wizard page without cache
                            contentFrame.Navigate(typeof(ExecutionRecordViewer));

                            break;
                        case "SampleQuery.SampleQueryContainer":
                            sender.Header = "";
                            sender.AlwaysShowHeader = false;

                            // Navigate to the sample query page using the cache
                            string sampleQueryPageName = "Graph_Editor.Pages." + selectedItemTag;

                            if (!pageCache.TryGetValue(sampleQueryPageName, out Page sampleQueryPage))
                            {
                                var pageType = Type.GetType(sampleQueryPageName);
                                page = (Page)Activator.CreateInstance(pageType);
                                pageCache[sampleQueryPageName] = page;
                            }

                            contentFrame.Content = pageCache[sampleQueryPageName];

                            break;
                        case "AccessTokenWizard.AccessTokenWizardContainer":
                            sender.Header = GraphEditorApplication.GetResourceString("MainWindow", "Message_AccessTokenWizardHeader");
                            sender.AlwaysShowHeader = true;

                            // Navigate to the access token wizard page without cache
                            contentFrame.Navigate(typeof(AccessTokenWizardContainer));

                            break;
                        case "About.AboutContainer":
                            sender.Header = GraphEditorApplication.GetResourceString("MainWindow", "Message_AboutHeader");
                            sender.AlwaysShowHeader = true;

                            // Navigate to the about page without cache
                            contentFrame.Navigate(typeof(AboutContainer));

                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public void UpdateStatusBarMainStatus(string Text)
        {
            TextBlock_MainStatus.Text = Text;
        }

        public void NavigateToMainEditor()
        {
            if (MainNavigation != null)
            {
                MainNavigation.SelectedItem = MainNavigation.MenuItems[0];
            }
        }

        internal void NavigateToMainEditor(ExecutionRecord Record, bool OnlyRequestToRerun = false)
        {
            // Get the editor page from the cache

            string pageName = "Graph_Editor.Pages.MainEditor.MainEditorContainer";

            if (!pageCache.TryGetValue(pageName, out Page page))
            {
                var pageType = Type.GetType(pageName);
                page = (Page)Activator.CreateInstance(pageType);
                pageCache[pageName] = page;
            }

            MainEditorContainer mainEditor = page as MainEditorContainer;
            mainEditor.LoadExecutionRecord(Record, OnlyRequestToRerun);

            NavigateToMainEditor();
        }

        internal void NavigateToMainEditor(SampleQueryItem SampleQuery)
        {
            // Get the editor page from the cache

            string pageName = "Graph_Editor.Pages.MainEditor.MainEditorContainer";

            if (!pageCache.TryGetValue(pageName, out Page page))
            {
                var pageType = Type.GetType(pageName);
                page = (Page)Activator.CreateInstance(pageType);
                pageCache[pageName] = page;
            }

            MainEditorContainer mainEditor = page as MainEditorContainer;
            mainEditor.LoadSampleQuery(SampleQuery);

            NavigateToMainEditor();
        }

        internal void NavigateToMainEditor(SampleQueryItem SampleQuery, string FileName, string Base64EncodedBody)
        {
            // Get the editor page from the cache

            string pageName = "Graph_Editor.Pages.MainEditor.MainEditorContainer";

            if (!pageCache.TryGetValue(pageName, out Page page))
            {
                var pageType = Type.GetType(pageName);
                page = (Page)Activator.CreateInstance(pageType);
                pageCache[pageName] = page;
            }

            MainEditorContainer mainEditor = page as MainEditorContainer;
            mainEditor.LoadSampleQuery(SampleQuery, FileName, Base64EncodedBody);

            NavigateToMainEditor();
        }

        public void NavigateToAccessTokenWizard()
        {
            if (MainNavigation != null)
            {
                MainNavigation.SelectedItem = MainNavigation.FooterMenuItems[0];
            }
        }
    }
}
