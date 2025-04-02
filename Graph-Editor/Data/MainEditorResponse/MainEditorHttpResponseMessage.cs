using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Graph_Editor.Data.MainEditorResponse
{
    public class MainEditorHttpResponseMessage
    {
        private HttpResponseMessage _httpResponseMessage;
        private bool _isAnalyzed = false;
        private string _parsedResponseHeader = "";
        private string _parsedResponseBodyString = "";
        private Stream _parsedResponseBodyStream;
        private string _parsedBase64BodyString = "";
        private BitmapImage _parsedResponseBodyBitmapImage;

        public MainEditorHttpResponseMessage()
        {
            RequestSucceeded = false;
        }

        public MainEditorHttpResponseMessage(HttpResponseMessage HttpResponseMessage, bool Succeeded )
        {
            _httpResponseMessage = HttpResponseMessage;
            RequestSucceeded = Succeeded;
        }

        /// <summary>
        /// Indicate whether the request was succeeded or not.
        /// </summary>
        public bool RequestSucceeded { get; set; }

        /// <summary>
        /// Internal message about the response.
        /// </summary>
        public string InternalMessage { get; set; }

        public bool IsAnalyzed => _isAnalyzed;

        public string ParsedResponseHeader => _parsedResponseHeader;

        public string ParsedResponseBodyString => _parsedResponseBodyString;

        public Stream ParsedResponseBodyStream => _parsedResponseBodyStream;

        public string ParsedBase64BodyString => _parsedBase64BodyString;

        public BitmapImage ParsedResponseBodyBitmapImage => _parsedResponseBodyBitmapImage;

        public HttpStatusCode StatusCode
        {
            get
            {
                return _httpResponseMessage.StatusCode;
            }
        }

        public string ContentType
        {
            get
            {
                if (_httpResponseMessage.Content?.Headers?.ContentType != null)
                {
                    return _httpResponseMessage.Content.Headers.ContentType.MediaType;
                }

                return string.Empty;
            }
        }

        public async Task AnalyzeAsync()
        {
            _parsedResponseHeader = _httpResponseMessage.Content.Headers.ToString() + System.Environment.NewLine + _httpResponseMessage.Headers.ToString();

            _parsedResponseBodyString = await _httpResponseMessage.Content.ReadAsStringAsync();

            _parsedResponseBodyStream = await _httpResponseMessage.Content.ReadAsStreamAsync();

            // Convert _parsedResponseBodyStream to Base64 string
            if (_parsedResponseBodyStream != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    _parsedResponseBodyStream.CopyTo(memoryStream);
                    _parsedBase64BodyString = Convert.ToBase64String(memoryStream.ToArray());
                }
            }

            try
            {
                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(_parsedResponseBodyStream.AsRandomAccessStream());
                _parsedResponseBodyBitmapImage = bitmapImage;
            }
            catch
            {
                _parsedResponseBodyBitmapImage = null;
            }

            _isAnalyzed = true;
        }

        public Dictionary<string, string> GetHeaderDictionary()
        {
            var headers = new Dictionary<string, string>();

            foreach (var contentHeader in _httpResponseMessage.Content.Headers)
            {
                headers.Add(contentHeader.Key, string.Join(" ", contentHeader.Value));
            }

            foreach (var header in _httpResponseMessage.Headers)
            {
                headers.Add(header.Key, string.Join(" ", header.Value));
            }

            return headers;
        }
    }
}
