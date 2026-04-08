# Troubleshooting Guide

## Common Issues & Solutions

---

### ❌ "COM Interface is not invokable or DLL is not registered"

**Cause:** The DLL hasn't been registered or the registration failed.

**Fix:**
```powershell
# Run as Administrator
cd d:\whatstallysender\TallyWhatsappSender\Setup
.\install.ps1 -Action install
```

Verify registration:
```powershell
.\install.ps1 -Action status
```

---

### ❌ "WhatsApp bridge is not running"

**Cause:** The Go bridge service isn't started.

**Fix:**
1. Double-click the "WhatsApp Bridge" desktop shortcut, or
2. Open a terminal and run: `C:\TallyWhatsApp\whatsapp-bridge.exe`
3. If the window closes immediately, run it from a terminal to see the error message.

---

### ❌ "Bridge is running but NOT authenticated"

**Cause:** First run or session expired (after ~20 days).

**Fix:**
1. Look at the bridge terminal window — a QR code will be visible.
2. Open WhatsApp on your phone → Settings → Linked Devices → Link a Device.
3. Scan the QR code.
4. Wait for "Client connected!" in the terminal.

---

### ❌ "No mobile number found for this party"

**Cause:** The party ledger in Tally doesn't have a mobile number set.

**Fix:**
1. Go to **Masters → Ledgers → [Party Name]**
2. Under **Mailing Details**, set **Mobile No** to the WhatsApp number.
3. Format: `919876543210` (country code + 10-digit number, no `+` or spaces)

---

### ❌ "Invalid contact number" (12-digit validation failure)

**Cause:** The mobile number in Tally is not exactly 12 digits.

**Indian numbers:** `91` + 10 digits = 12 total (e.g., `919876543210`)  
**Other countries:** Use your country code + local number = 12 digits total.

> If your numbers are consistently 10 digits (without country code), change `ContactValidationLength` in `App.config` to `10`.

---

### ❌ Bridge exits immediately on startup

**Cause:** Missing Go dependencies or SQLite CGO issue on Windows.

**Fix:**
1. Ensure you used MSYS2/MinGW to build: `go build -o whatsapp-bridge.exe .`
2. Check that `go-sqlite3` compiled with CGO:
   ```bash
   set CGO_ENABLED=1
   go build -o whatsapp-bridge.exe .
   ```
3. Copy any required `.dll` files from MinGW `bin/` directory alongside `whatsapp-bridge.exe`.

---

### ❌ PDF attachment not found / file path issues

**Cause:** Tally export directory doesn't exist or path has special characters.

**Fix:**
1. The `WA_EnsureExportDir` function creates the folder automatically.
2. Check that `TallyWhatsapp\` folder exists in the Tally data directory.
3. Avoid party names with slashes or special characters.

---

### ❌ Firewall / Port 8080 blocked

**Cause:** Windows Firewall is blocking the bridge port.

**Fix:**
```powershell
# Run as Administrator
New-NetFirewallRule -DisplayName "TallyWhatsApp Bridge" `
    -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
```

Or use the installer: `.\install.ps1 -Action install`

---

### ⚠️ Session expires frequently

**Cause:** WhatsApp Web sessions last approximately 20 days. If WhatsApp is used on too many devices, it may disconnect sooner.

**Fix:**
1. Keep the bridge as a dedicated linked device.
2. Don't use the same WhatsApp account from too many places simultaneously.
3. Enable auto-restart: `.\install.ps1 -Action service-install`

---

## Log File Analysis

Check `WhatsAppSender.log` (next to the registered DLL) for detailed error information.

The bridge also logs to its terminal window — check for errors there.

---

## Getting Help

1. Run the health check: `.\install.ps1 -Action status`
2. Test in Tally: **Gateway → WhatsApp → Test Connection**
3. Review the log file for specific error codes.
