﻿using Graph_Editor.Data.ExecutionRecord;
using Graph_Editor.Data.SampleQuery;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Storage;

namespace Graph_Editor
{
    public static class GraphEditorApplication
    {
        public readonly static int DefaultMaxExecutionRecordCount = 30;

        public static void NavigateToMainEditor()
        {
            (Application.Current as App).MainWindowAccessor.NavigateToMainEditor();
        }

        public static void NavigateToMainEditor(ExecutionRecord Record, bool OnlyRequestToRerun = false)
        {
            (Application.Current as App).MainWindowAccessor.NavigateToMainEditor(Record, OnlyRequestToRerun);
        }

        public static void NavigateToMainEditor(SampleQueryItem SampleQuery)
        {
            (Application.Current as App).MainWindowAccessor.NavigateToMainEditor(SampleQuery);
        }

        public static void NavigateToMainEditor(SampleQueryItem SampleQuery, string FileName, string Base64EncodedBody)
        {
            (Application.Current as App).MainWindowAccessor.NavigateToMainEditor(SampleQuery, FileName, Base64EncodedBody);
        }

        public static void NavigateToAccessTokenWizard()
        {
            (Application.Current as App).MainWindowAccessor.NavigateToAccessTokenWizard();
        }

        public static void UpdateStatusBarMainStatus(string Text)
        {
            (Application.Current as App).MainWindowAccessor.UpdateStatusBarMainStatus(Text);
        }

        public static bool TryParseJson(string Json, out string Result)
        {
            if (!string.IsNullOrEmpty(Json))
            {
                try
                {
                    // Parse the JSON string and format it with indentation
                    Result = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(Json), Formatting.Indented);
                    return true;
                }
                catch (JsonException)
                {
                    // Return false if parsing fails
                    Result = Json;
                    return false;
                }
            }

            Result = Json;
            return false;
        }

        public static string RemoveProblematicCharacters(string Input)
        {
            // Remove some characters that cause problems when searching text in the editor

            // Replace NBSP from the input string
            var temp = Input.Replace("\u00A0", " ");
            // Replace NNBSP from the input string
            temp = temp.Replace("\u202F", " ");
            // Replace left and right single quotation marks with normal single quotation marks
            temp = temp.Replace("\u2018", "'").Replace("\u2019", "'");
            // Replace left and right double quotation marks with normal double quotation marks
            temp = temp.Replace("\u201C", "\"").Replace("\u201D", "\"");

            return temp;
        }

