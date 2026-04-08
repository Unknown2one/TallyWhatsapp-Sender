# Copilot Instructions - Tally WhatsApp Sender

## Project Overview

This is a Tally ERP integration that sends invoices via WhatsApp using a three-layer architecture:
- **TDL (Tally Definition Language)**: UI layer in Tally
- **C# COM DLL**: Middle layer bridging Tally to WhatsApp
- **Go WhatsApp Bridge**: Backend service handling WhatsApp Web API

## Build & Test Commands

### WhatsApp Bridge (Go)
```bash
# Build
cd whatsapp-mcp\whatsapp-bridge
go build -o whatsapp-bridge.exe main.go

# Run
.\whatsapp-bridge.exe

# Test bridge health
curl http://localhost:8080/api/health
```

### C# DLL
```bash
# Build (from TallyWhatsappSender\TallyWhatsappsender\)
msbuild TallyWhatsappsender.csproj /p:Configuration=Release

# Register COM DLL (requires Admin)
cd C:\Windows\Microsoft.NET\Framework64\v4.0.30319
regasm "D:\whatstallysender\TallyWhatsappSender\Binary\TallyWhatsappsender.dll" /codebase

# Test registration
powershell -Command "New-Object -ComObject 'TallyWhatsappSender.WhatsAppAPI'"
```

### Integration Tests
```bash
# Full end-to-end test
.\test-integration.bat

# Start bridge manually
.\start-bridge.bat
```

## Architecture

### Communication Flow
```
Tally ERP
  ↓ (TDL calls COM)
TallyWhatsappsender.dll (C# COM)
  ↓ (HTTP POST to localhost:8080)
whatsapp-bridge.exe (Go + whatsmeow)
  ↓ (WhatsApp Web Protocol)
WhatsApp
```

### Key Components

