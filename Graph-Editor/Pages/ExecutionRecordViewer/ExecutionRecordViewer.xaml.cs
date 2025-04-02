using Graph_Editor.Data.ExecutionRecord;
using Graph_Editor.Logic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.ExecutionRecordViewer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ExecutionRecordViewer : Page
    {
        private ObservableCollection<ExecutionRecord> executionRecords { get; set; }

        public ExecutionRecordViewer()
        {
            this.InitializeComponent();

            executionRecords = new ObservableCollection<ExecutionRecord>(ExecutionRecordManager.ExecutionRecordList);
            ListView_ExecutionRecordListView.ItemsSource = executionRecords;

            executionRecords.CollectionChanged += ExecutionRecords_CollectionChanged;
            UpdateEmptyMessageVisibility();
        }

        private void ExecutionRecords_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateEmptyMessageVisibility();
        }

        private void Button_View_Click(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.NavigateToMainEditor((sender as Button)?.DataContext as ExecutionRecord);
        }


        private void Button_Rerun_Click(object sender, RoutedEventArgs e)
        {
            GraphEditorApplication.NavigateToMainEditor((sender as Button)?.DataContext as ExecutionRecord, true);
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ExecutionRecord executionRecord = button?.DataContext as ExecutionRecord;

            if (executionRecord != null)
            {
                var container = ListView_ExecutionRecordListView.ContainerFromItem(executionRecord) as ListViewItem;

                // Disable all buttons
                var buttons = FindVisualChildren<Button>(container);
                foreach (var buttonToBeDisabled in buttons)
                {
                    buttonToBeDisabled.IsEnabled = false;
                }

                // Start animation and remove the entry from the list
                var listViewItemGrid = FindVisualChild<Grid>(container);

                if (listViewItemGrid != null)
                {
                    // Start delete animation
                    var storyboard = (Storyboard)Resources["DeleteAnimation"];

                    // Stop the storyboard if it is running
                    storyboard.Stop();

                    Storyboard.SetTarget(storyboard, listViewItemGrid);

                    // Remove previous event handlers to avoid multiple calls
                    storyboard.Completed -= (s, a) => Storyboard_Completed(s, a, executionRecord);
                    storyboard.Completed += (s, a) => Storyboard_Completed(s, a, executionRecord);

                    storyboard.Begin();
                }
            }
        }

        private void Storyboard_Completed(object sender, object e, ExecutionRecord executionRecord)
        {
            if (executionRecord != null)
            {
                executionRecords.Remove(executionRecord);
                ExecutionRecordManager.SaveExecutionRecordList(new ExecutionRecordList(executionRecords));
            }
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                {
                    return tChild;
                }
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
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

        private void UpdateEmptyMessageVisibility()
        {
            TextBlock_EmptyMessage.Visibility = executionRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
