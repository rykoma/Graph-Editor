using Graph_Editor.Data.ExecutionRecord;
using Graph_Editor.Data.SampleQuery;
using Graph_Editor.Logic;
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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Payments;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUIEditor;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.SampleQuery
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SampleQueryContainer : Page
    {
        private ObservableCollection<SampleQueryItem> sampleQueryDataSource;
        private ObservableCollection<SampleQueryItem> filteredSampleQueryDataSource;
        private ObservableCollection<HeaderItem> sampleHeaders;

        // The debounce timer for the filter text box
        private CancellationTokenSource _debounceTimer;

        public SampleQueryContainer()
        {
            this.InitializeComponent();

            sampleQueryDataSource = new ObservableCollection<SampleQueryItem>();

            var builtInSampleQuery = SampleQueryLoader.LoadBuiltInSampleQueryData();
            if (builtInSampleQuery != null)
            {
                sampleQueryDataSource.Add(builtInSampleQuery);
            }

#if DEBUG
            sampleQueryDataSource.Add(SampleQueryLoader.LoadCustomSampleQueryData());
#endif

            if (sampleQueryDataSource.Count == 0)
            {
                // No sample query data available
                sampleQueryDataSource.Add(new SampleQueryItem() { Name = "No sample query data available" });
                TreeView_SampleQuery.SelectionChanged -= TreeView_SampleQuery_SelectionChanged;
            }

            filteredSampleQueryDataSource = new ObservableCollection<SampleQueryItem>(sampleQueryDataSource);

            sampleHeaders = new ObservableCollection<HeaderItem>();
            ListView_HeaderListEditor.ItemsSource = sampleHeaders;
            ListView_HeaderListViewer.ItemsSource = sampleHeaders;

            CodeEditorControl_RequestBodyViewer.Language = "json";
            CodeEditorControl_RequestBodyViewer.Editor.EndAtLastLine = true;
            CodeEditorControl_RequestBodyEditor.Language = "json";
            CodeEditorControl_RequestBodyEditor.Editor.EndAtLastLine = true;
        }

        private void SaveCustomSampleQuery()
        {
            Dictionary<string, SampleQueryItem> dataToBeExported = new Dictionary<string, SampleQueryItem>();

            dataToBeExported.Add("BuiltInSampleQuery", CreateBuiltInSampleQuery(filteredSampleQueryDataSource[1]));
            dataToBeExported.Add("CustomSampleQuery", filteredSampleQueryDataSource[1]);

            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folderPath = Path.Join(documentsPath, "Graph Editor");

            // Create a backup of existing CustomSampleQuery.json file with a timestamp
            string originalCustomSampleQueryPath = Path.Join(folderPath, "CustomSampleQuery.json");
            if (File.Exists(originalCustomSampleQueryPath))
            {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string backupCustomSampleQueryPath = Path.Join(folderPath, "CustomSampleQuery_" + timestamp + ".json");
                File.Copy(originalCustomSampleQueryPath, backupCustomSampleQueryPath);
            }

            foreach (var exportData in dataToBeExported)
            {
                // Save as JSON file
                string path = Path.Join(folderPath, exportData.Key + ".json");
                var json = JsonSerializer.Serialize(exportData.Value, typeof(SampleQueryItem), SourceGenerationContext.Default);

                int maxRetries = 3;
                int delay = 1000; // in milliseconds
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        File.WriteAllText(path, json);
                        break; // Success, exit the loop
                    }
                    catch (IOException ex) when (attempt < maxRetries - 1)
                    {
                        // Log the exception if needed
                        System.Diagnostics.Debug.WriteLine($"Attempt {attempt + 1} failed: {ex.Message}");
                        System.Threading.Thread.Sleep(delay); // Wait before retrying
                    }
                }
            }
        }

        private SampleQueryItem CreateBuiltInSampleQuery(SampleQueryItem Source)
        {
            var result = new SampleQueryItem()
            {
                Id = Source.Id,
                Type = Source.Type,
                Name = Source.Name,
                Method = Source.Method,
                Url = Source.Url,
                Headers = new Dictionary<string, string>(),
                Body = Source.Body,
                BinaryBodyRequired = Source.BinaryBodyRequired,
                Children = new ObservableCollection<SampleQueryItem>(),
                IsExpanded = false,
                IsBuiltIn = true
            };

            if (result.Name == "Custom query")
            {
                // Root item
                result.Name = "v1.0";
            }

            if (Source.Headers != null)
            {
                foreach (var header in Source.Headers)
                {
                    result.Headers.Add(header.Key, header.Value);
                }
            }
            
            if (Source.Children != null)
            {
                foreach (var child in Source.Children)
                {
                    result.Children.Add(CreateBuiltInSampleQuery(child));
                }
            }

            return result;
        }
        


        private void TreeViewItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                // Toggle the IsExpanded property of the TreeViewItem
                item.IsExpanded = !item.IsExpanded;
            }
        }

        private void TreeView_SampleQuery_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        {
            HideSampleQueryAndContainer();

            // If selected item's SampleQueryType is Query, Show the sample query
            if (args.AddedItems.Count > 0)
            {
                var selectedItem = (SampleQueryItem)args.AddedItems[0];
                if (selectedItem.Type == SampleQueryItem.SampleQueryType.Query)
                {
                    ShowSampleQuery(selectedItem);
                }
                else if (selectedItem.Type == SampleQueryItem.SampleQueryType.Category)
                {
                    ShowSampleCategory(selectedItem);
                }
            }
        }

        private async void MenuFlyoutItem_AddContainer_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            var selectedItem = GetSelectedSampleQueryItem(sender as MenuFlyoutItem);

            if (selectedItem != null)
            {
                var newItem = new SampleQueryItem()
                {
                    Name = "New Container",
                    Type = SampleQueryItem.SampleQueryType.Category,
                    IsExpanded = true,
                    IsBuiltIn = false,
                    Children = { }
                };

                selectedItem.Children.Add(newItem);

                // Expand the parent container
                selectedItem.IsExpanded = true;
                var container = TreeView_SampleQuery.ContainerFromItem(selectedItem) as TreeViewItem;
                if (container != null)
                {
                    container.IsExpanded = true;
                }

                // Select the new container
                TreeView_SampleQuery.SelectedItem = newItem;
            }
        }

        private async void MenuFlyoutItem_AddQuery_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            var selectedItem = GetSelectedSampleQueryItem(sender as MenuFlyoutItem);

            if (selectedItem != null)
            {
                var newItem = new SampleQueryItem()
                {
                    Name = "New Query",
                    Type = SampleQueryItem.SampleQueryType.Query,
                    IsBuiltIn = false,
                    Method = "GET",
                    Url = "https://graph.microsoft.com/v1.0/me",
                    Headers = new Dictionary<string, string>(),
                    Body = "",
                    BinaryBodyRequired = false
                };

                selectedItem.Children.Add(newItem);

                // Expand the parent container
                selectedItem.IsExpanded = true;
                var container = TreeView_SampleQuery.ContainerFromItem(selectedItem) as TreeViewItem;
                if (container != null)
                {
                    container.IsExpanded = true;
                }

                // Select the new query
                TreeView_SampleQuery.SelectedItem = newItem;
            }
        }

        private async void MenuFlyoutItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            var selectedItem = GetSelectedSampleQueryItem(sender as MenuFlyoutItem);

            if (selectedItem != null)
            {
                // Find the parent of the selected item
                var parent = FindParent(filteredSampleQueryDataSource, selectedItem);

                if (parent != null)
                {
                    parent.Children.Remove(selectedItem);
                }
                else
                {
                    // Cannot remove the root item
                    // Show a content dialog
                    var contentDialog = new ContentDialog()
                    {
                        XamlRoot = this.XamlRoot,
                        Title = GraphEditorApplication.GetResourceString("Pages.SampleQuery.SampleQueryContainer", "Message_CannotRemoveRootTitle"),
                        Content = GraphEditorApplication.GetResourceString("Pages.SampleQuery.SampleQueryContainer", "Message_CannotRemoveRootContent"),
                        CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                    };
                    await contentDialog.ShowAsync();
                }
            }
        }

        private async void Button_Run_Click(object sender, RoutedEventArgs e)
        {
            // Run the selected SampleQueryItem

            if (TreeView_SampleQuery.SelectedItem == null)
            {
                HideSampleQueryAndContainer();
                GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.SampleQuery.SampleQueryContainer", "Message_SelectSampleQuery"));
                return;
            }

            // Test the selected SampleQueryItem
            var selectedItem = (SampleQueryItem)TreeView_SampleQuery.SelectedItem;
            if (selectedItem == null || !TestSampleQueryItem(selectedItem))
            {
                HideSampleQueryAndContainer();
                GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.SampleQuery.SampleQueryContainer", "Message_InvalidSampleQuery"));
                return;
            }

            if (selectedItem.BinaryBodyRequired)
            {
                // Show a confirmation dialog if the selected SampleQueryItem requires a binary body
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = GraphEditorApplication.GetResourceString("Pages.SampleQuery.SampleQueryContainer", "Message_BinaryBodyRequiredTitle"),
                    Content = GraphEditorApplication.GetResourceString("Pages.SampleQuery.SampleQueryContainer", "Message_BinaryBodyRequiredContent"),
                    PrimaryButtonText = GraphEditorApplication.DialogYesButtonText,
                    CloseButtonText = GraphEditorApplication.DialogNoButtonText
                };

                var result = await contentDialog.ShowAsync();

                if (result == ContentDialogResult.None)
                {
                    return;
                }

                // Create a file picker
                var openPicker = new Windows.Storage.Pickers.FileOpenPicker();

                // Retrieve the window handle (HWND) of the main window.
                var window = (Application.Current as App)?.MainWindowAccessor;
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

                // Initialize the file picker with the window handle (HWND).
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

                // Set options
                openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                openPicker.FileTypeFilter.Add("*");

                Windows.Storage.StorageFile file = await openPicker.PickSingleFileAsync();

                if (null == file)
                {
                    return;
                }

                // Open file and read it.
                var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)stream.Size);
                byte[] bytes = new byte[stream.Size];
                reader.ReadBytes(bytes);

                // Convert byte array to base64 string
                string base64String = Convert.ToBase64String(bytes);

                // Show the query in Editor
                try
                {
                    GraphEditorApplication.NavigateToMainEditor(selectedItem, file.Name, base64String);
                }
                catch (Exception ex)
                {
                    GraphEditorApplication.UpdateStatusBarMainStatus(ex.Message);
                }
            }
            else
            {
                // Show the query in Editor
                try
                {
                    GraphEditorApplication.NavigateToMainEditor(selectedItem);
                }
                catch (Exception ex)
                {
                    GraphEditorApplication.UpdateStatusBarMainStatus(ex.Message);
                }
            }
        }

        private async void Button_SaveSampleCategory_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = (SampleQueryItem)TreeView_SampleQuery.SelectedItem;
            if (selectedItem == null)
            {
                return;
            }

            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            if (TextBox_SampleCategoryName.Text == "Custom query")
            {
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot rename to the same name of the root container.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();
                return;
            }

            selectedItem.Name = TextBox_SampleCategoryName.Text;

            SaveCustomSampleQuery();

            // Get the selected SampleQueryItem and its parent
            var selectedHierarchy = GetSelectedSampleQueryItemTree();

            // Rebind the TreeView
            TreeView_SampleQuery.ItemsSource = null;
            TreeView_SampleQuery.ItemsSource = filteredSampleQueryDataSource;

            // Expand the selected tree if it exists
            var currentHierarchy = filteredSampleQueryDataSource;
            bool exitLoop = false;
            int depth = 1;

            do
            {
                var foundSampleQueryItem = FindAndExpandSampleQueryItemContainer(currentHierarchy, depth, selectedHierarchy, true);

                if (foundSampleQueryItem != null)
                {
                    // The selected hierarchy is found
                    currentHierarchy = foundSampleQueryItem.Children;
                    depth++;
                }
                else
                {
                    // The selected hierarchy is not found
                    exitLoop = true;
                }


            } while (exitLoop == false);
        }

        private async void Button_SaveSampleQuery_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = (SampleQueryItem)TreeView_SampleQuery.SelectedItem;
            if (selectedItem == null)
            {
                return;
            }

            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            if (TextBox_Name.Text == "Custom query")
            {
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot rename to the same name of the root container.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();
                return;
            }

            bool reloadTreeView = false;

            if (selectedItem.Name != TextBox_Name.Text)
            {
                selectedItem.Name = TextBox_Name.Text;
                reloadTreeView = true;
            }

            selectedItem.Method = ComboBox_Method.SelectedValue.ToString();

            selectedItem.Url = TextBox_Url.Text;

            selectedItem.Headers.Clear();
            foreach (var header in sampleHeaders)
            {
                if (!string.IsNullOrWhiteSpace(header.HeaderName))
                {
                    selectedItem.Headers.Add(header.HeaderName, header.Value);
                }
            }

            if (ToggleSwitch_SendBinary.IsOn)
            {
                selectedItem.BinaryBodyRequired = true;
                selectedItem.Body = "";
            }
            else
            {
                selectedItem.BinaryBodyRequired = false;
                selectedItem.Body = CodeEditorControl_RequestBodyEditor.Editor.GetText(CodeEditorControl_RequestBodyEditor.Editor.TextLength);
            }

            SaveCustomSampleQuery();

            if (reloadTreeView)
            {
                // Get the selected SampleQueryItem and its parent
                var selectedHierarchy = GetSelectedSampleQueryItemTree();

                // Rebind the TreeView
                TreeView_SampleQuery.ItemsSource = null;
                TreeView_SampleQuery.ItemsSource = filteredSampleQueryDataSource;

                // Expand the selected tree if it exists
                var currentHierarchy = filteredSampleQueryDataSource;
                bool exitLoop = false;
                int depth = 1;

                do
                {
                    var foundSampleQueryItem = FindAndExpandSampleQueryItemContainer(currentHierarchy, depth, selectedHierarchy, true);

                    if (foundSampleQueryItem != null)
                    {
                        // The selected hierarchy is found
                        currentHierarchy = foundSampleQueryItem.Children;
                        depth++;
                    }
                    else
                    {
                        // The selected hierarchy is not found
                        exitLoop = true;
                    }


                } while (exitLoop == false);
            }
        }

        private void Button_AddHeader_Click(object sender, RoutedEventArgs e)
        {
            sampleHeaders.Add(new HeaderItem { HeaderName = "", Value = "", IsReadOnly = false });
        }

        private void Button_EditHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is HeaderItem item)
            {
                if (item.IsReadOnly)
                {
                    // Change to edit mode
                    item.IsReadOnly = false;
                }
                else
                {
                    // Change to read only mode
                    item.IsReadOnly = true;
                }
            }
        }

        private void Button_DeleteHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is HeaderItem item)
            {
                sampleHeaders.Remove(item);
            }
        }

        private void ShowSampleQuery(SampleQueryItem SampleQuery)
        {
            // Show the selected SampleQueryItem in the UI

            if (SampleQuery.IsBuiltIn)
            {
                TextBlock_Name.Text = SampleQuery.Name;
                TextBlock_Method.Text = SampleQuery.Method;
                TextBlock_Url.Text = SampleQuery.Url;

                foreach (var header in SampleQuery.Headers)
                {
                    sampleHeaders.Add(new HeaderItem { HeaderName = header.Key, Value = header.Value, IsReadOnly = true });
                }

                if (sampleHeaders.Count == 0)
                {
                    Expandar_SampleQueryHeader.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Expandar_SampleQueryHeader.Visibility = Visibility.Visible;
                }

                if (SampleQuery.BinaryBodyRequired)
                {
                    CodeEditorControl_RequestBodyViewer.Editor.ReadOnly = false;
                    CodeEditorControl_RequestBodyViewer.Editor.SetText("");
                    CodeEditorControl_RequestBodyViewer.Editor.ReadOnly = true;
                }
                else
                {
                    CodeEditorControl_RequestBodyViewer.Editor.ReadOnly = false;
                    CodeEditorControl_RequestBodyViewer.Editor.SetText(SampleQuery.Body);
                    CodeEditorControl_RequestBodyViewer.Editor.ReadOnly = true;
                }

                Border_SampleQueryViewer.Visibility = Visibility.Visible;
                Border_SampleQueryEditor.Visibility = Visibility.Collapsed;
                Border_SampleCategoryEditor.Visibility = Visibility.Collapsed;
            }
            else
            {
                TextBox_Name.Text = SampleQuery.Name;

                // List all containers in the TreeView
                Dictionary<SampleQueryItem, int> containers;
                    
                if (filteredSampleQueryDataSource.Count == 1)
                {
                    containers = GetSubSampleCategory(filteredSampleQueryDataSource[0], 0);
                }
                else
                {
                    containers = GetSubSampleCategory(filteredSampleQueryDataSource[1], 0);
                }

                MenuFlyout_MoveSampleQueryLast.Items.Clear();
                MenuFlyout_MoveSampleCategoryFirst.Items.Clear();

                if (containers.Count == 0)
                {
                    DropDownButton_MoveSampleCategoryLast.IsEnabled = false;
                    DropDownButton_MoveSampleCategoryFirst.IsEnabled = false;
                }
                else
                {
                    DropDownButton_MoveSampleCategoryLast.IsEnabled = true;
                    DropDownButton_MoveSampleCategoryFirst.IsEnabled = true;

                    foreach (var container in containers)
                    {
                        var menuFlyoutItem_MoveSampleQueryLast = new MenuFlyoutItem()
                        {
                            DataContext = container.Key,
                            Text = (container.Key as SampleQueryItem).Name,
                            Icon = new FontIcon
                            {
                                Glyph = "\uED41" // Container icon
                            },
                            Margin = new Thickness(16 * container.Value, 0, 0, 0)
                        };
                        menuFlyoutItem_MoveSampleQueryLast.Click += MenuFlyoutItem_MoveSampleQueryLast_Click;
                        MenuFlyout_MoveSampleQueryLast.Items.Add(menuFlyoutItem_MoveSampleQueryLast);

                        var menuFlyoutItem_MoveSampleQueryFirst = new MenuFlyoutItem()
                        {
                            DataContext = container.Key,
                            Text = (container.Key as SampleQueryItem).Name,
                            Icon = new FontIcon
                            {
                                Glyph = "\uED41" // Container icon
                            },
                            Margin = new Thickness(16 * container.Value, 0, 0, 0)
                        };
                        menuFlyoutItem_MoveSampleQueryFirst.Click += MenuFlyoutItem_MoveSampleQueryFirst_Click;
                        MenuFlyout_MoveSampleQueryFirst.Items.Add(menuFlyoutItem_MoveSampleQueryFirst);
                    }
                }

                ComboBox_Method.SelectedValue = SampleQuery.Method;
                TextBox_Url.Text = SampleQuery.Url;

                sampleHeaders.Clear();

                foreach (var header in SampleQuery.Headers)
                {
                    sampleHeaders.Add(new HeaderItem { HeaderName = header.Key, Value = header.Value, IsReadOnly = true });
                }

                if (SampleQuery.BinaryBodyRequired)
                {
                    ToggleSwitch_SendBinary.IsOn = true;
                    CodeEditorControl_RequestBodyEditor.Editor.ReadOnly = false;
                    CodeEditorControl_RequestBodyEditor.Editor.SetText("");
                    CodeEditorControl_RequestBodyEditor.Editor.ReadOnly = true;
                }
                else
                {
                    ToggleSwitch_SendBinary.IsOn = false;
                    CodeEditorControl_RequestBodyEditor.Editor.ReadOnly = false;
                    CodeEditorControl_RequestBodyEditor.Editor.SetText(SampleQuery.Body);
                }

                Border_SampleQueryViewer.Visibility = Visibility.Collapsed;
                Border_SampleQueryEditor.Visibility = Visibility.Visible;
                Border_SampleCategoryEditor.Visibility = Visibility.Collapsed;
            }
        }

        private async void MenuFlyoutItem_MoveSampleQueryLast_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            var menuItem = sender as MenuFlyoutItem;
            if (menuItem != null)
            {
                var selectedItem = (SampleQueryItem)TreeView_SampleQuery.SelectedItem;

                // Find the parent of the selected item
                var parent = FindParent(filteredSampleQueryDataSource, selectedItem);
                if (parent != null)
                {
                    // Remove the selected item from the parent
                    parent.Children.Remove(selectedItem);
                }

                // Add the selected item to the target category
                var targetCategory = (SampleQueryItem)menuItem.DataContext;
                targetCategory.Children.Add(selectedItem);

                SaveCustomSampleQuery();
            }
        }

        private async void MenuFlyoutItem_MoveSampleQueryFirst_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            var menuItem = sender as MenuFlyoutItem;
            if (menuItem != null)
            {
                var selectedItem = (SampleQueryItem)TreeView_SampleQuery.SelectedItem;

                // Find the parent of the selected item
                var parent = FindParent(filteredSampleQueryDataSource, selectedItem);
                if (parent != null)
                {
                    // Remove the selected item from the parent
                    parent.Children.Remove(selectedItem);
                }

                // Add the selected item to the target category as the first child
                var targetCategory = (SampleQueryItem)menuItem.DataContext;
                targetCategory.Children.Insert(0, selectedItem);

                SaveCustomSampleQuery();
            }
        }

        private Dictionary<SampleQueryItem, int> GetSubSampleCategory(SampleQueryItem SampleCategory, int SubCategoryDepth, SampleQueryItem CategoryIgnored = null)
        {
            var result = new Dictionary<SampleQueryItem, int>();

            foreach (var item in SampleCategory.Children)
            {
                if (item.Type == SampleQueryItem.SampleQueryType.Category)
                {
                    if (item == CategoryIgnored)
                    {
                        continue;
                    }

                    result.Add(item, SubCategoryDepth);

                    var subCategories = GetSubSampleCategory(item, SubCategoryDepth + 1, CategoryIgnored);
                    foreach (var subCategory in subCategories)
                    {
                        result.Add(subCategory.Key, subCategory.Value);
                    }
                }
            }

            return result;
        }

        private void ShowSampleCategory(SampleQueryItem SampleCategory)
        {
            if (SampleCategory.IsBuiltIn)
            {
                return;
            }

            // Show the selected SampleQueryItem in the UI

            TextBox_SampleCategoryName.Text = SampleCategory.Name;

            // List all containers in the TreeView
            Dictionary<SampleQueryItem, int> containers;
            if (SampleCategory.Name != "Custom query")
            {
                containers = GetSubSampleCategory(filteredSampleQueryDataSource[1], 0, SampleCategory);
                Button_SaveSampleCategory.IsEnabled = true;
            }
            else
            {
                containers = new Dictionary<SampleQueryItem, int>();
                Button_SaveSampleCategory.IsEnabled = false;
            }

            MenuFlyout_MoveSampleCategoryLast.Items.Clear();
            MenuFlyout_MoveSampleCategoryFirst.Items.Clear();

            if (containers.Count == 0)
            {
                DropDownButton_MoveSampleCategoryLast.IsEnabled = false;
                DropDownButton_MoveSampleCategoryFirst.IsEnabled = false;
            }
            else
            {
                DropDownButton_MoveSampleCategoryLast.IsEnabled = true;
                DropDownButton_MoveSampleCategoryFirst.IsEnabled = true;

                foreach (var container in containers)
                {
                    var menuFlyoutItem_MoveSampleCategoryLast = new MenuFlyoutItem()
                    {
                        DataContext = container.Key,
                        Text = (container.Key as SampleQueryItem).Name,
                        Icon = new FontIcon
                        {
                            Glyph = "\uED41" // Container icon
                        },
                        Margin = new Thickness(16 * container.Value, 0, 0, 0)
                    };
                    menuFlyoutItem_MoveSampleCategoryLast.Click += MenuFlyoutItem_MoveSampleCategoryLast_Click;
                    MenuFlyout_MoveSampleCategoryLast.Items.Add(menuFlyoutItem_MoveSampleCategoryLast);

                    var menuFlyoutItem_MoveSampleCategoryFirst = new MenuFlyoutItem()
                    {
                        DataContext = container.Key,
                        Text = (container.Key as SampleQueryItem).Name,
                        Icon = new FontIcon
                        {
                            Glyph = "\uED41" // Container icon
                        },
                        Margin = new Thickness(16 * container.Value, 0, 0, 0)
                    };
                    menuFlyoutItem_MoveSampleCategoryFirst.Click += MenuFlyoutItem_MoveSampleCategoryFirst_Click;
                    MenuFlyout_MoveSampleCategoryFirst.Items.Add(menuFlyoutItem_MoveSampleCategoryFirst);
                }
            }

            Border_SampleQueryViewer.Visibility = Visibility.Collapsed;
            Border_SampleQueryEditor.Visibility = Visibility.Collapsed;
            Border_SampleCategoryEditor.Visibility = Visibility.Visible;
        }

        private async void MenuFlyoutItem_MoveSampleCategoryLast_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            var menuItem = sender as MenuFlyoutItem;
            if (menuItem != null)
            {
                var selectedItem = (SampleQueryItem)TreeView_SampleQuery.SelectedItem;

                // Find the parent of the selected item
                var parent = FindParent(filteredSampleQueryDataSource, selectedItem);
                if (parent != null)
                {
                    // Remove the selected item from the parent
                    parent.Children.Remove(selectedItem);
                }

                // Add the selected item to the target category
                var targetCategory = (SampleQueryItem)menuItem.DataContext;
                targetCategory.Children.Add(selectedItem);

                SaveCustomSampleQuery();
            }
        }

        private async void MenuFlyoutItem_MoveSampleCategoryFirst_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_Filter.Text != string.Empty)
            {
                // SampleQueryItem is filtered
                // Show an error dialog
                var contentDialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Error",
                    Content = "Cannot save sample query when using filter.",
                    CloseButtonText = GraphEditorApplication.DialogCloseButtonText
                };
                await contentDialog.ShowAsync();

                return;
            }

            var menuItem = sender as MenuFlyoutItem;
            if (menuItem != null)
            {
                var selectedItem = (SampleQueryItem)TreeView_SampleQuery.SelectedItem;

                // Find the parent of the selected item
                var parent = FindParent(filteredSampleQueryDataSource, selectedItem);
                if (parent != null)
                {
                    // Remove the selected item from the parent
                    parent.Children.Remove(selectedItem);
                }

                // Add the selected item to the target category as the first child
                var targetCategory = (SampleQueryItem)menuItem.DataContext;
                targetCategory.Children.Insert(0, selectedItem);

                SaveCustomSampleQuery();
            }
        }

        private void TextBox_Url_Paste(object sender, TextControlPasteEventArgs e)
        {   
            bool parseResult = ExecutionRecordManager.TryParseClipboardTextToRequestRecord(out RequestRecord clipboardRequest);

            if (parseResult)
            {
                // Successfully parsed clipboard text into RequestRecord
                // Set them to the corresponding UI elements
                ComboBox_Method.SelectedValue = clipboardRequest.Method;
                TextBox_Url.Text = clipboardRequest.Url.Replace("/users/{id}", "/users/${UserObjectId}").Replace("/users/{user-id}", "/users/${UserObjectId}");
                sampleHeaders.Clear();
                foreach (var header in clipboardRequest.Headers)
                {
                    sampleHeaders.Add(new HeaderItem { HeaderName = header.Key, Value = header.Value, IsReadOnly = false });
                }
                CodeEditorControl_RequestBodyEditor.Editor.SetText(clipboardRequest.Body);

                e.Handled = true; // Mark the event as handled
            }
            else
            {
                // Failed to parse clipboard text
                // Do nothing and allow the default paste behavior
            }
        }

        private void TextBox_Filter_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Debounce the filter text change event
            _debounceTimer?.Cancel();
            _debounceTimer = new CancellationTokenSource();
            var token = _debounceTimer.Token;
            
            // Delay for 500ms before applying the filter
            Task.Delay(500, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    // Apply the filter
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ApplyFilter();
                    });
                }
            }, token);
        }

        private void ApplyFilter()
        {
            // Filter the SampleQueryItem in the TreeView

            // Get the selected SampleQueryItem and its parent
            var selectedHierarchy = GetSelectedSampleQueryItemTree();

            var filter = TextBox_Filter.Text.ToLower();
            filteredSampleQueryDataSource.Clear();

            foreach (var item in FilterItems(sampleQueryDataSource, filter))
            {
                filteredSampleQueryDataSource.Add(item);
            }

            // Expand the selected tree if it exists
            if (filteredSampleQueryDataSource.Count() > 0 && selectedHierarchy.Count() > 0)
            {
                var currentHierarchy = filteredSampleQueryDataSource;
                bool exitLoop = false;
                int depth = 1;

                do
                {
                    var foundSampleQueryItem = FindAndExpandSampleQueryItemContainer(currentHierarchy, depth, selectedHierarchy);

                    if (foundSampleQueryItem != null)
                    {
                        // The selected hierarchy is found
                        currentHierarchy = foundSampleQueryItem.Children;
                        depth++;
                    }
                    else
                    {
                        // The selected hierarchy is not found
                        exitLoop = true;
                    }


                } while (exitLoop == false);
            }
        }

        private SampleQueryItem FindAndExpandSampleQueryItemContainer(ObservableCollection<SampleQueryItem> CurrentHierarchy, int Depth, List<string> SampleQueryItemIDsToBeExpanded, bool UseCustomQueryTree = false)
        {
            // Find the SampleQueryItem in CurrentHierarchy, and expand it
            // Return the SampleQueryItem if it is found in the CurrentHierarchy
            // Otherwise, return null

            bool selectedHierarchyFound = false;
            bool selectedItemFound = false;
            SampleQueryItem result = null;

            foreach (var container in CurrentHierarchy)
            {
                if (UseCustomQueryTree)
                {
                    if (container.Name == "v1.0")
                    {
                        // This is filteredSampleQueryDataSource[0], and it should be ignored, because it is not used in this scenario
                        continue;
                    }
                }
                else
                {
                    if (container.Name == "Custom query")
                    {
                        // This is filteredSampleQueryDataSource[1], and it should be ignored, because it exists only when the app is running with debug mode and SampleQueryItem.Id is the same as the built-in SampleQueryItem.Id
                        continue;
                    }
                }

                if (SampleQueryItemIDsToBeExpanded.Contains(container.Id.ToString()))
                {
                    selectedHierarchyFound = true;
                    result = container;

                    if (SampleQueryItemIDsToBeExpanded.First() == container.Id.ToString())
                    {
                        selectedItemFound = true;
                    }

                    ExpandSampleQueryContainer(container, Depth * 120, selectedItemFound);
                }

                if (selectedHierarchyFound)
                {
                    // If the selected hierarchy is found, break the loop
                    break;
                }
            }

            return result;
        }

        private void ExpandSampleQueryContainer(SampleQueryItem SampleQuery, int Delay, bool SelectExpandedSampleQuery = false)
        {
            // Expand the SampleQueryItem in the TreeView
            DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(Delay);
                SampleQuery.IsExpanded = true;
                
                var container = TreeView_SampleQuery.ContainerFromItem(SampleQuery) as TreeViewItem;
                if (container != null)
                {
                    container.IsExpanded = true;

                    if (SelectExpandedSampleQuery)
                    {
                        // Select the expanded SampleQueryItem
                        TreeView_SampleQuery.SelectedItem = SampleQuery;
                        // Scroll the selected item into view
                        container.StartBringIntoView();
                    }
                }
            });
        }

        private List<string> GetSelectedSampleQueryItemTree()
        {
            // Get the selected SampleQueryItem and its parent
            // Then get the selected SampleQueryItem's hierarchy to the root.

            List<string> result = new List<string>();
            var selectedItem = (SampleQueryItem)TreeView_SampleQuery.SelectedItem;

            if (selectedItem != null)
            {
                bool hasParent = false;

                do
                {
                    result.Add(selectedItem.Id.ToString());

                    var parent = FindParent(filteredSampleQueryDataSource, selectedItem);
                    if (parent == null)
                    {
                        hasParent = false;
                    }
                    else
                    {
                        selectedItem = parent;
                        hasParent = true;
                    }
                } while (hasParent == true);
            }

            return result;
        }

        private void HideSampleQueryAndContainer()
        {
            // Hide the SampleQueryItem in the UI

            Border_SampleQueryViewer.Visibility = Visibility.Collapsed;
            Border_SampleQueryEditor.Visibility = Visibility.Collapsed;
            Border_SampleCategoryEditor.Visibility = Visibility.Collapsed;

            TextBlock_Name.Text = string.Empty;
            TextBox_Name.Text = string.Empty;
            MenuFlyout_MoveSampleQueryLast.Items.Clear();
            MenuFlyout_MoveSampleCategoryLast.Items.Clear();
            MenuFlyout_MoveSampleQueryFirst.Items.Clear();
            MenuFlyout_MoveSampleCategoryFirst.Items.Clear();
            TextBlock_Method.Text = string.Empty;
            ComboBox_Method.SelectedValue = null;
            TextBlock_Url.Text = string.Empty;
            TextBox_Url.Text = string.Empty;
            sampleHeaders.Clear();
            ToggleSwitch_SendBinary.IsOn = false;
            CodeEditorControl_RequestBodyViewer.Editor.ReadOnly = false;
            CodeEditorControl_RequestBodyViewer.Editor.SetText("");
            CodeEditorControl_RequestBodyEditor.Editor.ReadOnly = false;
            CodeEditorControl_RequestBodyEditor.Editor.SetText("");
        }

        private bool TestSampleQueryItem(SampleQueryItem Query)
        {
            // Test the selected SampleQueryItem
            
            if (Query.Type != SampleQueryItem.SampleQueryType.Query)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Query.Name))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Query.Method) && (Query.Method != "GET" || Query.Method != "POST" || Query.Method != "PUT" || Query.Method != "PATCH" || Query.Method != "DELETE"))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Query.Url))
            {
                return false;
            }

            if (Query.Headers == null)
            {
                return false;
            }

            if (Query.Body == null)
            {
                return false;
            }

            return true;
        }

        private SampleQueryItem GetSelectedSampleQueryItem(MenuFlyoutItem Target)
        {
            SampleQueryItem result = null;

            var menuFlyoutItem = Target;
            if (menuFlyoutItem != null)
            {
                var selectedItem = (SampleQueryItem)menuFlyoutItem.DataContext;
                if (selectedItem != null)
                {
                    result = selectedItem;
                }
            }

            return result;
        }

        private SampleQueryItem FindParent(ObservableCollection<SampleQueryItem> items, SampleQueryItem target)
        {
            foreach (var item in items)
            {
                if (item.Children.Contains(target))
                {
                    return item;
                }

                var parent = FindParent(item.Children, target);
                if (parent != null)
                {
                    return parent;
                }
            }

            return null;
        }

        private IEnumerable<SampleQueryItem> FilterItems(IEnumerable<SampleQueryItem> Items, string Filter)
        {
            foreach (var item in Items)
            {
                if (item.Name.ToLower().Contains(Filter) || (item.Url != null && item.Url.ToLower().Contains(Filter)))
                {
                    // If the item matches the filter, add it to the result with all children

                    yield return new SampleQueryItem
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Type = item.Type,
                        Method = item.Method,
                        Url = item.Url,
                        Headers = item.Headers,
                        Body = item.Body,
                        BinaryBodyRequired = item.BinaryBodyRequired,
                        IsBuiltIn = item.IsBuiltIn,
                        IsExpanded = true,
                        Children = new ObservableCollection<SampleQueryItem>(item.Children)
                    };
                }
                else
                {
                    // If the item does not match the filter, add only the children that match the filter

                    var filteredChildren = FilterItems(item.Children, Filter).ToList();
                    if (filteredChildren.Any())
                    {
                        yield return new SampleQueryItem
                        {
                            Id = item.Id,
                            Name = item.Name,
                            Type = item.Type,
                            Method = item.Method,
                            Url = item.Url,
                            Headers = item.Headers,
                            Body = item.Body,
                            BinaryBodyRequired = item.BinaryBodyRequired,
                            IsBuiltIn = item.IsBuiltIn,
                            IsExpanded = true,
                            Children = new ObservableCollection<SampleQueryItem>(filteredChildren)
                        };
                    }
                }
            }
        }

        private string GetClipboardText()
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                return dataPackageView.GetTextAsync().AsTask().Result;
            }
            return string.Empty;
        }
    }
}
