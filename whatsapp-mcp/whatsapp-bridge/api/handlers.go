package api

import (
	"encoding/json"
	"fmt"
	"net/http"
	"time"

	"go.mau.fi/whatsmeow"
	"whatsapp-client/validation"
)

// MessageStore interface for accessing message store methods
type MessageStore interface {
	// Add methods as needed
}

// Handler holds the dependencies for API handlers
type Handler struct {
	Client       *whatsmeow.Client
	MessageStore MessageStore
	LastPing     time.Time
	Validator    *validation.Validator
}

// NewHandler creates a new API handler
func NewHandler(client *whatsmeow.Client, messageStore MessageStore, validator *validation.Validator) *Handler {
	return &Handler{
		Client:       client,
		MessageStore: messageStore,
		LastPing:     time.Now(),
		Validator:    validator,
	}
}

// SendMessage handles POST /api/send-message
func (h *Handler) SendMessage(w http.ResponseWriter, r *http.Request) {
	var req SendMessageRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, "Invalid request format", http.StatusBadRequest)
		return
	}

	if req.Recipient == "" {
		respondError(w, "Recipient is required", http.StatusBadRequest)
		return
	}

	if req.Message == "" {
		respondError(w, "Message is required", http.StatusBadRequest)
		return
	}

	messageID, err := sendTextMessage(h.Client, req.Recipient, req.Message)
	if err != nil {
		respondError(w, fmt.Sprintf("Failed to send message: %v", err), http.StatusInternalServerError)
		return
	}

	respondJSON(w, SendMessageResponse{
		Success:   true,
		Message:   "Message sent successfully",
		MessageID: messageID,
	})
}

// SendFile handles POST /api/send-file
func (h *Handler) SendFile(w http.ResponseWriter, r *http.Request) {
	var req SendFileRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, "Invalid request format", http.StatusBadRequest)
		return
	}

	if req.Recipient == "" {
		respondError(w, "Recipient is required", http.StatusBadRequest)
		return
	}

	if req.FilePath == "" {
		respondError(w, "FilePath is required", http.StatusBadRequest)
		return
	}

	// Validate the file using the validator
	if h.Validator != nil {
		if err := h.Validator.ValidateComplete(req.FilePath); err != nil {
			// Check if it's a ValidationError and respond with appropriate details
			if validErr, ok := err.(*validation.ValidationError); ok {
				respondValidationError(w, validErr)
				return
			}
			respondError(w, fmt.Sprintf("File validation failed: %v", err), http.StatusBadRequest)
			return
		}
	}

	messageID, err := sendFileMessage(h.Client, req.Recipient, req.FilePath, req.Caption)
	if err != nil {
		respondError(w, fmt.Sprintf("Failed to send file: %v", err), http.StatusInternalServerError)
		return
	}

	respondJSON(w, SendMessageResponse{
		Success:   true,
		Message:   "File sent successfully",
		MessageID: messageID,
	})
}

// SendFileWithMessage handles POST /api/send-file-with-message
func (h *Handler) SendFileWithMessage(w http.ResponseWriter, r *http.Request) {
	var req SendFileWithMessageRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, "Invalid request format", http.StatusBadRequest)
		return
	}

	if req.Recipient == "" {
		respondError(w, "Recipient is required", http.StatusBadRequest)
		return
	}

	if req.FilePath == "" {
		respondError(w, "FilePath is required", http.StatusBadRequest)
		return
	}

	if req.Message == "" {
		respondError(w, "Message is required", http.StatusBadRequest)
		return
	}

	// Validate the file using the validator
	if h.Validator != nil {
		if err := h.Validator.ValidateComplete(req.FilePath); err != nil {
			// Check if it's a ValidationError and respond with appropriate details
			if validErr, ok := err.(*validation.ValidationError); ok {
				respondValidationError(w, validErr)
				return
			}
			respondError(w, fmt.Sprintf("File validation failed: %v", err), http.StatusBadRequest)
			return
		}
	}

	// Send file first (without caption)
	fileMessageID, err := sendFileMessage(h.Client, req.Recipient, req.FilePath, "")
	if err != nil {
		respondError(w, fmt.Sprintf("Failed to send file: %v", err), http.StatusInternalServerError)
		return
	}

	// Then send the text message
	textMessageID, err := sendTextMessage(h.Client, req.Recipient, req.Message)
	if err != nil {
		respondError(w, fmt.Sprintf("File sent but failed to send message: %v", err), http.StatusInternalServerError)
		return
	}

	respondJSON(w, SendMessageResponse{
		Success:   true,
		Message:   fmt.Sprintf("File and message sent successfully (file: %s, text: %s)", fileMessageID, textMessageID),
		MessageID: textMessageID,
	})
}

// Health handles GET /api/health
func (h *Handler) Health(w http.ResponseWriter, r *http.Request) {
	h.LastPing = time.Now()
	
	status := "disconnected"
	authenticated := false

	if h.Client.IsConnected() {
		status = "connected"
	}

	if h.Client.IsLoggedIn() {
		authenticated = true
	}

	respondJSON(w, HealthResponse{
		Status:        status,
		Authenticated: authenticated,
		LastPing:      h.LastPing,
	})
}

// Status handles GET /api/status
func (h *Handler) Status(w http.ResponseWriter, r *http.Request) {
	h.LastPing = time.Now()

	response := StatusResponse{
		Connected:     h.Client.IsConnected(),
		Authenticated: h.Client.IsLoggedIn(),
		LastPing:      h.LastPing,
	}

	// Get additional details if authenticated
	if h.Client.IsLoggedIn() {
		store := h.Client.Store
		if store != nil && store.ID != nil {
			response.PhoneNumber = store.ID.User
			response.DeviceName = fmt.Sprintf("Device %d", store.ID.Device)
		}
	}

	respondJSON(w, response)
}

// Helper function to send text messages
func sendTextMessage(client *whatsmeow.Client, recipient, message string) (string, error) {
	return SendTextMessage(client, recipient, message)
}

// Helper function to send file messages
func sendFileMessage(client *whatsmeow.Client, recipient, filePath, caption string) (string, error) {
	return SendFileMessage(client, recipient, filePath, caption)
}

// Helper function to respond with JSON
func respondJSON(w http.ResponseWriter, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(data)
}

// Helper function to respond with error
func respondError(w http.ResponseWriter, message string, statusCode int) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(statusCode)
	json.NewEncoder(w).Encode(ErrorResponse{
		Success: false,
		Error:   message,
	})
}

// Helper function to respond with validation error
func respondValidationError(w http.ResponseWriter, err *validation.ValidationError) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(err.StatusCode)
	json.NewEncoder(w).Encode(err.ToJSON())
}