**1. TDL Layer** (`TallyWhatsappSender\TDL\`)
- Entry point: `WhatsappSenderTally.txt`
- Triggers on Sales Voucher save
- Extracts party mobile number and voucher details
- Must be loaded in Tally via F12 → TDL & Add-ons

**2. C# COM DLL** (`TallyWhatsappSender\TallyWhatsappsender\`)
- ProgId: `TallyWhatsappSender.WhatsAppAPI`
- Main class: `TallyInterface.cs` (COM-visible entry point)
- HTTP client: `WhatsAppClient.cs` (talks to Go bridge)
- No Selenium/ChromeDriver dependency (replaced with HTTP)
- Must be registered with `regasm /codebase` to work with Tally

**3. Go Bridge** (`whatsapp-mcp\whatsapp-bridge\`)
- HTTP server on port 8080
- Uses `whatsmeow` library for WhatsApp Web protocol
- SQLite for session persistence (`store/whatsapp.db`)
- QR code authentication on first run
- Endpoints:
  - `POST /api/send-message` - Send text
  - `POST /api/send-file` - Send PDF/attachments
  - `GET /api/health` - Check auth status

## Key Conventions

### Mobile Number Format
- **Always 12 digits** with country code
- Format: `919876543210` (no `+`, spaces, or dashes)
- Multiple recipients: comma-separated `919876543210,919876543211`
- Validated in both DLL and bridge

### Configuration Files

**Bridge Config** (`config.json` at root):
```json
{
  "server": { "port": 8080, "host": "localhost" },
  "whatsapp": { "timeout_seconds": 60, "auto_reconnect": true },
  "files": { "max_size_mb": 100, "allowed_types": ["pdf", "jpg", ...] }
}
```

**DLL Config** (`TallyWhatsappsender.dll.config`):
- Bridge URL, timeouts, retry logic
- Read via `ConfigManager.Instance.WhatsAppSettings`

### Error Handling Pattern

The C# DLL uses retry logic with exponential backoff:
```csharp
// WhatsAppClient.cs
private ApiResult PostWithRetry(string endpoint, string body)
{
    for (int attempt = 1; attempt <= _maxRetries; attempt++) {
        // try request
        // on failure, wait _retryDelayMs * attempt
    }
}
```

Bridge returns consistent JSON:
```json
{
  "success": true,
  "message": "Message sent",
  "message_id": "ABC123XYZ"
}
```

### COM Interop Requirements

**Critical for DLL changes:**
1. All public interfaces must be `[ComVisible(true)]`
2. Use `[ClassInterface(ClassInterfaceType.AutoDual)]` on implementation
3. Must recompile AND re-register after code changes:
   ```bash
   msbuild /p:Configuration=Release
   regasm /u <path> # unregister old
   regasm <path> /codebase # register new
   ```
4. GUID must remain stable (defined in `AssemblyInfo.cs`)

### Session Management

- Bridge stores WhatsApp session in `store/whatsapp.db`
- Sessions expire ~20-30 days (need QR re-scan)
- Bridge must stay running for Tally to send messages
- Use Task Scheduler or NSSM for auto-start

## File Organization

```
whatstallysender/
├── TallyWhatsappSender/
│   ├── Binary/              # Compiled DLL + dependencies
│   ├── TDL/                 # Tally scripts
│   └── TallyWhatsappsender/ # C# source
│       ├── TallyInterface.cs      # COM entry point
│       ├── WhatsAppClient.cs      # HTTP client
│       ├── MessageTemplates.cs    # Message formatting
│       └── Config/                # Configuration classes
├── whatsapp-mcp/
│   └── whatsapp-bridge/     # Go service
│       ├── main.go          # Entry point + WhatsApp client
│       ├── api/             # HTTP handlers
│       ├── validation/      # Phone number validation
│       └── store/           # SQLite session storage
├── config.json              # Bridge configuration
├── start-bridge.bat         # Quick-start script
├── register-dll.ps1         # DLL registration helper
└── test-integration.bat     # E2E test suite
```

## Development Workflow

### Modifying the C# DLL
1. Edit source in `TallyWhatsappsender/`
2. Build with `msbuild`
3. Copy output to `Binary/`
4. Unregister old DLL (as Admin)
5. Register new DLL (as Admin)
6. Restart Tally to load updated DLL

### Modifying the Go Bridge
1. Edit source in `whatsapp-bridge/`
2. Stop running bridge (`Ctrl+C`)
3. Rebuild: `go build -o whatsapp-bridge.exe main.go`
4. Start: `.\whatsapp-bridge.exe`
5. No need to restart Tally (DLL reconnects automatically)

### Modifying TDL
1. Edit `WhatsappSenderTally.txt`
2. In Tally: F12 → TDL → Reload
3. Or restart Tally if auto-load configured

## Common Pitfalls

1. **"DLL not registered"**: Always run `regasm` as Administrator
2. **"Bridge not running"**: Check `http://localhost:8080/api/health`
3. **"Invalid contact"**: Mobile must be exactly 12 digits
4. **"Attachment not found"**: Verify TallyWhatsapp export folder exists
5. **CGO errors on Go build**: Install MSYS2 and mingw-w64 toolchain (required for SQLite on Windows)
6. **Session expired**: Delete `store/whatsapp.db` and re-scan QR code

## Dependencies

### Go (whatsapp-bridge)
- `go.mau.fi/whatsmeow` - WhatsApp Web library
- `github.com/mattn/go-sqlite3` - Session storage (requires CGO)
- `github.com/go-chi/chi/v5` - HTTP router

### C# (.NET Framework 4.0+)
- System.Net - HTTP client
- System.Runtime.InteropServices - COM interop
- System.Web.Extensions - JSON serialization
- No external NuGet packages (by design for Tally compatibility)

## Testing Approach

**Unit-level**: Not currently implemented
**Integration-level**: `test-integration.bat` checks:
- Bridge running and authenticated
- DLL registered and reachable
- End-to-end communication path

**Manual testing**:
1. Start bridge
2. Create Sales Voucher in Tally
3. Add party mobile (12 digits)
4. Save → Observe WhatsApp send prompt
5. Verify message received on phone

## Future Enhancements

(Documented for context, not implemented)
- Support for Purchase Vouchers
- Bulk invoice sending
- Message delivery status webhooks
- WhatsApp Business API integration (vs. Web API)
