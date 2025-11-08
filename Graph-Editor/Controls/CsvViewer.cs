using Graph_Editor.Pages.MainEditor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Controls
{
    public sealed partial class CsvViewer : StackPanel
    {
        public CsvViewer()
        {
            
        }

        public void LoadDataTable(DataTable DataSource, string LowCsv, string FileName)
        {
            // Remove all existing headers and rows
            Children.Clear();

            if (DataSource != null)
            {
                // Calculate max width per column
                var maxLengths = new int[DataSource.Columns.Count];

                for (int i = 0; i < DataSource.Columns.Count; i++)
                {
                    maxLengths[i] = DataSource.Columns[i].ColumnName.Length;
                }

                foreach (DataRow row in DataSource.Rows)
                {
                    for (int i = 0; i < DataSource.Columns.Count && i < row.ItemArray.Length; i++)
                    {
                        if (row[i].ToString().Length > maxLengths[i])
                            maxLengths[i] = row[i].ToString().Length;
                    }
                }

                var columnWidths = maxLengths.Select(len => new GridLength(len * 8 + 20)).ToList();

                // Create header row

                var headerGrid = new Grid { Margin = new Thickness(2) };

                for (int i = 0; i < DataSource.Columns.Count; i++)
                {
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = columnWidths[i] });

                    var border = new Border
                    {
                        BorderBrush = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"],
                        BorderThickness = new Thickness(0, 0, 1, 1)
                    };

                    var text = new TextBlock
                    {
                        Text = DataSource.Columns[i].ColumnName,
                        Margin = new Thickness(4),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold
                    };

                    border.Child = text;

                    Grid.SetColumn(border, i);
                    headerGrid.Children.Add(border);
                }

                Children.Add(headerGrid);

                // Create data rows

                foreach (DataRow row in DataSource.Rows)
                {
                    var rowGrid = new Grid { Margin = new Thickness(2) };
                    for (int i = 0; i < DataSource.Columns.Count; i++)
                    {
                        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = columnWidths[i] });

                        var border = new Border
                        {
                            BorderBrush = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"],
                            BorderThickness = new Thickness(0, 0, 1, 1)
                        };

                        string cellValue = i < row.ItemArray.Length ? row[i].ToString() : "";

                        var text = new TextBlock
                        {
                            Text = cellValue,
                            Margin = new Thickness(4),
                            ContextFlyout = CreateCellMenu(cellValue, row, DataSource, LowCsv, FileName)
                        };

                        border.Child = text;

                        Grid.SetColumn(border, i);
                        rowGrid.Children.Add(border);
                    }
                    Children.Add(rowGrid);
                }
            }
        }

        private static MenuFlyout CreateCellMenu(string SelectedCellValue, DataRow SelectedRow, DataTable EntireDataTable, string RowResponseBody, string FileName)
        {
            var menuFlyout_CsvCell = new MenuFlyout();
            {
                var menuFlyoutSubItem_copySubMenu = new MenuFlyoutSubItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutSubItem_copySubMenu_Text") };
                {
                    var menuFlyoutItem_copyCellValue = new MenuFlyoutItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutItem_copyCellValue_Text") };
                    menuFlyoutItem_copyCellValue.Click += (s, e) =>
                    {
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(SelectedCellValue);
                        Clipboard.SetContent(dataPackage);
                    };
                    menuFlyoutSubItem_copySubMenu.Items.Add(menuFlyoutItem_copyCellValue);
                }
                menuFlyout_CsvCell.Items.Add(menuFlyoutSubItem_copySubMenu);

                string[] columnNames = EntireDataTable.Columns.Cast<DataColumn>().Select(col => col.ColumnName).ToArray();
                string[] rowData = SelectedRow.ItemArray.Select(item => item.ToString()).ToArray();

                var menuFlyoutSubItem_copyRowSubMenu = new MenuFlyoutSubItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutSubItem_copyRowSubMenu_Text") };
                {
                    var menuFlyoutItem_copyRowAsCsv = new MenuFlyoutItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutItem_copyRowAsCsv_Text") };
                    menuFlyoutItem_copyRowAsCsv.Click += (s, e) =>
                    {
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(string.Join(",", rowData));
                        Clipboard.SetContent(dataPackage);
                    };
                    menuFlyoutSubItem_copyRowSubMenu.Items.Add(menuFlyoutItem_copyRowAsCsv);

                    var menuFlyoutItem_copyRowAsTsv = new MenuFlyoutItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutItem_copyRowAsTsv_Text") };
                    menuFlyoutItem_copyRowAsTsv.Click += (s, e) =>
                    {
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(string.Join("\t", rowData));
                        Clipboard.SetContent(dataPackage);
                    };
                    menuFlyoutSubItem_copyRowSubMenu.Items.Add(menuFlyoutItem_copyRowAsTsv);
                }
                menuFlyoutSubItem_copySubMenu.Items.Add(menuFlyoutSubItem_copyRowSubMenu);

                var menuFlyoutSubItem_copyHeaderAndRowSubMenu = new MenuFlyoutSubItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutSubItem_copyHeaderAndRowSubMenu_Text") };

                {
                    var menuFlyoutItem_copyHeaderAndRowAsCsv = new MenuFlyoutItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutItem_copyHeaderAndRowAsCsv_Text") };
                    menuFlyoutItem_copyHeaderAndRowAsCsv.Click += (s, e) =>
                    {
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(string.Join(",", columnNames) + System.Environment.NewLine + string.Join(",", rowData));
                        Clipboard.SetContent(dataPackage);
                    };
                    menuFlyoutSubItem_copyHeaderAndRowSubMenu.Items.Add(menuFlyoutItem_copyHeaderAndRowAsCsv);

                    var menuFlyoutItem_copyHeaderAndRowAsTsv = new MenuFlyoutItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutItem_copyHeaderAndRowAsTsv_Text") };
                    menuFlyoutItem_copyHeaderAndRowAsTsv.Click += (s, e) =>
                    {
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(string.Join("\t", columnNames) + System.Environment.NewLine + string.Join("\t", rowData));
                        Clipboard.SetContent(dataPackage);
                    };
                    menuFlyoutSubItem_copyHeaderAndRowSubMenu.Items.Add(menuFlyoutItem_copyHeaderAndRowAsTsv);
                }

                var menuFlyoutSubItem_copyEntireTableSubMenu = new MenuFlyoutSubItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutSubItem_copyEntireTableSubMenu_Text") };
                {
                    var menuFlyoutItem_copyEntireTableAsCsv = new MenuFlyoutItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutItem_copyEntireTableAsCsv_Text") };
                    menuFlyoutItem_copyEntireTableAsCsv.Click += (s, e) =>
                    {
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(RowResponseBody);
                        Clipboard.SetContent(dataPackage);
                    };
                    menuFlyoutSubItem_copyEntireTableSubMenu.Items.Add(menuFlyoutItem_copyEntireTableAsCsv);

                    var menuFlyoutItem_copyEntireTableAsTsv = new MenuFlyoutItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutItem_copyEntireTableAsTsv_Text") };
                    menuFlyoutItem_copyEntireTableAsTsv.Click += (s, e) =>
                    {
                        var stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine(string.Join("\t", columnNames));
                        foreach (DataRow row in EntireDataTable.Rows)
                        {
                            var rowValues = row.ItemArray.Select(item => item.ToString());
                            stringBuilder.AppendLine(string.Join("\t", rowValues));
                        }
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(stringBuilder.ToString());
                        Clipboard.SetContent(dataPackage);
                    };
                    menuFlyoutSubItem_copyEntireTableSubMenu.Items.Add(menuFlyoutItem_copyEntireTableAsTsv);
                }
                menuFlyoutSubItem_copySubMenu.Items.Add(menuFlyoutSubItem_copyEntireTableSubMenu);
            }

            {
                var menuFlyoutItem_saveAsCsv = new MenuFlyoutItem { Text = GraphEditorApplication.GetResourceString("Controls.CsvViewer", "menuFlyoutItem_saveAsCsv_Text") };
                menuFlyoutItem_saveAsCsv.Click += async (s, e) =>
                {
                    await SaveCsvFileAsync(RowResponseBody, FileName);
                };
                menuFlyout_CsvCell.Items.Add(menuFlyoutItem_saveAsCsv);
            }

            return menuFlyout_CsvCell;
        }

        public static async Task SaveCsvFileAsync(string CsvContent, string FileName)
        {
            // Create a file picker
            FileSavePicker savePicker = new FileSavePicker();

            // Retrieve the window handle (HWND) of the main window.
            var window = (Application.Current as App)?.MainWindowAccessor;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            // Set options
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            // Set the file type choices
            savePicker.FileTypeChoices.Add("CSV File", new List<string> { ".csv" });
            savePicker.SuggestedFileName = FileName;

            var saveFile = await savePicker.PickSaveFileAsync();
            if (null == saveFile)
            {
                return; // User cancelled the save operation
            }

            try
            {
                await Windows.Storage.FileIO.WriteTextAsync(saveFile, CsvContent);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that may occur during the save operation
                // Show ContentDialog as an error dialog
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to save the CSV file: {ex.Message}",
                    CloseButtonText = "Ok"
                };

                await dialog.ShowAsync();

                return;
            }

            GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Controls.CsvViewer", "Message_CsvSavedSuccessfully"));
        }
    }
}
