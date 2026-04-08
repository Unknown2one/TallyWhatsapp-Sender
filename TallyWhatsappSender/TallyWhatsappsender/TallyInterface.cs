using System;
using System.Runtime.InteropServices;

namespace TallyWhatsappsender
{
    /// <summary>
    /// COM-visible interface for Tally WhatsApp integration
    /// Maintains backward compatibility while adding new features
    /// </summary>
    [ComVisible(true)]
    [Guid("2B8E9F1C-A3D4-4F2B-9C7E-1234567890AB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface ITallyWhatsAppSender
    {
        /// <summary>
        /// Legacy method - maintains backward compatibility with existing TDL scripts
        /// </summary>
        [DispId(1)]
        string InitProcess(string contact, string file_route, string title, string chrome_binary);

        /// <summary>
        /// Send message with file attachment using new HTTP API
        /// </summary>
        [DispId(2)]
        string SendFileWithMessage(string contact, string file_path, string message);

        /// <summary>
        /// Send text message only
        /// </summary>
        [DispId(3)]
        string SendMessage(string contact, string message);

        /// <summary>
        /// Send file with caption
        /// </summary>
        [DispId(4)]
        string SendFile(string contact, string file_path, string caption);

        /// <summary>
        /// Send message using template
        /// </summary>
        [DispId(5)]
        string SendTemplateMessage(string contact, string templateName, string templateData);

        /// <summary>
        /// Check if WhatsApp service is connected
        /// </summary>
        [DispId(6)]
        bool IsServiceAvailable();

        /// <summary>
        /// Get last error message
        /// </summary>
        [DispId(7)]
        string GetLastError();
    }

    /// <summary>
    /// Main implementation class for Tally WhatsApp integration
    /// Uses HTTP API to communicate with Go WhatsApp bridge
    /// </summary>
    [ComVisible(true)]
    [Guid("3C9F0E2D-B4E5-4A3C-0D8F-2345678901BC")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("TallyWhatsappsender.WhatsAppSender")]
    public class WhatsAppSender : ITallyWhatsAppSender
    {
        private string _lastError;
        private readonly WhatsAppClient _client;

        public WhatsAppSender()
        {
            _lastError = string.Empty;
            _client = new WhatsAppClient();
        }

        /// <summary>
        /// Legacy method for backward compatibility with old TDL scripts
        /// Now uses HTTP API instead of Selenium
        /// </summary>
        public string InitProcess(string contact, string file_route, string title, string chrome_binary)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(contact))
                {
                    return "Error: Contact number is required";
                }

                if (string.IsNullOrEmpty(file_route))
                {
                    return "Error: File path is required";
                }

                // Fire and forget background sending to avoid freezing Tally
                System.Threading.ThreadPool.QueueUserWorkItem(state => {
                    SendFileWithMessage(contact, file_route, title);
                });

                return "Message queued for sending in background.";
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Send file with message to one or more contacts
        /// </summary>
        public string SendFileWithMessage(string contact, string file_path, string message)
        {
            try
            {
                if (!System.IO.File.Exists(file_path))
                {
                    _lastError = "File not found: " + file_path;
                    return "Error: Attachment not found!";
                }

                // Split contacts by comma for multiple recipients
                string[] contacts = contact.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                int successCount = 0;
                int failCount = 0;
                string lastError = "";

                foreach (string ct in contacts)
                {
                    string cleanContact = ct.Trim();
                    
                    // Validate contact format (12 digits with country code, no +)
                    if (cleanContact.Length != 12 || !IsNumeric(cleanContact))
                    {
                        failCount++;
                        lastError = "Invalid contact number: " + cleanContact;
                        continue;
                    }

                    // Send file
                    var fileResult = _client.SendFile(cleanContact, file_path, string.Empty);
                    if (!fileResult.Success)
                    {
                        failCount++;
                        lastError = fileResult.Message;
                        continue;
                    }

                    // Send message if provided
                    if (!string.IsNullOrEmpty(message))
                    {
                        var msgResult = _client.SendMessage(cleanContact, message);
                        if (!msgResult.Success)
                        {
                            failCount++;
                            lastError = msgResult.Message;
                            continue;
                        }
                    }

                    successCount++;
                }

                if (failCount > 0)
                {
                    _lastError = lastError;
                    return "Partial success: " + successCount + " sent, " + failCount + " failed. Last error: " + lastError;
                }

                return "Process finished: " + successCount + " message(s) sent successfully";
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Send text message only
        /// </summary>
        public string SendMessage(string contact, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(contact))
                {
                    return "Error: Contact number is required";
                }

                if (string.IsNullOrEmpty(message))
                {
                    return "Error: Message is required";
                }

                // Split contacts for multiple recipients
                string[] contacts = contact.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                int successCount = 0;
                int failCount = 0;
                string lastError = "";

                foreach (string ct in contacts)
                {
                    string cleanContact = ct.Trim();
                    
                    if (cleanContact.Length != 12 || !IsNumeric(cleanContact))
                    {
                        failCount++;
                        lastError = "Invalid contact number: " + cleanContact;
                        continue;
                    }

                    var result = _client.SendMessage(cleanContact, message);
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        lastError = result.Message;
                    }
                }

                if (failCount > 0)
                {
                    _lastError = lastError;
                    return "Partial success: " + successCount + " sent, " + failCount + " failed";
                }

                return "Success: " + successCount + " message(s) sent";
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Send file with optional caption
        /// </summary>
        public string SendFile(string contact, string file_path, string caption)
        {
            try
            {
                if (!System.IO.File.Exists(file_path))
                {
                    _lastError = "File not found: " + file_path;
                    return "Error: File not found!";
                }

                string[] contacts = contact.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                int successCount = 0;
                int failCount = 0;
                string lastError = "";

                foreach (string ct in contacts)
                {
                    string cleanContact = ct.Trim();
                    
                    if (cleanContact.Length != 12 || !IsNumeric(cleanContact))
                    {
                        failCount++;
                        lastError = "Invalid contact: " + cleanContact;
                        continue;
                    }

                    var result = _client.SendFile(cleanContact, file_path, caption);
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        lastError = result.Message;
                    }
                }

                if (failCount > 0)
                {
                    _lastError = lastError;
                    return "Partial success: " + successCount + " sent, " + failCount + " failed";
                }

                return "Success: " + successCount + " file(s) sent";
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Send message using predefined template
        /// </summary>
        public string SendTemplateMessage(string contact, string templateName, string templateData)
        {
            try
            {
                var template = MessageTemplates.GetTemplate(templateName);
                if (template == null)
                {
                    return "Error: Template '" + templateName + "' not found";
                }

                string message = template.ApplyData(templateData);
                return SendMessage(contact, message);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Check if WhatsApp service is available
        /// </summary>
        public bool IsServiceAvailable()
        {
            try
            {
                return _client.CheckHealth();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get last error message
        /// </summary>
        public string GetLastError()
        {
            return _lastError;
        }

        private bool IsNumeric(string value)
        {
            foreach (char c in value)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }
    }
}
