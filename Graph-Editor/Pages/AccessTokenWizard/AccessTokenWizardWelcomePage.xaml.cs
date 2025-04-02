using Graph_Editor.Data.EditorAccessToken;
using Graph_Editor.Pages.AccessTokenWizard.CurrentAccessToken;
using Graph_Editor.Pages.MainEditor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.AccessTokenWizard
{
    public sealed partial class AccessTokenWizardWelcomePage : Page
    {
        // Cache of pages of NavigationView
        private Dictionary<string, Page> pageCache = [];

        public AccessTokenWizardWelcomePage()
        {
            this.InitializeComponent();

            // Create cached pages.
            string[] currentAccessTokenPages = new string[]
            {
                "CurrentAccessTokenRawPage",
                "CurrentAccessTokenDecodedHeaderPage",
                "CurrentAccessTokenDecodedClaimPage",
                "CurrentAccessTokenDecodedReadableClaimPage",
                "CurrentAccessTokenDecodedSignaturePage"
            };

            foreach (string currentAccessTokenPage in currentAccessTokenPages)
            {
                string pageName = "Graph_Editor.Pages.AccessTokenWizard.CurrentAccessToken." + currentAccessTokenPage;

                if (!pageCache.TryGetValue(currentAccessTokenPage, out Page page))
                {
                    var pageType = Type.GetType(pageName);
                    page = (Page)Activator.CreateInstance(pageType);
                    pageCache[currentAccessTokenPage] = page;
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            GraphEditorApplication.UpdateStatusBarMainStatus("");

            if (EditorAccessToken.Instance.AuthenticationResult == null)
            {
                // Hide current access token information
                Grid_AuthResultNotExist.Visibility = Visibility.Visible;
                Grid_AuthResultExist.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Show current access token information

                Grid_AuthResultNotExist.Visibility = Visibility.Collapsed;
                Grid_AuthResultExist.Visibility = Visibility.Visible;

                // Prepare all current access token information pages

                // Raw access token
                string pageName = "CurrentAccessTokenRawPage";
                CurrentAccessTokenRawPage currentAccessTokenRawPage = pageCache[pageName] as CurrentAccessTokenRawPage;
                currentAccessTokenRawPage.RawAccessToken.Text = EditorAccessToken.Instance.AuthenticationResult.AccessToken;

                // Decode access token and split into 3 parts

                string decodedHeader = "Failed to decode.";
                string decodedClaim = "Failed to decode.";
                string decodedReadableClaim = "Failed to decode.";
                string decodedSignature = "Failed to decode.";

                Tuple<bool, string, string, string, string> decodedAccessToken = TryDecodeToken(EditorAccessToken.Instance.AuthenticationResult.AccessToken);
                if (decodedAccessToken != null && decodedAccessToken.Item1 == true) {
                    decodedHeader = decodedAccessToken.Item2;
                    decodedClaim = decodedAccessToken.Item3;
                    decodedReadableClaim = decodedAccessToken.Item4;
                    decodedSignature = decodedAccessToken.Item5;
                }

                // Decoded header
                pageName = "CurrentAccessTokenDecodedHeaderPage";
                CurrentAccessTokenDecodedHeaderPage currentAccessTokenDecodedHeaderPage = pageCache[pageName] as CurrentAccessTokenDecodedHeaderPage;
                currentAccessTokenDecodedHeaderPage.DecodedHeader.Editor.EndAtLastLine = true;
                currentAccessTokenDecodedHeaderPage.DecodedHeader.Editor.ReadOnly = false;
                currentAccessTokenDecodedHeaderPage.DecodedHeader.Editor.SetText(decodedHeader);
                currentAccessTokenDecodedHeaderPage.DecodedHeader.Editor.ReadOnly = true;

                // Decoded claim
                pageName = "CurrentAccessTokenDecodedClaimPage";
                CurrentAccessTokenDecodedClaimPage currentAccessTokenDecodedClaimPage = pageCache[pageName] as CurrentAccessTokenDecodedClaimPage;
                currentAccessTokenDecodedClaimPage.DecodedClaim.Editor.EndAtLastLine = true;
                currentAccessTokenDecodedClaimPage.DecodedClaim.Editor.ReadOnly = false;
                currentAccessTokenDecodedClaimPage.DecodedClaim.Editor.SetText(decodedClaim);
                currentAccessTokenDecodedClaimPage.DecodedClaim.Editor.ReadOnly = true;

                // Decoded claim
                pageName = "CurrentAccessTokenDecodedReadableClaimPage";
                CurrentAccessTokenDecodedReadableClaimPage currentAccessTokenDecodedReadableClaimPage = pageCache[pageName] as CurrentAccessTokenDecodedReadableClaimPage;
                currentAccessTokenDecodedReadableClaimPage.DecodedReadableClaim.Editor.EndAtLastLine = true;
                currentAccessTokenDecodedReadableClaimPage.DecodedReadableClaim.Editor.ReadOnly = false;
                currentAccessTokenDecodedReadableClaimPage.DecodedReadableClaim.Editor.SetText(decodedReadableClaim);
                currentAccessTokenDecodedReadableClaimPage.DecodedReadableClaim.Editor.ReadOnly = true;

                // Decoded signature
                pageName = "CurrentAccessTokenDecodedSignaturePage";
                CurrentAccessTokenDecodedSignaturePage currentAccessTokenDecodedSignaturePage = pageCache[pageName] as CurrentAccessTokenDecodedSignaturePage;
                currentAccessTokenDecodedSignaturePage.DecodedSignature.Text = decodedSignature;

                // Show the raw page
                NavigationView_CurrentAccessToken.SelectedItem = NavigationView_CurrentAccessToken.MenuItems[0];
            }
        }

        private void NavigationView_CurrentAccessToken_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                string selectedItemTag = ((string)selectedItem.Tag);
                string pageName = "Graph_Editor.Pages.AccessTokenWizard.CurrentAccessToken." + selectedItemTag;

                if (!pageCache.TryGetValue(selectedItemTag, out Page page))
                {
                    var pageType = Type.GetType(pageName);
                    page = (Page)Activator.CreateInstance(pageType);
                    pageCache[selectedItemTag] = page;
                }

                Frame_CurrentAccessToken.Content = page;
            }
        }

        private Tuple<bool, string, string, string, string> TryDecodeToken(string Token)
        {
            bool result = true;
            string decodedReadableClaim = "";
            try
            {
                string[] tokenParts = Token.Split('.');

                if (tokenParts.Length < 2)
                {
                    return new Tuple<bool, string, string, string, string>(false, "", "", "", "");
                }

                if (!GraphEditorApplication.TryParseJson(Encoding.UTF8.GetString(ConvertFromBase64String(tokenParts[0])), out string decodedHeader))
                {
                    decodedHeader = "";
                    result = false;
                }

                if (!GraphEditorApplication.TryParseJson(Encoding.UTF8.GetString(ConvertFromBase64String(tokenParts[1])), out string decodedClaim))
                {
                    decodedClaim = "";
                    result = false;
                }
                else
                {
                    // Prepare the readable version
                    decodedReadableClaim = ConvertToReadableClaim(decodedClaim);
                }

                string decodedSignature = BitConverter.ToString(ConvertFromBase64String(tokenParts[2]));

                return new Tuple<bool, string, string, string, string>(result, decodedHeader, decodedClaim, decodedReadableClaim, decodedSignature);
            }
            catch
            {
                return new Tuple<bool, string, string, string,  string>(false, "", "", "", "");
            }
        }

        private byte[] ConvertFromBase64String(string Data)
        {
            string temp = Data.Replace('-', '+').Replace('_', '/');

            switch (temp.Length % 4)
            {
                case 0:
                    break;
                case 1:
                    break;
                case 2:
                    temp += "==";
                    break;
                case 3:
                    temp += "=";
                    break;
                default:
                    throw new Exception();
            }

            return Convert.FromBase64String(temp);
        }

        private string ConvertToReadableClaim(string RawClaim)
        {
            // Convert claims into readable string
            // See https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference

            var rawClaimDictionary = new Dictionary<string, object>();
            var readableClaimDictionary = new Dictionary<string, object>();

            try
            {
                rawClaimDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(RawClaim);

                foreach (KeyValuePair<string, object> item in rawClaimDictionary)
                {
                    string description = "";
                    object value = null;

                    switch (item.Key)
                    {
                        case "acrs":
                            description = "Auth Context IDs of the operations that the bearer is eligible to perform";
                            value = item.Value;
                            break;
                        case "aud":
                            description = "Intended audience of the token";
                            value = item.Value;
                            break;
                        case "iss":
                            description = "STS that constructs and returns the token, and the Microsoft Entra tenant of the authenticated user";
                            value = item.Value;
                            break;
                        case "idp":
                            description = "Identity provider that authenticated the subject of the token";
                            value = item.Value;
                            break;
                        case "iat":
                            description = "When the authentication for this token occurred";
                            value = CalculateDate(item.Value.ToString());
                            break;
                        case "nbf":
                            description = "the time after which the JWT can be processed";
                            value = CalculateDate(item.Value.ToString());
                            break;
                        case "exp":
                            description = "The expiration time before which the JWT can be accepted for processing";
                            value = CalculateDate(item.Value.ToString());
                            break;
                        case "amr":
                            description = "The authentication method of the subject of the token";
                            if (item.Value != null)
                            {
                                switch (item.Value.ToString())
                                {
                                    case "pwd":
                                        value = "Password authentication, either a user's Microsoft password or a client secret of an application";
                                        break;
                                    case "rsa":
                                        value = "Authentication was based on the proof of an RSA key";
                                        break;
                                    case "otp":
                                        value = "One-time passcode using an email or a text message";
                                        break;
                                    case "fed":
                                        value = "Federated authentication assertion (such as JWT or SAML)";
                                        break;
                                    case "wia":
                                        value = "Windows Integrated Authentication";
                                        break;
                                    case "mfa":
                                        value = "Multifactor authentication";
                                        break;
                                    case "ngcmfa":
                                        value = "Multifactor authentication (Used for provisioning of certain advanced credential types)";
                                        break;
                                    case "wiaormfa":
                                        value = "Windows or an MFA credential";
                                        break;
                                    case "none":
                                        value = "no completed authentication";
                                        break;
                                    default:
                                        value = item.Value;
                                        break;
                                }
                            }
                            else
                            {
                                value = item.Value;
                            }
                            break;
                        case "appid":
                            description = "Application ID of the client using the token";
                            value = item.Value;
                            break;
                        case "azp":
                            description = "Application ID of the client using the token";
                            value = item.Value;
                            break;
                        case "appidacr":
                        case "azpacr":
                            description = "Authentication method of the client";
                            if (item.Value != null)
                            {
                                switch (item.Value.ToString())
                                {
                                    case "0":
                                        value = "Public client";
                                        break;
                                    case "1":
                                        value = "Client ID and client secret";
                                        break;
                                    case "2":
                                        value = "Client certificate";
                                        break;
                                    default:
                                        value = item.Value;
                                        break;
                                }
                            }
                            else
                            {
                                value = item.Value;
                            }

                            break;
                        case "preferred_username":
                            description = "The primary username that represents the user";
                            value = item.Value;
                            break;
                        case "name":
                            description = "A human-readable value that identifies the subject of the token";
                            value = item.Value;
                            break;
                        case "scp":
                            description = "The set of scopes exposed by the application for which the client application has requested (and received) consent";
                            value = item.Value;
                            break;
                        case "roles":
                            description = "The set of permissions exposed by the application that the requesting application or user has been given permission to call";
                            value = item.Value;
                            break;
                        case "wids":
                            description = "The tenant-wide roles assigned to this user, from the section of roles present in Microsoft Entra built-in roles";
                            value = item.Value;
                            break;
                        case "groups":
                            description = "Object IDs that represent the group memberships of the subject";
                            value = item.Value;
                            break;
                        case "hasgroups":
                            description = "Whether the user is in at least one group";
                            value = item.Value;
                            break;
                        case "sub":
                            description = "The principal associated with the token";
                            value = item.Value;
                            break;
                        case "oid":
                            description = "The immutable identifier for the requestor, which is the verified identity of the user or service principal";
                            value = item.Value;
                            break;
                        case "tid":
                            description = "The tenant that the user is signing in to";
                            value = item.Value;
                            break;
                        case "unique_name":
                            description = "A human readable value that identifies the subject of the token";
                            value = item.Value;
                            break;
                        case "uti":
                            description = "Token identifier claim, equivalent to jti in the JWT specification";
                            value = item.Value;
                            break;
                        case "ver":
                            description = "The version of the access token";
                            value = item.Value;
                            break;
                        case "xms_cc":
                            description = "Whether the client application that acquired the token is capable of handling claims challenges";
                            value = item.Value;
                            break;

                        case "ipaddr":
                            description = "The IP address the user authenticated from";
                            value = item.Value;
                            break;
                        case "onprem_sid":
                            description = "In cases where the user has an on-premises authentication, this claim provides their SID";
                            value = item.Value;
                            break;
                        case "pwd_exp":
                            description = "When the user's password expires";
                            value = CalculateDate(item.Value.ToString());
                            break;
                        case "pwd_url":
                            description = "A URL where users can reset their password";
                            value = item.Value;
                            break;
                        case "in_corp":
                            description = "Signals if the client is signing in from the corporate network";
                            value = item.Value;
                            break;
                        case "nickname":
                            description = "Another name for the user, separate from first or last name";
                            value = item.Value;
                            break;
                        case "family_name":
                            description = "The last name, surname, or family name of the user as defined on the user object";
                            value = item.Value;
                            break;
                        case "given_name":
                            description = "The first or given name of the user, as set on the user object.";
                            value = item.Value;
                            break;
                        case "upn":
                            description = "The username of the user";
                            value = item.Value;
                            break;
                        case "typ":
                            description = "Type";
                            value = item.Value;
                            break;
                        case "alg":
                            description = "Algorithm";
                            value = item.Value;
                            break;
                        case "x5t":
                            description = "X.509 certificate SHA-1 Thumbprint";
                            value = item.Value;
                            break;
                        case "kid":
                            description = "Key ID";
                            value = item.Value;
                            break;
                        default:
                            description = item.Key;
                            value = item.Value;
                            break;
                    }

                    if (readableClaimDictionary.ContainsKey(description))
                    {
                        description += " (" + item.Key + ")";
                    }

                    readableClaimDictionary.Add(description, value);
                }

                string result = JsonConvert.SerializeObject(readableClaimDictionary, Formatting.Indented);
                return result;
            }
            catch (JsonException)
            {
                return RawClaim;
            }
        }

        private string CalculateDate(string Seconds)
        {
            DateTime baseDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            if (long.TryParse(Seconds, out long secondsAfterBase))
            {
                return baseDate.AddSeconds(secondsAfterBase).ToString("yyyy/MM/dd HH:mm:ss") + " (UTC)";
            }
            else
            {
                return Seconds;
            }
        }
    }
}