        public static T GetSetting<T>(Settings Key, T DefaultValue)
        {
            if (string.IsNullOrEmpty(Key.ToString())) {
                return DefaultValue;
            }

            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey(Key.ToString()))
                {
                    return (T)localSettings.Values[Key.ToString()];
                }
                else
                {
                    return DefaultValue;
                }
            }
            catch
            {
                Debug.WriteLine("Could not get setting : "  + Key);
                return DefaultValue;
            }
        }

        public static void SaveSetting(Settings Key, object Value)
        {
            try
            {
                if (Key.ToString().Contains("Password"))
                {
                    throw new Exception("Use SaveConfidentialSetting to save password.");
                }

                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[Key.ToString()] = Value;
            }
            catch
            {
                Debug.WriteLine("Could not save setting : " + Key);
            }
        }

        public static string GetConfidentialSetting(Settings Key, String DefaultValue)
        {
            var vault = new PasswordVault();

            try
            {
                var passwordCredential = vault.Retrieve("Graph-Editor", Key.ToString());

                if (passwordCredential != null)
                {
                    passwordCredential.RetrievePassword();
                    return passwordCredential.Password;
                }
                else
                {
                    return DefaultValue;
                }
            }
            catch
            {
                Debug.WriteLine("Could not get setting : " + Key);
                return DefaultValue;
            }
        }

        public static void SaveConfidentialSetting(Settings Key, string Value)
        {
            try
            {
                var vault = new PasswordVault();
                vault.Add(new PasswordCredential("Graph-Editor", Key.ToString(), Value));
            }
            catch
            {
                Debug.WriteLine("Could not save setting : " + Key);
            }
        }

        public static string GetResourceString(string ViewName, string ResourceName)
        {
            try
            {
                return new ResourceManager().MainResourceMap.GetValue(ViewName + "/" + ResourceName).ValueAsString;
            }
            catch
            {
                return "";
            }
        }

        public static string GetRandomTipsResourceName()
        {
            var availaleTips = new List<string>
            {
                "Message_WelcomeMessage",
                "Message_KnownIssue_UIFreezing"
            };

            // Randomly select a tip from the available tips
            Random random = new Random();
            int randomIndex = random.Next(availaleTips.Count);
            return availaleTips.ElementAt(randomIndex);
        }

        internal static void DeleteOldRequestAndResponseLogs(int retentionPeriod)
        {
            string settingLofFilePath = GraphEditorApplication.GetSetting(GraphEditorApplication.Settings.GlobalSetting_RequestAndResponseLoggingFolderPath, "");

            if (!Directory.Exists(settingLofFilePath))
            {
                // Specified log folder path is not exsisting.
                return;
            }

            // Get all log files in the specified folder and sort them by file name
            var logFiles = Directory.GetFiles(settingLofFilePath, "MainEditorLog_*.log");
            var sortedLogFiles = logFiles.OrderBy(f => f).ToList();

            // Log file name format: MainEditorLog_yyyyMMdd.log
            // Remove all log files older than the retention period
            foreach (var logFile in sortedLogFiles)
            {
                DateTime fileDate;
                string fileName = Path.GetFileName(logFile);

                // Extract the date from the file name
                string logPrefix = "MainEditorLog_";

                if (fileName != null && fileName.StartsWith(logPrefix) && DateTime.TryParseExact(fileName.Substring(logPrefix.Length, 8), "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out fileDate))
                {
                    if ((DateTime.UtcNow - fileDate).TotalDays > retentionPeriod)
                    {
                        try
                        {
                            File.Delete(logFile);
                        }
                        catch (Exception)
                        {
                            // Handle exception if needed
                        }
                    }
                    else
                    {
                        // If the file is within the retention period, break the loop
                        break;
                    }
                }
            }
        }

        public static string DialogYesButtonText
        {
            get
            {
                return GetResourceString("Resources", "Message_DialogYesButtonText");
            }
        }

        public static string DialogNoButtonText
        {
            get
            {
                return GetResourceString("Resources", "Message_DialogNoButtonText");
            }
        }

        public static string DialogCloseButtonText {
            get
            {
                return GetResourceString("Resources", "Message_DialogCloseButtonText");
            }
        }

        public struct LanguageInfo
        {
            public string Language;
            public string Locale;
        }

        public static LanguageInfo[] SupportedLanguages
        {
            get
            {
                return new LanguageInfo[]
                {
                    new LanguageInfo { Language = "System Default", Locale = "" },
                    new LanguageInfo { Language = "English", Locale = "en-us" },
                    new LanguageInfo { Language = "Japanese", Locale = "ja-jp" }
                        
                };
            }
        }

        public enum Settings
        {
            AccessTokenWizardMethodSelectionPage__RadioButtons_Method__SelectedIndex,
            AccessTokenWizardBuiltInPage_LastSelectedScopes,
            AccessTokenWizardClientSecretPage__TextBox_TenantName__Text,
            AccessTokenWizardClientSecretPage__TextBox_ApplicationId__Text,
            AccessTokenWizardClientSecretPage__PasswordBox_ClientSecret__Password,
            GlobalSetting_RequestAndResponseLoggingEnabled,
            GlobalSetting_RequestAndResponseLoggingFolderPath,
            GlobalSetting_RequestAndResponseLoggingRetentionDays,
            GlobalSetting_DisableRequestAndResponseLoggingWhenAppRestart,
            GlobalSetting_ExcludeAuthorizationHeader,
            GlobalSetting_DisplayLanguageOverride,
            GlobalSetting_MaxExecutionRecordCount,
            GlobalSetting_EncodePlusCharacter,
            GlobalSetting_EncodeSharpCharacter,
            GlobalSetting_RequestSendingConfirmation_GET,
            GlobalSetting_RequestSendingConfirmation_POST,
            GlobalSetting_RequestSendingConfirmation_PUT,
            GlobalSetting_RequestSendingConfirmation_PATCH,
            GlobalSetting_RequestSendingConfirmation_DELETE,
            GlobalSetting_CustomScopes,
            MainEditorLoggingHandler_AllowAutoRedirect
        }
    }
}
