using Graph_Editor.Data.EditorAccessToken;
using Graph_Editor.Data.ExecutionRecord;
using Graph_Editor.Data.MainEditorResponse;
using Graph_Editor.Data.SampleQuery;
using Graph_Editor.Logic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using static Graph_Editor.Data.ExecutionRecord.ResponseRecord;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.MainEditor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainEditorContainer : Page
    {
        // Cache of pages of NavigationView
        private Dictionary<string, Page> pageCache = [];

        private MainEditorHttpClient mainEditorHttpClient = new();

        private bool toggleSwithch_SendBinary_ToggledByCode = false;

        private List<string> preNormalizedSampleUrls;

        public MainEditorContainer()
        {
            this.InitializeComponent();

            // Create cached pages.
            string[] mainEditorPages = new string[]
            {
                "MainEditorRequestHeader",
                "MainEditorRequestBody",
                "MainEditorResponseHeader",
                "MainEditorResponseBody"
            };

            foreach (string mainEditorPage in mainEditorPages)
            {
                string pageName = "Graph_Editor.Pages.MainEditor." + mainEditorPage;

                if (!pageCache.TryGetValue(mainEditorPage, out Page page))
                {
                    var pageType = Type.GetType(pageName);
                    page = (Page)Activator.CreateInstance(pageType);
                    pageCache[mainEditorPage] = page;
                }
            }

            // Show the last execution record
            if (ExecutionRecordManager.ExecutionRecordList.Count > 0)
            {
                LoadExecutionRecord(ExecutionRecordManager.ExecutionRecordList[0]);
            }

            // Show a tips in the InfoBar
            string tipsResourceName = GraphEditorApplication.GetRandomTipsResourceName();

            if (!string.IsNullOrEmpty(tipsResourceName))
            {
                string tipsString = GraphEditorApplication.GetResourceString("TipsString", tipsResourceName);
                string linkText = GraphEditorApplication.GetResourceString("TipsString", tipsResourceName + "_LinkText");
                string linkUrl = GraphEditorApplication.GetResourceString("TipsString", tipsResourceName + "_LinkUrl");

                if (!string.IsNullOrEmpty(tipsString))
                {
                    CloseInfoBarTop();

                    if (!string.IsNullOrEmpty(linkText) && !string.IsNullOrEmpty(linkUrl))
                    {
                        OpenInfoBarTop(InfoBarSeverity.Informational, "Tips", tipsString, linkText, linkUrl);
                    }
                    else
                    {
                        OpenInfoBarTop(InfoBarSeverity.Informational, "Tips", tipsString);
                    }
                }

            }

            preNormalizedSampleUrls = SampleQueryLoader.SampleUrls(SampleQueryLoader.LoadBuiltInSampleQueryData());

            GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Resources", "Message_Ready"));

            AutoSuggestBox_RequestUrl.Focus(FocusState.Pointer);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Show Body tabs.
            NavigationView_Request.SelectedItem = NavigationView_Request.MenuItems[1];
            NavigationView_Response.SelectedItem = NavigationView_Response.MenuItems[1];
        }

        private void NavigationView_Request_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                string selectedItemTag = ((string)selectedItem.Tag);
                string pageName = "Graph_Editor.Pages.MainEditor." + selectedItemTag;

                pageCache.TryGetValue(selectedItemTag, out Page page);

                Frame_Request.Content = page;

                if (Frame_Request.Content is MainEditorRequestHeader mainEditorRequestHeader)
                {
                    mainEditorRequestHeader.MakeAllHeadersReadOnly();
                }
                else if (Frame_Request.Content is MainEditorRequestBody mainEditorRequestBody)
                {
                    // Check Content-Type header value

                    pageCache.TryGetValue("MainEditorRequestHeader", out Page mainEditorRequestHeaderCache);
                    string contentTypeHeaderValue = (mainEditorRequestHeaderCache as MainEditorRequestHeader).ContentTypeHeaderValue;

                    if (ToggleSwitch_SendBinary.IsOn)
                    {
                        mainEditorRequestBody.Body.HighlightingLanguage = "plaintext";
                    }
                    else if (contentTypeHeaderValue != null)
                    {
                        // Change HighlightingLanguage of mainEditorRequestBody

                        contentTypeHeaderValue = contentTypeHeaderValue.ToLower();

                        if (contentTypeHeaderValue.Contains("application/json"))
                        {
                            mainEditorRequestBody.Body.HighlightingLanguage = "json";
                        }
                        else if (contentTypeHeaderValue.Contains("text/plain"))
                        {
                            mainEditorRequestBody.Body.HighlightingLanguage = "plaintext";
                        }
                        else if (contentTypeHeaderValue.StartsWith("image"))
                        {
                            mainEditorRequestBody.Body.HighlightingLanguage = "plaintext";
                        }
                        else
                        {
                            mainEditorRequestBody.Body.HighlightingLanguage = "json";
                        }
                    }
                    else
                    {
                        mainEditorRequestBody.Body.HighlightingLanguage = "json";
                    }
                }
            }
        }

        private async void ToggleSwitch_SendBinary_Toggled(object sender, RoutedEventArgs e)
        {
            if (toggleSwithch_SendBinary_ToggledByCode)
            {
                // If ToggleSwitch is toggled by code, do nothing.
            }
            else
            {
                // If ToggleSwitch is toggled by user action, show a open file dialog.
                if (ToggleSwitch_SendBinary.IsOn)
                {
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

                    if (file != null)
                    {
                        // Open file and read it.
                        var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                        var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
                        await reader.LoadAsync((uint)stream.Size);
                        byte[] bytes = new byte[stream.Size];
                        reader.ReadBytes(bytes);

                        // Convert byte array to base64 string
                        string base64String = Convert.ToBase64String(bytes);

                        // Show the file name
                        TextBlock_BodyFileNameHeader.Visibility = Visibility.Visible;
                        TextBlock_BodyFileNameValue.Visibility = Visibility.Visible;
                        TextBlock_BodyFileNameValue.Text = file.Name;

                        // Set the base64 string to MainEditorRequestBody
                        if (pageCache.TryGetValue("MainEditorRequestBody", out Page requestBodyPage))
                        {
                            MainEditorRequestBody mainEditorRequestBody = requestBodyPage as MainEditorRequestBody;
                            if (mainEditorRequestBody != null)
                            {
                                mainEditorRequestBody.Body.HighlightingLanguage = "plaintext";
                                mainEditorRequestBody.Body.Editor.SetText(base64String);
                                mainEditorRequestBody.Body.Editor.ReadOnly = true;
                                mainEditorRequestBody.Body.Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray);
                            }
                        }
                    }
                    else
                    {
                        ToggleSwitch_SendBinary.IsOn = false;
                    }
                }
                else
                {
                    // If ToggleSwitch is not toggled, clear the text of MainEditorRequestBody

                    // Hide the file name
                    TextBlock_BodyFileNameHeader.Visibility = Visibility.Collapsed;
                    TextBlock_BodyFileNameValue.Visibility = Visibility.Collapsed;
                    TextBlock_BodyFileNameValue.Text = String.Empty;

                    if (pageCache.TryGetValue("MainEditorRequestBody", out Page requestBodyPage))
                    {
                        MainEditorRequestBody mainEditorRequestBody = requestBodyPage as MainEditorRequestBody;
                        if (mainEditorRequestBody != null)
                        {
                            mainEditorRequestBody.Body.Editor.ReadOnly = false;
                            mainEditorRequestBody.Body.Editor.SetText("");
                            mainEditorRequestBody.Body.HighlightingLanguage = "json";
                            mainEditorRequestBody.Body.Background = new SolidColorBrush(Microsoft.UI.Colors.White);
                        }
                    }
                }
            }

            toggleSwithch_SendBinary_ToggledByCode = false;
        }

        private void NavigationView_Response_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                string selectedItemTag = ((string)selectedItem.Tag);
                string pageName = "Graph_Editor.Pages.MainEditor." + selectedItemTag;

                if (!pageCache.TryGetValue(selectedItemTag, out Page page))
                {
                    var pageType = Type.GetType(pageName);
                    page = (Page)Activator.CreateInstance(pageType);
                    pageCache[selectedItemTag] = page;
                }

                Frame_Response.Content = page;

                if (Frame_Response.Content is MainEditorResponseHeader mainEditorResponseHeader)
                {
                    // If we need to show something, write it here.
                }
                else if (Frame_Response.Content is MainEditorResponseBody mainEditorResponseBody)
                {
                    // If we need to show something, write it here.
                }
            }
        }

        private void MenuFlyoutItem_InfoBar_ResponseTop_CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            // Copy the URL to the clipboard

            if (InfoBar_ResponseTop.Tag != null && InfoBar_ResponseTop.Tag is ExecutionRecord executionRecord)
            {
                var url = executionRecord.Request.Url;
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(url);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }

            // Update status bar
            GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_CopiedUrlToClipboard"));
        }

        private void MenuFlyoutItem_InfoBar_ResponseTop_CopySimpleSummary_Click(object sender, RoutedEventArgs e)
        {
            // Copy the method, URL, and status code to the clipboard

            if (InfoBar_ResponseTop.Tag != null && InfoBar_ResponseTop.Tag is ExecutionRecord executionRecord)
            {
                var summary = executionRecord.CreateSimpleSummary();
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(summary);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }

            // Update status bar
            GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_CopiedSimpleSummaryToClipboard"));
        }

        private void MenuFlyoutItem_InfoBar_ResponseTop_CopyFullDetails_Click(object sender, RoutedEventArgs e)
        {
            // Copy the full details to the clipboard

            if (InfoBar_ResponseTop.Tag != null && InfoBar_ResponseTop.Tag is ExecutionRecord executionRecord)
            {
                var details = executionRecord.CreateFullDetails();
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(details);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }

            // Update status bar
            GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_CopiedFullDetailsToClipboard"));
        }

        private void Button_Run_Click(object sender, RoutedEventArgs e)
        {
            ExecureRequest();
        }

        private async void ExecureRequest()
        {
            // Close InfoBar
            CloseInfoBarTop();
            CloseInfoBarResponseTop();

            if (Data.EditorAccessToken.EditorAccessToken.Instance.AuthenticationResult == null)
            {
                // User does not have access token

                ContentDialog contentDialogAccessTokenCheck = new ContentDialog();

                contentDialogAccessTokenCheck.XamlRoot = this.XamlRoot;
                contentDialogAccessTokenCheck.Title = GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_AccessTokenMissingDialogTitle");
                contentDialogAccessTokenCheck.PrimaryButtonText = GraphEditorApplication.DialogYesButtonText;
                contentDialogAccessTokenCheck.SecondaryButtonText = "";
                contentDialogAccessTokenCheck.CloseButtonText = GraphEditorApplication.DialogNoButtonText;
                contentDialogAccessTokenCheck.DefaultButton = ContentDialogButton.Primary;
                contentDialogAccessTokenCheck.Content = new TextBlock() { Text = GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_AccessTokenMissingDialogContent") };

                var AccessTokenCheckResult = await contentDialogAccessTokenCheck.ShowAsync();

                if (AccessTokenCheckResult == ContentDialogResult.Primary)
                {
                    GraphEditorApplication.NavigateToAccessTokenWizard();
                }

                return;
            }

            if (RequestSendingConfirmationDialogRequired() == true)
            {
                // Show a dialog and check if the user wants to continue
                ContentDialog contentDialogRequestSendingConfirmation = new ContentDialog();
                contentDialogRequestSendingConfirmation.XamlRoot = this.XamlRoot;
                contentDialogRequestSendingConfirmation.Title = GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestSendingConfirmationDialogTitle");
                contentDialogRequestSendingConfirmation.PrimaryButtonText = GraphEditorApplication.DialogYesButtonText;
                contentDialogRequestSendingConfirmation.SecondaryButtonText = "";
                contentDialogRequestSendingConfirmation.CloseButtonText = GraphEditorApplication.DialogNoButtonText;
                contentDialogRequestSendingConfirmation.DefaultButton = ContentDialogButton.Close;
                contentDialogRequestSendingConfirmation.Content = new TextBlock() { Text = GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestSendingConfirmationDialogContent") };

                var requestSendingConfirmationResult = await contentDialogRequestSendingConfirmation.ShowAsync();

                if (requestSendingConfirmationResult != ContentDialogResult.Primary)
                {
                    // User does not want to continue
                    return;
                }
            }

            DateTime requestStartTime = DateTime.UtcNow;

            // Get request URL
            string requestUrl = AutoSuggestBox_RequestUrl.Text;

            // Get request headers

            Dictionary<string, string> headers = null;

            if (pageCache.TryGetValue("MainEditorRequestHeader", out Page requestHeaderPage))
            {
                MainEditorRequestHeader mainEditorRequestHeader = requestHeaderPage as MainEditorRequestHeader;
                if (mainEditorRequestHeader != null)
                {
                    mainEditorRequestHeader.MakeAllHeadersReadOnly();
                    headers = mainEditorRequestHeader.GetAllHeader();
                }
            }

            // Get request body

            string bodyString = "";
            Stream bodyStream = null;

            if (pageCache.TryGetValue("MainEditorRequestBody", out Page requestBodyPage))
            {
                MainEditorRequestBody mainEditorRequestBody = requestBodyPage as MainEditorRequestBody;
                if (mainEditorRequestBody != null)
                {
                    bodyString = mainEditorRequestBody.Body.Editor.GetText(mainEditorRequestBody.Body.Editor.TextLength);
                }
            }

            if (ToggleSwitch_SendBinary.IsOn)
            {
                // Convert base64 string to byte array
                byte[] bytes = Convert.FromBase64String(bodyString);
                bodyStream = new MemoryStream(bytes);
            }

            MainEditorHttpResponseMessage response = null;

            if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                response = await mainEditorHttpClient.SendGetRequestAsync(requestUrl, headers);
            }
            else if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                if (ToggleSwitch_SendBinary.IsOn)
                {
                    response = await mainEditorHttpClient.SendPostRequestAsync(requestUrl, headers, bodyStream);
                }
                else
                {
                    response = await mainEditorHttpClient.SendPostRequestAsync(requestUrl, headers, bodyString);
                }
            }
            else if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                if (ToggleSwitch_SendBinary.IsOn)
                {
                    response = await mainEditorHttpClient.SendPutRequestAsync(requestUrl, headers, bodyStream);
                }
                else
                {
                    response = await mainEditorHttpClient.SendPutRequestAsync(requestUrl, headers, bodyString);
                }
            }
            else if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("PATCH", StringComparison.OrdinalIgnoreCase))
            {
                if (ToggleSwitch_SendBinary.IsOn)
                {
                    response = await mainEditorHttpClient.SendPatchRequestAsync(requestUrl, headers, bodyStream);
                }
                else
                {
                    response = await mainEditorHttpClient.SendPatchRequestAsync(requestUrl, headers, bodyString);
                }
            }
            else if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                response = await mainEditorHttpClient.SendDeleteRequestAsync(requestUrl, headers);
            }

            DateTime requestEndTime = DateTime.UtcNow;

            if (response == null)
            {
                // Something went wrong
                // Open InfoBar and show error
                OpenInfoBarTop(InfoBarSeverity.Error, "Error", GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestFailed"));
                GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestFailed"));
            }
            else if (!response.RequestSucceeded)
            {
                // Open InfoBar and show error
                OpenInfoBarTop(InfoBarSeverity.Error, "Error", response.InternalMessage);
                GraphEditorApplication.UpdateStatusBarMainStatus(response.InternalMessage);
            }
            else
            {
                // Request succeeded
                // Create an execution record

                ExecutionRecord executionRecord = new ExecutionRecord()
                {
                    Request = new RequestRecord()
                    {
                        DateTime = requestStartTime,
                        Method = ComboBox_RequestMethod.SelectedValue.ToString(),
                        Url = requestUrl,
                        Headers = headers,
                        IsBinaryBody = ToggleSwitch_SendBinary.IsOn,
                        Body = bodyString,
                        FileName = TextBlock_BodyFileNameValue.Text
                    },
                    Response = new ResponseRecord()
                    {
                        DateTime = requestEndTime,
                        StatusCode = response.StatusCode,
                        Headers = response.GetHeaderDictionary(),
                        ContentType = response.ContentType,
                        BodyString = response.ParsedResponseBodyString,
                        Base64EncodedBinaryBody = response.ContentType == "application/json" ? "" : response.ParsedBase64BodyString
                    }
                };

                // Show response in MainEditorResponseBody
                await ShowResponseAsync(executionRecord);

                // Save the request and response to the execution record list
                try
                {
                    ExecutionRecordManager.AddExecutionRecord(executionRecord);
                }
                catch (Exception ex)
                {
                    // Open InfoBar and show warning
                    OpenInfoBarTop(InfoBarSeverity.Warning, "Warning", GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_ExecutionRecordSaveFailed") + ex.Message);
                }

                NavigationView_Response.SelectedItem = NavigationView_Response.MenuItems[1];
            }
        }

        private bool RequestSendingConfirmationDialogRequired()
        {
            // Return true if the request sending confirmation setting is enabled

            if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_GET, false);
            }

            if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_POST, false);
            }

            if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("PUT", StringComparison.OrdinalIgnoreCase))
            {
                return GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_PUT, false);
            }

            if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("PATCH", StringComparison.OrdinalIgnoreCase))
            {
                return GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_PATCH, false);
            }

            if (ComboBox_RequestMethod.SelectedValue.ToString().Equals("DELETE", StringComparison.OrdinalIgnoreCase))
            {
                return GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestSendingConfirmation_DELETE, true);
            }

            // Default to true if the method is not recognized
            return true;
        }

        private async Task ShowResponseAsync(ExecutionRecord executionRecord)
        {
            ResponseRecord responseRecord = executionRecord.Response;

            string header = responseRecord.Headers.Aggregate("", (current, header) => current + header.Key + ": " + header.Value + "\n");
            string bodyString = responseRecord.BodyString;
            ResponseBodyDisplayMode bodyDisplayMode = responseRecord.DisplayMode;
            HttpStatusCode StatusCode = responseRecord.StatusCode;

            MainEditorResponseHeader mainEditorResponseHeader = pageCache["MainEditorResponseHeader"] as MainEditorResponseHeader;
            mainEditorResponseHeader.Header.Text = header;

            MainEditorResponseBody mainEditorResponseBody = pageCache["MainEditorResponseBody"] as MainEditorResponseBody;
            mainEditorResponseBody.HideAllViewer();

            switch (bodyDisplayMode)
            {
                case ResponseBodyDisplayMode.Json:
                    mainEditorResponseBody.TextResponseViewer.Editor.ReadOnly = false;
                    mainEditorResponseBody.TextResponseViewer.HighlightingLanguage = "json";

                    if (GraphEditorApplication.TryParseJson(bodyString, out string parsedJsonStringResult))
                    {
                        mainEditorResponseBody.TextResponseViewer.Editor.SetText(GraphEditorApplication.RemoveProblematicCharacters(parsedJsonStringResult));
                        GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestComplete"));
                    }
                    else
                    {
                        mainEditorResponseBody.TextResponseViewer.Editor.SetText(GraphEditorApplication.RemoveProblematicCharacters(bodyString));
                        GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestCompleteWithJsonParseError"));
                    }

                    mainEditorResponseBody.TextResponseViewer.Visibility = Visibility.Visible;
                    mainEditorResponseBody.TextResponseViewer.Editor.ReadOnly = true;

                    break;
                case ResponseBodyDisplayMode.Image:
                    Stream bodyStream = new MemoryStream(Convert.FromBase64String(responseRecord.Base64EncodedBinaryBody));
                    BitmapImage bodyBitmapImage = new BitmapImage();

                    try
                    {
                        await bodyBitmapImage.SetSourceAsync(bodyStream.AsRandomAccessStream());
                    }
                    catch
                    {
                        bodyBitmapImage = null;
                    }

                    mainEditorResponseBody.ImageResponseViewerContent.Source = bodyBitmapImage;
                    mainEditorResponseBody.ImageResponseViewerContent.Tag = responseRecord;
                    mainEditorResponseBody.ImageResponseViewer.Visibility = Visibility.Visible;
                    GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestComplete"));
                    break;
                case ResponseBodyDisplayMode.Csv:
                    string contentDisposition = responseRecord.Headers.GetValueOrDefault("Content-Disposition", string.Empty);
                    string fileName = string.IsNullOrEmpty(contentDisposition) ? "data.csv" : contentDisposition.Split(';').Select(part => part.Trim()).FirstOrDefault(part => part.StartsWith("filename=", StringComparison.OrdinalIgnoreCase))?.Substring("filename=".Length).Trim('\"') ?? "data.csv";

                    DataTable csvDataTable = GenerateCsvDataTable(bodyString);

                    if (csvDataTable != null)
                    {
                        mainEditorResponseBody.CsvResponseViewerContent.LoadDataTable(csvDataTable, bodyString, fileName);
                        mainEditorResponseBody.CsvResponseViewer.Visibility = Visibility.Visible;
                        GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestComplete"));

                        break;
                    }
                    else
                    {
                        goto default; // If CSV parsing failed, treat it as plain text
                    }
                case ResponseBodyDisplayMode.PlainText:
                default:
                    mainEditorResponseBody.TextResponseViewer.Editor.ReadOnly = false;
                    mainEditorResponseBody.TextResponseViewer.HighlightingLanguage = "plaintext";
                    mainEditorResponseBody.TextResponseViewer.Editor.SetText(GraphEditorApplication.RemoveProblematicCharacters(bodyString));
                    GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_RequestComplete"));
                    mainEditorResponseBody.TextResponseViewer.Visibility = Visibility.Visible;
                    mainEditorResponseBody.TextResponseViewer.Editor.ReadOnly = true;

                    break;
            }

            // Show status code in InfoBar

            int numericStatusCode = (int)StatusCode;
            InfoBarSeverity severity;

            if (numericStatusCode <= 199)
            {
                severity = InfoBarSeverity.Informational;
            }
            else if (numericStatusCode <= 299)
            {
                severity = InfoBarSeverity.Success;
            }
            else if (numericStatusCode <= 399)
            {
                severity = InfoBarSeverity.Success;
            }
            else
            {
                severity = InfoBarSeverity.Error;
            }

            OpenInfoBarResponseTop(severity, "", StatusCode.ToString() + " - " + numericStatusCode.ToString(), executionRecord);
        }

        private void ClearResponse()
        {
            MainEditorResponseHeader mainEditorResponseHeader = pageCache["MainEditorResponseHeader"] as MainEditorResponseHeader;
            mainEditorResponseHeader.Header.Text = string.Empty;

            MainEditorResponseBody mainEditorResponseBody = pageCache["MainEditorResponseBody"] as MainEditorResponseBody;
            mainEditorResponseBody.HideAllViewer();

            mainEditorResponseBody.TextResponseViewer.Editor.ReadOnly = false;
            mainEditorResponseBody.TextResponseViewer.HighlightingLanguage = "json";
            mainEditorResponseBody.TextResponseViewer.Editor.SetText(string.Empty);
            mainEditorResponseBody.TextResponseViewer.Visibility = Visibility.Visible;
            mainEditorResponseBody.TextResponseViewer.Editor.ReadOnly = true;

            mainEditorResponseBody.ImageResponseViewerContent.Source = null;
            mainEditorResponseBody.ImageResponseViewerContent.Tag = null;

            mainEditorResponseBody.CsvResponseViewerContent.Children.Clear();

            // Hide status code in InfoBar
            CloseInfoBarResponseTop();
        }

        private static DataTable GenerateCsvDataTable(string CsvData)
        {
            // Create a MemoryStream from the CSV data
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(CsvData))
            {
                Position = 0 // Reset stream position to the beginning
            };

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var dataTable = new DataTable();
            bool isHeader = true;
            List<string> columnNames = new();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null) continue;

                var fields = ParseCsvLine(line);

                if (isHeader)
                {
                    foreach (var columnName in fields)
                    {
                        dataTable.Columns.Add(columnName, typeof(string));
                        columnNames.Add(columnName);
                    }
                    isHeader = false;
                }
                else
                {
                    var row = dataTable.NewRow();

                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        string columnName = columnNames[i];

                        if (i < fields.Count)
                        {
                            string value = fields[i];
                            row[columnName] = string.IsNullOrWhiteSpace(value) ? "" : value;
                        }
                        else
                        {
                            row[columnName] = "";
                        }
                    }

                    dataTable.Rows.Add(row);
                }
            }

            return dataTable;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var field = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            field.Append('"');
                            i++; // skip next quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        result.Add(field.ToString());
                        field.Clear();
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
            }

            result.Add(field.ToString()); // Add the last field
            return result;
        }

        private void OpenInfoBarResponseTop(InfoBarSeverity Severity, string Title, string Message, ExecutionRecord Record)
        {
            InfoBar_ResponseTop.Severity = Severity;
            TextBlock_InfoBar_ResponseTop_Title.Text = Title;
            TextBlock_InfoBar_ResponseTop_Message.Text = Message;
            InfoBar_ResponseTop.Tag = Record;
            InfoBar_ResponseTop.IsOpen = true;
        }

        private void CloseInfoBarResponseTop()
        {
            InfoBar_ResponseTop.IsOpen = false;
            InfoBar_ResponseTop.Severity = InfoBarSeverity.Informational;
            InfoBar_ResponseTop.Title = "";
            InfoBar_ResponseTop.Message = "";
            TextBlock_InfoBar_ResponseTop_Title.Text = "";
            TextBlock_InfoBar_ResponseTop_Message.Text = "";
            InfoBar_ResponseTop.Tag = null;
        }

        private void ShowRequest(RequestRecord RequestRecord)
        {
            ShowRequest(RequestRecord.Method, RequestRecord.Url, RequestRecord.Headers, RequestRecord.Body, RequestRecord.FileName, RequestRecord.IsBinaryBody);
        }

        private void ShowRequest(string Method, string Url, Dictionary<string, string> Headers, string Body, string FileName, bool IsBinaryBody)
        {
            ComboBox_RequestMethod.SelectedValue = Method;
            AutoSuggestBox_RequestUrl.Text = Url;

            // Set request headers
            pageCache.TryGetValue("MainEditorRequestHeader", out Page requestHeaderPage);
            MainEditorRequestHeader mainEditorRequestHeader = requestHeaderPage as MainEditorRequestHeader;
            mainEditorRequestHeader.ReplaceAllHeader(Headers);

            // Set request body
            pageCache.TryGetValue("MainEditorRequestBody", out Page requestBodyPage);
            MainEditorRequestBody mainEditorRequestBody = requestBodyPage as MainEditorRequestBody;
            mainEditorRequestBody.Body.Editor.ReadOnly = false;
            mainEditorRequestBody.Body.Editor.SetText(Body);

            TextBlock_BodyFileNameValue.Text = FileName;

            toggleSwithch_SendBinary_ToggledByCode = true;
            ToggleSwitch_SendBinary.IsOn = IsBinaryBody;

            if (IsBinaryBody)
            {
                // Show the file name
                TextBlock_BodyFileNameHeader.Visibility = Visibility.Visible;
                TextBlock_BodyFileNameValue.Visibility = Visibility.Visible;

                // Disable the editor
                mainEditorRequestBody.Body.HighlightingLanguage = "plaintext";
                mainEditorRequestBody.Body.Editor.ReadOnly = true;
                mainEditorRequestBody.Body.Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray);
            }
            else
            {
                // Hide the file name
                TextBlock_BodyFileNameHeader.Visibility = Visibility.Collapsed;
                TextBlock_BodyFileNameValue.Visibility = Visibility.Collapsed;
                TextBlock_BodyFileNameValue.Text = String.Empty;

                // Enable the editor
                mainEditorRequestBody.Body.HighlightingLanguage = "json";
                mainEditorRequestBody.Body.Background = new SolidColorBrush(Microsoft.UI.Colors.White);
            }
        }

        private void OpenInfoBarTop(InfoBarSeverity Severity, string Title, string Message)
        {
            InfoBar_Top.Severity = Severity;
            InfoBar_Top.Title = Title;
            InfoBar_Top.Message = Message;
            InfoBar_Top.IsOpen = true;
        }

        private void OpenInfoBarTop(InfoBarSeverity Severity, string Title, string Message, string LinkText, string URL)
        {
            InfoBar_Top.Severity = Severity;

            HyperlinkButton hyperlinkButton = new HyperlinkButton()
            {
                Content = LinkText,
                NavigateUri = new Uri(URL),
                VerticalAlignment = VerticalAlignment.Center
            };

            InfoBar_Top.Content = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Children =
                {
                    new TextBlock()
                    {
                        Text = Title,
                        Margin = new Thickness(0, 0, 16, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold
                    },
                    new TextBlock()
                    {
                        Text = Message,
                        Margin = new Thickness(0, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    hyperlinkButton
                }
            };

            ToolTipService.SetToolTip(hyperlinkButton, "Open " + URL);

            InfoBar_Top.IsOpen = true;
        }

        private void CloseInfoBarTop()
        {
            InfoBar_Top.IsOpen = false;
            InfoBar_Top.Severity = InfoBarSeverity.Informational;
            InfoBar_Top.Title = "";
            InfoBar_Top.Message = "";
            InfoBar_Top.Content = null;
        }

        internal async void LoadExecutionRecord(ExecutionRecord record, bool OnlyRequestToRerun = false)
        {
            // Close InfoBar
            CloseInfoBarTop();

            // Clear response
            ClearResponse();

            // Show request in MainEditorRequestHeader and MainEditorRequestBody
            ShowRequest(record.Request);

            if (OnlyRequestToRerun)
            {
                // Open InfoBar and show message
                OpenInfoBarTop(InfoBarSeverity.Informational, "Information", GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_ExecutionRecordLoadedForRerun"));
                GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_ExecutionRecordLoadedForRerun"));
            }
            else
            {
                await ShowResponseAsync(record);

                // Open InfoBar and show message
                OpenInfoBarTop(InfoBarSeverity.Informational, "Information", GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_ExecutionRecordLoaded"));
                GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_ExecutionRecordLoaded"));
            }
        }

        internal void LoadSampleQuery(SampleQueryItem sampleQueryItem)
        {
            // Close InfoBar
            CloseInfoBarTop();

            // Resolve placeholders in the sample query Url
            string resolvedSampleQueryUrl = ResolveSampleQueryPlaceholder(sampleQueryItem.Url);

            // Resolve placeholders in the sample query headers
            Dictionary<string, string> resolvedSampleQueryHeaders = ResolveSampleQueryPlaceholder(sampleQueryItem.Headers);

            // Resolve placeholders in the sample query body
            string resolvedSampleQueryBody = ResolveSampleQueryPlaceholder(sampleQueryItem.Body);

            // Show the sample query in MainEditorRequestHeader and MainEditorRequestBody
            // FileName is always empty and IsBinaryBody is always false because the sample query does not contain binary body
            ShowRequest(sampleQueryItem.Method, resolvedSampleQueryUrl, resolvedSampleQueryHeaders, resolvedSampleQueryBody, FileName: string.Empty, IsBinaryBody: false);

            // Clear response
            ClearResponse();

            // Open InfoBar and show message
            OpenInfoBarTop(InfoBarSeverity.Informational, "Information", GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_SampleQueryLoaded"));
            GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_SampleQueryLoaded"));
        }

        internal void LoadSampleQuery(SampleQueryItem sampleQueryItem, string FileName, string Base64EncodedBody)
        {
            // Close InfoBar
            CloseInfoBarTop();

            // Resolve placeholders in the sample query Url
            string resolvedSampleQueryUrl = ResolveSampleQueryPlaceholder(sampleQueryItem.Url);

            // Resolve placeholders in the sample query headers
            Dictionary<string, string> resolvedSampleQueryHeaders = ResolveSampleQueryPlaceholder(sampleQueryItem.Headers);

            // Show the sample query in MainEditorRequestHeader and MainEditorRequestBody
            // FileName is always empty and IsBinaryBody is always false because the sample query does not contain binary body
            ShowRequest(sampleQueryItem.Method, resolvedSampleQueryUrl, resolvedSampleQueryHeaders, Base64EncodedBody, FileName, sampleQueryItem.BinaryBodyRequired);

            // Clear response
            ClearResponse();

            // Open InfoBar and show message
            OpenInfoBarTop(InfoBarSeverity.Informational, "Information", GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_SampleQueryLoaded"));
            GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorContainer", "Message_SampleQueryLoaded"));
        }

        private string ResolveSampleQueryPlaceholder(string OriginalSampleQyeryString)
        {
            string result = OriginalSampleQyeryString;

            foreach (var function in SampleQueryFunctions)
            {
                result = result.Replace(function.Key, function.Value());
            }

            return result;
        }

        private Dictionary<string, string> ResolveSampleQueryPlaceholder(Dictionary<string, string> Headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (var header in Headers)
            {
                result.Add(header.Key, ResolveSampleQueryPlaceholder(header.Value));
            }

            return result;
        }

        private readonly Dictionary<string, Func<string>> SampleQueryFunctions = new Dictionary<string, Func<string>>()
        {
            {
                "${DomainName}", () =>
                {
                    return SignInUserDomainName;
                }
            },
            {
                "${UserObjectId}", () =>
                {
                    var authResult = EditorAccessToken.Instance.AuthenticationResult;
                    return authResult != null && authResult.Account != null && authResult.Account.HomeAccountId != null && authResult.Account.HomeAccountId.ObjectId != null
                    ? authResult.Account.HomeAccountId.ObjectId
                    : "{id | userPrincipalName}";
                }
            },
            {
                "${ObjectId}", () =>
                {
                    var authResult = EditorAccessToken.Instance.AuthenticationResult;
                    return authResult != null && authResult.Account != null && authResult.Account.HomeAccountId != null && authResult.Account.HomeAccountId.ObjectId != null
                    ? authResult.Account.HomeAccountId.ObjectId
                    : "{objectId}";
                }
            },
            {
                "${LocalTimeZone}", () =>
                {
                    return TimeZoneInfo.Local.Id;
                }
            },
            {
                "${LocalTimeZoneOffset}", () =>
                {
                    var offset = TimeZoneInfo.Local.BaseUtcOffset;
                    return offset.Hours >= 0
                    ? "+" + offset.ToString("hh\\:mm")
                    : "-" + offset.ToString("hh\\:mm");
                }
            },
            {
                "${Date1DayLater}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
                }
            },
            {
                "${Date120DayLater}", () =>
                {
                    return DateTime.Now.AddDays(120).ToString("yyyy-MM-dd");
                }
            },
            {
                "${DateTime0DayLater0}", () =>
                {
                    return DateTime.Now.ToString("yyyy-MM-dd") + "T00:00:00";
                }
            },
            {
                "${DateTime0DayLater18}", () =>
                {
                    return DateTime.Now.ToString("yyyy-MM-dd") + "T18:00:00";
                }
            },
            {
                "${DateTime1DayLater0}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T00:00:00";
                }
            },
            {
                "${DateTime1DayLater9}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T09:00:00";
                }
            },
            {
                "${DateTime1DayLater10}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T10:00:00";
                }
            },
            {
                "${DateTime1DayLater12}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T12:00:00";
                }
            },
            {
                "${DateTime1DayLater14}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T14:00:00";
                }
            },
            {
                "${DateTime1DayLater17}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T17:00:00";
                }
            },
            {
                "${DateTime1DayLater18}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T18:00:00";
                }
            },
            {
                "${DateTime2DayLater0}", () =>
                {
                    return DateTime.Now.AddDays(2).ToString("yyyy-MM-dd") + "T00:00:00";
                }
            },
            {
                "${DateTime8DayLater18}", () =>
                {
                    return DateTime.Now.AddDays(8).ToString("yyyy-MM-dd") + "T18:00:00";
                }
            },
            {
                "${DateTime3DayLater17}", () =>
                {
                    return DateTime.Now.AddDays(3).ToString("yyyy-MM-dd") + "T17:00:00";
                }
            },
            {
                "${DateTime1DayLater0Utc}", () =>
                {
                    return DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T00:00:00Z";
                }
            },
            {
                "${SampleInternalUser1Address}", () =>
                {
                    return "AdeleV@" + SignInUserDomainName;
                }
            },
            {
                "${SampleInternalUser1Name}", () =>
                {
                    return "Adele Vance";
                }
            },
            {
                "${SampleInternalUser1MailNickname}", () =>
                {
                    return "AdeleV";
                }
            },
            {
                "${SampleInternalUser2Address}", () =>
                {
                    return "samanthab@" + SignInUserDomainName;
                }
            },
            {
                "${SampleInternalUser2Name}", () =>
                {
                    return "Samantha Booth";
                }
            },
             {
                "${SampleInternalUser3Address}", () =>
                {
                    return "DanaS@" + SignInUserDomainName;
                }
            },
            {
                "${SampleInternalUser3Name}", () =>
                {
                    return "Dana Swope";
                }
            },
            {
                "${SampleInternalUser4Address}", () =>
                {
                    return "AlexW@" + SignInUserDomainName;
                }
            },
            {
                "${SampleInternalUser4Name}", () =>
                {
                    return "Alex Wilber";
                }
            },
            {
                "${SampleInternalUser5Address}", () =>
                {
                    return "meganb@" + SignInUserDomainName;
                }
            },
            {
                "${SampleInternalUser6Address}", () =>
                {
                    return "frannis@" + SignInUserDomainName;
                }
            },
            {
                "${SampleInternalUser7Address}", () =>
                {
                    return "fannyd@" + SignInUserDomainName;
                }
            },
            {
                "${SampleInternalUser8Address}", () =>
                {
                    return "alexd@" + SignInUserDomainName;
                }
            },
            {
                "${SampleInternalUser8Name}", () =>
                {
                    return "Alex Darrow";
                }
            },
            {
                "${SampleInternalUser9Address}", () =>
                {
                    return "johndoe@" + SignInUserDomainName;
                }
            },
            {
                "${Guid}", () =>
                {
                    return Guid.NewGuid().ToString();
                }
            }
        };

        private static string SignInUserDomainName
        {
            get
            {
                var authResult = EditorAccessToken.Instance.AuthenticationResult;
                return authResult != null && authResult.Account != null
                ? authResult.Account.Username.Split("@")[1]
                : "contoso.com";
            }
        }

        // Normalize URL parts for comparison
        // - /me → users/{id}
        // - /users/<Actual values and placeholders> → users/{id}
        private string[] NormalizeParts(string[] Parts, bool NormalizeMePart = true)
        {
            var wellKnownArrayPartNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "calendars",
                "events",
                "groups",
                "messages",
                "mailFolders",
                "sites",
                "users"
            };

            var normalizedParts = new List<string>();
            for (int i = 0; i < Parts.Length; i++)
            {
                var seg = Parts[i];

                if (NormalizeMePart == true && seg.Equals("me", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedParts.Add("users");
                    normalizedParts.Add("{id}");
                    continue;
                }

                if (wellKnownArrayPartNames.Contains(seg))
                {
                    normalizedParts.Add(seg);
                    if (i + 1 < Parts.Length)
                    {
                        normalizedParts.Add("{id}");
                        i++; // Skip actual values and any placeholders
                    }
                    continue;
                }

                normalizedParts.Add(seg);
            }
            return normalizedParts.ToArray();
        }

        private List<SuggestionItem> GetNextCandidates(string UserInputUrl)
        {
            if (string.IsNullOrWhiteSpace(UserInputUrl) || preNormalizedSampleUrls.Count == 0)
            {
                return new();
            }

            // Convert "/me/" for comparison
            UserInputUrl = UserInputUrl.Replace("/me/", "/users/user01@contoso.com/", StringComparison.OrdinalIgnoreCase);

            var userInputUrlParts = UserInputUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Split the URL into parts
            // The last part is treated as input-in-progress
            var userInputUrlLastPart = UserInputUrl.EndsWith('/') ? "" : userInputUrlParts.Last();
            var userInputUrlPrefixParts = UserInputUrl.EndsWith('/') ? userInputUrlParts : userInputUrlParts.Take(userInputUrlParts.Length - 1).ToArray();

            var normalizedUserInputUrlPrefixParts = NormalizeParts(userInputUrlPrefixParts);
            var candidates = new List<SuggestionItem>();

            foreach (var sampleUrl in preNormalizedSampleUrls)
            {
                var sampleUrlParts = sampleUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var normalizedSampleUrlParts = NormalizeParts(sampleUrlParts, NormalizeMePart: false);

                if (normalizedUserInputUrlPrefixParts.Length >= normalizedSampleUrlParts.Length)
                {
                    // Skip if the sample URL is shorter than or equal to the user input prefix
                    continue;
                }

                // Compare normalized prefix parts
                bool match = true;
                for (int i = 0; i < normalizedUserInputUrlPrefixParts.Length; i++)
                {
                    if (!normalizedUserInputUrlPrefixParts[i].Equals(normalizedSampleUrlParts[i], StringComparison.OrdinalIgnoreCase))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    // Get candidates from the matched sample URL
                    // Here we don't use the normalized parts
                    var nextPart = sampleUrlParts[normalizedUserInputUrlPrefixParts.Length];

                    if (!string.IsNullOrEmpty(userInputUrlLastPart) && !nextPart.StartsWith(userInputUrlLastPart, StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip if the next part does not match the input-in-progress
                        continue;
                    }

                    // Resolve placeholders in the next part and add to candidates
                    var example = ResolveSampleQueryPlaceholder(nextPart);
                    candidates.Add(new SuggestionItem
                    {
                        DisplayText = example,
                        InsertText = example,
                        IsHistory = false
                    });
                }
            }

            return candidates
                .GroupBy(c => c.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void AutoSuggestBox_RequestUrl_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var candidates = GetNextCandidates(sender.Text);

                // Add history matches as candidates (deduplicated, max 3, reversed)
                var historyMatches = ExecutionRecordManager.ExecutionRecordList
                    .Where(h => h.Request.Url.StartsWith(sender.Text, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(h => h.Request.Url, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .Take(3)
                    .Reverse();

                foreach ( var historyMatch in historyMatches ) {
                    if (historyMatch != null)
                    {
                        // Get the part before the last slash in sender.Text
                        var lastSlashIndex = sender.Text.LastIndexOf("/");
                        var historyCandidateInsertText = historyMatch.Request.Url.Substring(lastSlashIndex + 1);
                        var historyCandidateDisplayText = historyCandidateInsertText;

                        candidates.Insert(0, new SuggestionItem { DisplayText = historyCandidateDisplayText, InsertText = historyCandidateInsertText, IsHistory = true });
                    }
                }

                sender.ItemsSource = candidates.ToList();
            }
        }

        private void AutoSuggestBox_RequestUrl_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is SuggestionItem item)
            {
                // User selected a suggestion using mouse or touch
                ApplyChosenSuggestionToRequestUrl(item.InsertText);
            }
        }

        private void AutoSuggestBox_RequestUrl_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is SuggestionItem item)
            {
                // User selected a suggestion using Enter key
                ApplyChosenSuggestionToRequestUrl(item.InsertText);
            }
            else
            {
                ExecureRequest();
            }
        }

        private void AutoSuggestBox_RequestUrl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Check if the key is a combination of Ctrl + V
            var ctrlDown = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

            if (ctrlDown && e.Key == VirtualKey.V)
            {
                bool parseResult = ExecutionRecordManager.TryParseClipboardTextToRequestRecord(out RequestRecord clipboardRequest);

                if (parseResult)
                {
                    //LoadExecutionRecord(new ExecutionRecord { Request = clipboardRequest }, OnlyRequestToRerun: true);
                    ShowRequest(clipboardRequest);

                    // Successfully parsed clipboard text into RequestRecord
                    e.Handled = true; // Mark the event as handled
                }
                else
                {
                    // Failed to parse clipboard text
                    // Do nothing and allow the default paste behavior
                }
            }
        }

        private void ApplyChosenSuggestionToRequestUrl(string ChosenSuggestion)
        {
            if (string.IsNullOrEmpty(ChosenSuggestion))
            {
                return;
            }

            if (AutoSuggestBox_RequestUrl.Text == ChosenSuggestion)
            {
                return;
            }

            if (AutoSuggestBox_RequestUrl.Text.EndsWith(ChosenSuggestion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Replace the part after the last slash with the chosen suggestion
            var slash = AutoSuggestBox_RequestUrl.Text.LastIndexOf('/');
            if (slash >= 0)
            {
                AutoSuggestBox_RequestUrl.Text = AutoSuggestBox_RequestUrl.Text.Substring(0, slash + 1) + ChosenSuggestion;
            }
            else
            {
                AutoSuggestBox_RequestUrl.Text = ChosenSuggestion;
            }
        }
    }

    // Suggestion item for AutoSuggestBox
    public class SuggestionItem
    {
        public string DisplayText { get; set; }
        public string InsertText { get; set; }
        public bool IsHistory { get; set; }
        public override string ToString() => DisplayText;

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(SuggestionItem))]
        public SuggestionItem() { }
    }
}
