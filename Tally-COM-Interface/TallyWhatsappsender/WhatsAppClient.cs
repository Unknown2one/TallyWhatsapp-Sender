using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using TallyWhatsappsender.Config;

namespace TallyWhatsappsender
{
    /// <summary>
    /// HTTP client for communicating with the Go WhatsApp bridge service.
    /// Replaces the old Selenium-based approach with lightweight REST API calls.
    /// </summary>
    public class WhatsAppClient
    {
        private readonly string _baseUrl;
        private readonly int _timeoutMs;
        private readonly int _maxRetries;
        private readonly int _retryDelayMs;
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public WhatsAppClient()
        {
            try
            {
                var settings = ConfigManager.Instance.WhatsAppSettings;
                _baseUrl = settings.BridgeUrl.TrimEnd('/');
                _timeoutMs = settings.ApiTimeout * 1000;
                _maxRetries = settings.MaxRetries;
                _retryDelayMs = settings.RetryDelay;
            }
            catch
            {
                // Use safe defaults if config fails to load
                _baseUrl = "http://localhost:8080";
                _timeoutMs = 30000;
                _maxRetries = 3;
                _retryDelayMs = 1000;
            }
        }

        /// <summary>
        /// Send a text message to a recipient.
        /// </summary>
        public ApiResult SendMessage(string recipient, string message)
        {
            var body = _json.Serialize(new { recipient = recipient, message = message });
            return PostWithRetry("/api/send-message", body);
        }

        /// <summary>
        /// Send a file (PDF, image, etc.) to a recipient with an optional caption.
        /// </summary>
        public ApiResult SendFile(string recipient, string filePath, string caption)
        {
            var body = _json.Serialize(new { recipient = recipient, file_path = filePath, caption = caption ?? "" });
            return PostWithRetry("/api/send-file", body);
        }

        /// <summary>
        /// Send a file followed immediately by a text message.
        /// </summary>
        public ApiResult SendFileWithMessage(string recipient, string filePath, string message)
        {
            var body = _json.Serialize(new { recipient = recipient, file_path = filePath, message = message });
            return PostWithRetry("/api/send-file-with-message", body);
        }

        /// <summary>
        /// Check whether the WhatsApp bridge service is running and authenticated.
        /// </summary>
        public bool CheckHealth()
        {
            try
            {
                string url = _baseUrl + "/api/health";
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 5000; // quick health check

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            string content = reader.ReadToEnd();
                            var result = _json.DeserializeObject(content) as System.Collections.Generic.Dictionary<string, object>;
                            if (result != null && result.ContainsKey("authenticated"))
                            {
                                return (bool)result["authenticated"];
                            }
                        }
                    }
                }
            }
            catch
            {
                // Service unavailable
            }
            return false;
        }

        // ─── Private helpers ─────────────────────────────────────────────────────

        private ApiResult PostWithRetry(string endpoint, string jsonBody)
        {
            ApiResult lastResult = null;

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    lastResult = Post(endpoint, jsonBody);
                    if (lastResult.Success)
                        return lastResult;

                    // Don't retry on client errors (4xx)
                    if (lastResult.StatusCode >= 400 && lastResult.StatusCode < 500)
                        return lastResult;
                }
                catch (WebException ex)
                {
                    lastResult = new ApiResult
                    {
                        Success = false,
                        Message = "Connection error: " + ex.Message,
                        StatusCode = 0
                    };
                }
                catch (Exception ex)
                {
                    lastResult = new ApiResult
                    {
                        Success = false,
                        Message = "Unexpected error: " + ex.Message,
                        StatusCode = 0
                    };
                }

                if (attempt < _maxRetries)
                {
                    System.Threading.Thread.Sleep(_retryDelayMs * attempt); // exponential backoff
                }
            }

            return lastResult ?? new ApiResult { Success = false, Message = "Unknown error after all retries" };
        }

        private ApiResult Post(string endpoint, string jsonBody)
        {
            string url = _baseUrl + endpoint;
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = bodyBytes.Length;
            request.Timeout = _timeoutMs;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }

            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                var errorResp = ex.Response as HttpWebResponse;
                if (errorResp != null)
                {
                    // Read error response body
                    string errorBody = "";
                    using (var reader = new StreamReader(errorResp.GetResponseStream()))
                        errorBody = reader.ReadToEnd();

                    return ParseResponse(errorBody, (int)errorResp.StatusCode);
                }
                throw;
            }

            using (response)
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string body = reader.ReadToEnd();
                return ParseResponse(body, (int)response.StatusCode);
            }
        }

        private ApiResult ParseResponse(string body, int statusCode)
        {
            try
            {
                var dict = _json.DeserializeObject(body) as System.Collections.Generic.Dictionary<string, object>;
                if (dict != null)
                {
                    bool success = dict.ContainsKey("success") && (bool)dict["success"];
                    string message = dict.ContainsKey("message") && dict["message"] != null ? dict["message"].ToString() : "";
                    string error   = dict.ContainsKey("error") && dict["error"] != null ? dict["error"].ToString() : "";
                    string msgId   = dict.ContainsKey("message_id") && dict["message_id"] != null ? dict["message_id"].ToString() : "";

                    return new ApiResult
                    {
                        Success    = success,
                        Message    = success ? message : (string.IsNullOrEmpty(error) ? message : error),
                        MessageId  = msgId,
                        StatusCode = statusCode
                    };
                }
            }
            catch { /* fall through */ }

            return new ApiResult
            {
                Success    = statusCode >= 200 && statusCode < 300,
                Message    = body,
                StatusCode = statusCode
            };
        }
    }

    /// <summary>
    /// Result of a WhatsApp API call.
    /// </summary>
    public class ApiResult
    {
        public bool   Success    { get; set; }
        public string Message    { get; set; }
        public string MessageId  { get; set; }
        public int    StatusCode { get; set; }
    }
}
