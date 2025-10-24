using Graph_Editor.Data.ExecutionRecord;
using Microsoft.UI.Input;
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
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Search;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using WinUIEditor;

// This application uses CsvHelper, which is licensed under the Apache License, Version 2.0.
// CsvHelper Copyright (c) Josh Close and Contributors
// See LICENSE-THIRD-PARTY-CSVHELPER for details.

namespace Graph_Editor.Pages.MainEditor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainEditorResponseBody : Page
    {
        public MainEditorResponseBody()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            CodeEditorControl_ResponseBody.Editor.EndAtLastLine = true;


#if DEBUG
            string result = @"{
  ""@odata.context"": ""https://graph.microsoft.com/v1.0/$metadata#users('adfbba18-bbb0-4bd0-aec5-57c245f88e74')/calendarView"",
  ""value"": [
    {
      ""@odata.etag"": ""W/\""7uQ6Podif0K748CRVAAT9gAAGuEjng==\"""",
      ""id"": ""AAMkADRiMDQ2YzAzLTU4M2YtNGYzYi1hYWVmLTU1ZmI2YzFmNzhiMABGAAAAAACqkK9kG4vrTI2ng5iJuNxLBwDi_6QRa0e-Rr8KinrNHW-3AAAAAAENAADu5Do_h2J-QrvjwJFUABP2AAAa5oPDAAA="",
      ""createdDateTime"": ""2024-07-08T13:39:43.7211575Z"",
      ""lastModifiedDateTime"": ""2024-07-08T13:41:44.9807407Z"",
      ""changeKey"": ""7uQ6Podif0K748CRVAAT9gAAGuEjng=="",
      ""categories"": [
        ""Blue Category""
      ],
      ""transactionId"": ""e7c02c1e-2ae1-9955-f8fa-fb49e7c5be85"",
      ""originalStartTimeZone"": ""UTC"",
      ""originalEndTimeZone"": ""UTC"",
      ""iCalUId"": ""040000008200E00074C5B7101A82E008000000002BBD634A3CD1DA01000000000000000010000000C7C47E2EADB04745AC20CC46F68B2844"",
      ""uid"": ""040000008200E00074C5B7101A82E008000000002BBD634A3CD1DA01000000000000000010000000C7C47E2EADB04745AC20CC46F68B2844"",
      ""reminderMinutesBeforeStart"": 15,
      ""isReminderOn"": true,
      ""hasAttachments"": false,
      ""subject"": ""event1"",
      ""bodyPreview"": """",
      ""importance"": ""normal"",
      ""sensitivity"": ""normal"",
      ""isAllDay"": false,
      ""isCancelled"": false,
      ""isOrganizer"": true,
      ""responseRequested"": true,
      ""seriesMasterId"": null,
      ""showAs"": ""busy"",
      ""type"": ""singleInstance"",
      ""webLink"": ""https://outlook.office365.com/owa/?itemid=AAMkADRiMDQ2YzAzLTU4M2YtNGYzYi1hYWVmLTU1ZmI2YzFmNzhiMABGAAAAAACqkK9kG4vrTI2ng5iJuNxLBwDi%2B6QRa0e%2FRr8KinrNHW%2F3AAAAAAENAADu5Do%2Bh2J%2FQrvjwJFUABP2AAAa5oPDAAA%3D&exvsurl=1&path=/calendar/item"",
      ""onlineMeetingUrl"": null,
      ""isOnlineMeeting"": false,
      ""onlineMeetingProvider"": ""unknown"",
      ""allowNewTimeProposals"": true,
      ""occurrenceId"": null,
      ""isDraft"": false,
      ""hideAttendees"": false,
      ""responseStatus"": {
        ""response"": ""organizer"",
        ""time"": ""0001-01-01T00:00:00Z""
      },
      ""body"": {
        ""contentType"": ""html"",
        ""content"": ""<html>\r\n<head>\r\n<meta http-equiv=\""Content-Type\"" content=\""text/html; charset=utf-8\"">\r\n</head>\r\n<body>\r\n<div style=\""font-family:Aptos,Aptos_EmbeddedFont,Aptos_MSFontService,Calibri,Helvetica,sans-serif; font-size:12pt; color:rgb(0,0,0)\"">\r\n</div>\r\n<div style=\""font-family:Aptos,Aptos_EmbeddedFont,Aptos_MSFontService,Calibri,Helvetica,sans-serif; font-size:12pt; color:rgb(0,0,0)\"">\r\n<br>\r\n</div>\r\n</body>\r\n</html>\r\n""
      },
      ""start"": {
        ""dateTime"": ""2024-07-09T08:00:00"",
        ""timeZone"": ""UTC""
      },
      ""end"": {
        ""dateTime"": ""2024-07-09T08:30:00"",
        ""timeZone"": ""UTC""
      },
      ""location"": {
        ""displayName"": """",
        ""locationType"": ""default"",
        ""uniqueIdType"": ""unknown"",
        ""address"": {},
        ""coordinates"": {}
      },
      ""locations"": [],
      ""recurrence"": null,
      ""attendees"": [],
      ""organizer"": {
        ""emailAddress"": {
          ""name"": ""RemoteUser01"",
          ""address"": ""RemoteUser01@orenodomain.net""
        }
      },
      ""onlineMeeting"": null
    }
  ]
}";

            CodeEditorControl_ResponseBody.Editor.SetText(result);
#endif

            CodeEditorControl_ResponseBody.Editor.ReadOnly = true;
        }

        public CodeEditorControl TextResponseViewer => CodeEditorControl_ResponseBody;
        public Image ImageResponseViewerContent => Image_ResponseBody;
        public ScrollViewer ImageResponseViewer => ScrollViewer_ImageResponseBody;
        public ScrollViewer CsvResponseViewer => ScrollViewer_CsvResponseBody;
        public StackPanel CsvResponseViewerContent => StackPanel_CsvResponseBody;

        private void CodeEditorControl_ResponseBody_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.F && (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
            {
                // Ctrl + F
                ShowSearchPanel();
            }
            else if (e.Key == VirtualKey.F3)
            {
                // F3

                if (SearchPanel.Visibility == Visibility.Collapsed)
                {
                    // F3 key was pushed but SearchPanel is not visible
                    ShowSearchPanel();
                }
                else
                {
                    // Search next
                    ExecuteSearchInResponse();
                }
            }
        }

        private void TextBox_Search_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                ExecuteSearchInResponse();
            }
        }

        private void Button_SearchNext_Click(object sender, RoutedEventArgs e)
        {
            ExecuteSearchInResponse();
        }

        private void Button_CloseSearchPanel_Click(object sender, RoutedEventArgs e)
        {
            SearchPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowSearchPanel()
        {
            SearchPanel.Visibility = Visibility.Visible;
            TextBox_Search.Focus(FocusState.Programmatic);
            TextBox_Search.SelectAll();
        }

        private void ExecuteSearchInResponse()
        {
            string wordToFind = TextBox_Search.Text;

            // Get all text in CodeEditorControl_ResponseBody
            string allCurrentText = CodeEditorControl_ResponseBody.Editor.GetText(CodeEditorControl_ResponseBody.Editor.TextLength);

            // Get the current selection position
            long currentSelectionEnd = CodeEditorControl_ResponseBody.Editor.SelectionEnd;

            // Search the word and find the start position
            int foundFistIndex = allCurrentText.IndexOf(wordToFind, (int)currentSelectionEnd, StringComparison.OrdinalIgnoreCase);

            if (foundFistIndex < 0)
            {
                // Not found
                // Search from the start again

                foundFistIndex = allCurrentText.IndexOf(wordToFind, 0, StringComparison.OrdinalIgnoreCase);

                if (foundFistIndex < 0)
                {
                    // This text does not contain the word to search.
                    GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorResponseHeader", "Message_CouldNotFindText"));
                    return;
                }
                else
                {
                    GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorResponseHeader", "Message_EndOfResponseReached"));
                }
            }
            else
            {
                GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorResponseHeader", "Message_PressF3ToSearchNext"));
            }

            // Select the found word from end to start
            CodeEditorControl_ResponseBody.Editor.SetSelection(foundFistIndex + wordToFind.Length, foundFistIndex);

            // Highlight the found word
            CodeEditorControl_ResponseBody.Editor.ScrollCaret();
            CodeEditorControl_ResponseBody.Focus(FocusState.Programmatic);
        }

        public void HideAllViewer()
        {
            CodeEditorControl_ResponseBody.Visibility = Visibility.Collapsed;
            ScrollViewer_ImageResponseBody.Visibility = Visibility.Collapsed;
            ScrollViewer_CsvResponseBody.Visibility = Visibility.Collapsed;
        }

        private async void MenuFlyoutItem_Image_ResponseBody_SaveAs_Click(object sender, RoutedEventArgs e)
        {
            ResponseRecord responseRecord = (ResponseRecord)Image_ResponseBody.Tag;

            if (responseRecord == null || responseRecord.Base64EncodedBinaryBody == null)
            {
                GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorResponseBody", "Message_NoImageToSave"));
                return;
            }

            // Create a file picker
            FileSavePicker savePicker = new FileSavePicker();

            // Retrieve the window handle (HWND) of the main window.
            var window = (Application.Current as App)?.MainWindowAccessor;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            // Set options
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;

            // Set the file type choices
            string fileExtension = ".png"; // Default to PNG if ContentType is null or not recognized

            if (!string.IsNullOrEmpty(responseRecord.ContentType))
            {
                string contentType = responseRecord.ContentType.ToLowerInvariant();
                if (contentType.Contains("image/png"))
                {
                    fileExtension = ".png";
                    savePicker.FileTypeChoices.Add("PNG Image", new List<string> { fileExtension });
                }
                else if (contentType.Contains("image/jpeg"))
                {
                    fileExtension = ".jpg";
                    savePicker.FileTypeChoices.Add("JPEG Image", new List<string> { fileExtension });  
                }
                else if (contentType.Contains("image/gif"))
                {
                    fileExtension = ".gif";
                    savePicker.FileTypeChoices.Add("GIF Image", new List<string> { fileExtension });
                }
                else if (contentType.Contains("image/bmp"))
                {
                    fileExtension = ".bmp";
                    savePicker.FileTypeChoices.Add("BMP Image", new List<string> { fileExtension });
                }
                else
                {
                    // If the content type is not recognized, default to PNG
                    savePicker.FileTypeChoices.Add("PNG Image", new List<string> { fileExtension });
                }
            }
            else
            {
                // If ContentType is null or empty, default to PNG
                savePicker.FileTypeChoices.Add("PNG Image", new List<string> { fileExtension });
            }

            savePicker.SuggestedFileName = $"ResponseImage{fileExtension}";
            

            var saveFile = await savePicker.PickSaveFileAsync();
            if (null == saveFile)
            {
                return; // User cancelled the save operation                
            }

            byte[] imageBytes = Convert.FromBase64String(responseRecord.Base64EncodedBinaryBody);

            try
            {
                await Windows.Storage.FileIO.WriteBytesAsync(saveFile, imageBytes);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that may occur during the save operation
                // Show ContentDialog as an error dialog
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to save the image: {ex.Message}",
                    CloseButtonText = "Ok"
                };

                await dialog.ShowAsync();
                
                return;
            }
            
            GraphEditorApplication.UpdateStatusBarMainStatus(GraphEditorApplication.GetResourceString("Pages.MainEditor.MainEditorResponseBody", "Message_ImageSavedSuccessfully"));
        }
    }
}
