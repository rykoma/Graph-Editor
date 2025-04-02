using Graph_Editor.Data.MainEditorResponse;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Web.Http;
using HttpClient = System.Net.Http.HttpClient;
using HttpResponseMessage = System.Net.Http.HttpResponseMessage;

namespace Graph_Editor.Logic
{
    public class MainEditorHttpClient
    {
        private static readonly HttpClient _httpClient;
        private static readonly string ResourceFileName = "Logic.MainEditorHttpClient";

        static MainEditorHttpClient()
        {
            _httpClient = new HttpClient(new MainEditorLoggingHandler() { AllowAutoRedirect = false });
        }

        public async Task<MainEditorHttpResponseMessage> SendGetRequestAsync(string URL, Dictionary<string, string> Headers = null)
        {
            try
            {
                ValidateRequestUrl(URL);
                PrepareHeaders(Headers);

                HttpResponseMessage httpResponse;
                
                httpResponse = await _httpClient.GetAsync(URL);

                MainEditorHttpResponseMessage editorHttpResponse = new MainEditorHttpResponseMessage(httpResponse, true);
                await editorHttpResponse.AnalyzeAsync();

                return editorHttpResponse;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return new MainEditorHttpResponseMessage(null, false) { InternalMessage = ex.Message };
            }
        }

        public async Task<MainEditorHttpResponseMessage> SendPostRequestAsync(string URL, Dictionary<string, string> Headers = null, string Body = "")
        {
            try
            {
                ValidateRequestUrl(URL);
                PrepareHeaders(Headers);

                HttpResponseMessage httpResponse;
                
                httpResponse = await _httpClient.PostAsync(URL, new StringContent(Body, Encoding.UTF8, GetMediaType(Headers)));

                MainEditorHttpResponseMessage editorHttpResponse = new MainEditorHttpResponseMessage(httpResponse, true);
                await editorHttpResponse.AnalyzeAsync();

                return editorHttpResponse;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return new MainEditorHttpResponseMessage(null, false) { InternalMessage = ex.Message };
            }
        }

        public async Task<MainEditorHttpResponseMessage> SendPostRequestAsync(string URL, Dictionary<string, string> Headers = null, Stream Body = null)
        {
            try
            {
                ValidateRequestUrl(URL);
                PrepareHeaders(Headers);

                HttpResponseMessage httpResponse;

                var streamContent = new StreamContent(Body);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMediaType(Headers));
                httpResponse = await _httpClient.PostAsync(URL, streamContent);

                MainEditorHttpResponseMessage editorHttpResponse = new MainEditorHttpResponseMessage(httpResponse, true);
                await editorHttpResponse.AnalyzeAsync();

                return editorHttpResponse;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return new MainEditorHttpResponseMessage(null, false) { InternalMessage = ex.Message };
            }
        }

        public async Task<MainEditorHttpResponseMessage> SendPutRequestAsync(string URL, Dictionary<string, string> Headers = null, string Body = "")
        {
            try
            {
                ValidateRequestUrl(URL);
                PrepareHeaders(Headers);

                HttpResponseMessage httpResponse;

                httpResponse = await _httpClient.PutAsync(URL, new StringContent(Body, Encoding.UTF8, GetMediaType(Headers)));
                
                MainEditorHttpResponseMessage editorHttpResponse = new MainEditorHttpResponseMessage(httpResponse, true);
                await editorHttpResponse.AnalyzeAsync();

                return editorHttpResponse;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return new MainEditorHttpResponseMessage(null, false) { InternalMessage = ex.Message };
            }
        }

