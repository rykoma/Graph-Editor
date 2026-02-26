using Graph_Editor.Data.ExecutionRecord;
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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Storage;

namespace Graph_Editor
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SettingKeyAttribute : Attribute
    {
        public string Key { get; }

        public SettingKeyAttribute(string key)
        {
            Key = key;
        }
    }

    public static class GraphEditorApplication
    {
        private const string MigrationCompletedFlagKey = "_SettingsMigrationToGuid_Completed";
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

        private static string GetSettingKey(Settings setting)
        {
            var fieldInfo = setting.GetType().GetField(setting.ToString());
            var attribute = fieldInfo?.GetCustomAttribute<SettingKeyAttribute>();

            return attribute?.Key ?? setting.ToString();
        }

        public static void MigrateAllSettingsToGuidKeys()
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.ContainsKey(MigrationCompletedFlagKey))
            {
                return;
            }

            Debug.WriteLine("Starting settings migration to GUID keys...");

            MigrateLocalSettings();
            MigrateConfidentialSettings();

            localSettings.Values[MigrationCompletedFlagKey] = true;
            Debug.WriteLine("Settings migration completed.");
        }

        private static void MigrateLocalSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            foreach (Settings setting in Enum.GetValues<Settings>())
            {
                try
                {
                    string oldKey = setting.ToString();

                    if (oldKey.Contains("Password"))
                    {
                        continue;
                    }

                    string newKey = GetSettingKey(setting);

                    if (oldKey == newKey || localSettings.Values.ContainsKey(newKey))
                    {
                        continue;
                    }

                    if (localSettings.Values.ContainsKey(oldKey))
                    {
                        var value = localSettings.Values[oldKey];
                        localSettings.Values[newKey] = value;
                        Debug.WriteLine($"Migrated setting: {oldKey} -> {newKey}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to migrate setting {setting}: {ex.Message}");
                }
            }
        }

        private static void MigrateConfidentialSettings()
        {
            var vault = new PasswordVault();

            foreach (Settings setting in Enum.GetValues<Settings>())
            {
                if (!setting.ToString().Contains("Password"))
                {
                    continue;
                }

                try
                {
                    string oldKey = setting.ToString();
                    string newKey = GetSettingKey(setting);

                    if (oldKey == newKey)
                    {
                        continue;
                    }

                    try
                    {
                        var existingNew = vault.Retrieve("Graph-Editor", newKey);
                        if (existingNew != null)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        var oldCredential = vault.Retrieve("Graph-Editor", oldKey);
                        if (oldCredential != null)
                        {
                            oldCredential.RetrievePassword();
                            vault.Add(new PasswordCredential("Graph-Editor", newKey, oldCredential.Password));
                            Debug.WriteLine($"Migrated confidential setting: {oldKey} -> {newKey}");
                        }
                    }
                    catch
                    {
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to migrate confidential setting {setting}: {ex.Message}");
                }
            }
        }

        public static T GetSetting<T>(Settings Key, T DefaultValue)
        {
            string settingKey = GetSettingKey(Key);

            if (string.IsNullOrEmpty(settingKey)) {
                return DefaultValue;
            }

            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey(settingKey))
                {
                    return (T)localSettings.Values[settingKey];
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

                string settingKey = GetSettingKey(Key);
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[settingKey] = Value;
            }
            catch
            {
                Debug.WriteLine("Could not save setting : " + Key);
            }
        }

        public static string GetConfidentialSetting(Settings Key, String DefaultValue)
        {
            var vault = new PasswordVault();
            string settingKey = GetSettingKey(Key);

            try
            {
                var passwordCredential = vault.Retrieve("Graph-Editor", settingKey);

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
                string settingKey = GetSettingKey(Key);
                var vault = new PasswordVault();
                vault.Add(new PasswordCredential("Graph-Editor", settingKey, Value));
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
                "Message_KnownIssue_SampleQueryIncomplete",
                "Message_KnownIssue_AppLibraryNotAvailable"
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
            /// <summary>Authentication Wizard: Previously used authentication flow</summary>
            [SettingKey("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d")]
            AccessTokenWizardMethodSelectionPage__RadioButtons_Method__SelectedIndex,

            /// <summary>Authentication Wizard: Previously used scopes</summary>
            [SettingKey("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e")]
            AccessTokenWizardBuiltInPage_LastSelectedScopes,

            /// <summary>Authentication Wizard: Previously used tenant name</summary>
            [SettingKey("c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f")]
            AccessTokenWizardClientSecretPage__TextBox_TenantName__Text,

            /// <summary>Authentication Wizard: Previously used application ID</summary>
            [SettingKey("d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a")]
            AccessTokenWizardClientSecretPage__TextBox_ApplicationId__Text,

            /// <summary>Authentication Wizard: Previously used client secret</summary>
            [SettingKey("e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b")]
            AccessTokenWizardClientSecretPage__PasswordBox_ClientSecret__Password,

            /// <summary>Global Setting: Enable request/response logging</summary>
            [SettingKey("f6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c")]
            GlobalSetting_RequestAndResponseLoggingEnabled,

            /// <summary>Global Setting: Request/response log folder path</summary>
            [SettingKey("a7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d")]
            GlobalSetting_RequestAndResponseLoggingFolderPath,

            /// <summary>Global Setting: Request/response log retention days</summary>
            [SettingKey("b8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e")]
            GlobalSetting_RequestAndResponseLoggingRetentionDays,

            /// <summary>Global Setting: Disable logging when app restarts</summary>
            [SettingKey("c9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f")]
            GlobalSetting_DisableRequestAndResponseLoggingWhenAppRestart,

            /// <summary>Global Setting: Exclude Authorization header when logging</summary>
            [SettingKey("d0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a")]
            GlobalSetting_ExcludeAuthorizationHeader,

            /// <summary>Global Setting: Display language override</summary>
            [SettingKey("e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b")]
            GlobalSetting_DisplayLanguageOverride,

            /// <summary>Global Setting: Maximum execution record count</summary>
            [SettingKey("f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c")]
            GlobalSetting_MaxExecutionRecordCount,

            /// <summary>Global Setting: Encode plus character</summary>
            [SettingKey("a3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d")]
            GlobalSetting_EncodePlusCharacter,

            /// <summary>Global Setting: Encode sharp character</summary>
            [SettingKey("b4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e")]
            GlobalSetting_EncodeSharpCharacter,

            /// <summary>Global Setting: Request sending confirmation (GET)</summary>
            [SettingKey("c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f")]
            GlobalSetting_RequestSendingConfirmation_GET,

            /// <summary>Global Setting: Request sending confirmation (POST)</summary>
            [SettingKey("d6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a")]
            GlobalSetting_RequestSendingConfirmation_POST,

            /// <summary>Global Setting: Request sending confirmation (PUT)</summary>
            [SettingKey("e7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b")]
            GlobalSetting_RequestSendingConfirmation_PUT,

            /// <summary>Global Setting: Request sending confirmation (PATCH)</summary>
            [SettingKey("f8a9b0c1-d2e3-4f4a-5b6c-7d8e9f0a1b2c")]
            GlobalSetting_RequestSendingConfirmation_PATCH,

            /// <summary>Global Setting: Request sending confirmation (DELETE)</summary>
            [SettingKey("a9b0c1d2-e3f4-4a5b-6c7d-8e9f0a1b2c3d")]
            GlobalSetting_RequestSendingConfirmation_DELETE,

            /// <summary>Global Setting: Custom scopes</summary>
            [SettingKey("b0c1d2e3-f4a5-4b6c-7d8e-9f0a1b2c3d4e")]
            GlobalSetting_CustomScopes,

            /// <summary>Main Editor: Allow automatic redirect</summary>
            [SettingKey("c1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f")]
            MainEditorLoggingHandler_AllowAutoRedirect
        }
    }
}