        public async Task<MainEditorHttpResponseMessage> SendPutRequestAsync(string URL, Dictionary<string, string> Headers = null, Stream Body = null)
        {
            try
            {
                ValidateRequestUrl(URL);
                PrepareHeaders(Headers);

                HttpResponseMessage httpResponse;

                var streamContent = new StreamContent(Body);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMediaType(Headers));
                httpResponse = await _httpClient.PutAsync(URL, streamContent);

                MainEditorHttpResponseMessage editorHttpResponse = new MainEditorHttpResponseMessage(httpResponse, true);
                await editorHttpResponse.AnalyzeAsync();

                return editorHttpResponse;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return new MainEditorHttpResponseMessage(null, false) { InternalMessage = ex.Message };
            }
        }

        public async Task<MainEditorHttpResponseMessage> SendPatchRequestAsync(string URL, Dictionary<string, string> Headers = null, string Body = "")
        {
            try
            {
                ValidateRequestUrl(URL);
                PrepareHeaders(Headers);

                HttpResponseMessage httpResponse;

                httpResponse = await _httpClient.PatchAsync(URL, new StringContent(Body, Encoding.UTF8, GetMediaType(Headers)));

                MainEditorHttpResponseMessage editorHttpResponse = new MainEditorHttpResponseMessage(httpResponse, true);
                await editorHttpResponse.AnalyzeAsync();

                return editorHttpResponse;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return new MainEditorHttpResponseMessage(null, false) { InternalMessage = ex.Message };
            }
        }

        public async Task<MainEditorHttpResponseMessage> SendPatchRequestAsync(string URL, Dictionary<string, string> Headers = null, Stream Body = null)
        {
            try
            {
                ValidateRequestUrl(URL);
                PrepareHeaders(Headers);

                HttpResponseMessage httpResponse;

                var streamContent = new StreamContent(Body);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMediaType(Headers));
                httpResponse = await _httpClient.PatchAsync(URL, streamContent);

                MainEditorHttpResponseMessage editorHttpResponse = new MainEditorHttpResponseMessage(httpResponse, true);
                await editorHttpResponse.AnalyzeAsync();

                return editorHttpResponse;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return new MainEditorHttpResponseMessage(null, false) { InternalMessage = ex.Message };
            }
        }

        public async Task<MainEditorHttpResponseMessage> SendDeleteRequestAsync(string URL, Dictionary<string, string> Headers = null)
        {
            try
            {
                ValidateRequestUrl(URL);
                PrepareHeaders(Headers);

                HttpResponseMessage httpResponse;

                httpResponse = await _httpClient.DeleteAsync(URL);

                MainEditorHttpResponseMessage editorHttpResponse = new MainEditorHttpResponseMessage(httpResponse, true);
                await editorHttpResponse.AnalyzeAsync();

                return editorHttpResponse;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return new MainEditorHttpResponseMessage(null, false) { InternalMessage = ex.Message };
            }
        }

        private void ValidateRequestUrl(string URL)
        {
            // Check if the URL starts with "https://"
            if (URL.StartsWith("https://") == false)
            {
                throw new Exception(GraphEditorApplication.GetResourceString(ResourceFileName, "Message_CouldNotSendRequestRequestUrlFormatError"));
            }
        }

        private void PrepareHeaders(Dictionary<string, string> Headers = null)
        {
            _httpClient.DefaultRequestHeaders.Clear(); // Remove all headers
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/plain");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Data.EditorAccessToken.EditorAccessToken.Instance.AuthenticationResult.AccessToken);

            if (Headers != null)
            {
                foreach (var header in Headers)
                {
                    if (header.Key == "")
                    {
                        if (header.Value == "")
                        {
                            // Ignore
                            continue;
                        }

                        throw new Exception(GraphEditorApplication.GetResourceString(ResourceFileName, "Message_CouldNotSendRequestRequestHeaderNameMustNotBeBlank"));
                    }

                    if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception(GraphEditorApplication.GetResourceString(ResourceFileName, "Message_CouldNotSendRequestRequestAuthorizationHeaderExist"));
                    }


                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ignore
                        // Content-Type header will be used in HttpContent later
                        continue;
                    }

                    _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }
        }

        private string GetMediaType(Dictionary<string, string> Headers = null)
        {
            if (Headers != null && Headers.ContainsKey("Content-Type"))
            {
                return Headers["Content-Type"];
            }
            else
            {
                return "application/json";
            }
        }
    }
}
