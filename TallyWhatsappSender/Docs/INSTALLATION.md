# Installation Guide — Tally WhatsApp Integration

## Overview

This guide walks through installing the unified Tally→WhatsApp solution. It uses a **pure HTTP** approach where Tally communicates directly with a persistent WhatsApp Go bridge. (The legacy C# DLL / Selenium approach has been entirely removed).

---

## Prerequisites

| Requirement | Details |
|---|---|
| Windows 10/11 | 64-bit recommended |
| Go 1.19+ | Required to **build** the bridge (not to run the pre-built exe) |
| TallyPrime 2.0+ | Required for `$$HTTP Post` support |
| WhatsApp account | Personal or Business |

---

## Step 1 — Build the WhatsApp Bridge (Go)

> Skip this step if you have a pre-built `whatsapp-bridge.exe`.

```powershell
cd d:\whatstallysender\whatsapp-mcp\whatsapp-bridge
go build -o whatsapp-bridge.exe .
```

The build produces `whatsapp-bridge.exe` in the same folder.

---

## Step 2 — Run the Installer

Open **PowerShell as Administrator** and run:

```powershell
cd d:\whatstallysender\TallyWhatsappSender\Setup
.\install.ps1 -Action install
```

This will:
- Copy TDL scripts to `C:\TallyWhatsApp\TDL\`
- Copy the bridge executable to `C:\TallyWhatsApp\`
- Add a Windows Firewall rule for port 8080
- Create a **desktop shortcut** for the bridge

---

## Step 3 — First-Time Authentication

1. **Double-click** the "WhatsApp Bridge" shortcut on your Desktop
   (or run `C:\TallyWhatsApp\whatsapp-bridge.exe` from a terminal).
2. A **QR code** appears in the terminal window.
3. Open WhatsApp on your phone → **Linked Devices** → **Link a Device**.
4. Scan the QR code.
5. The terminal shows `Client connected!` — authentication is complete.

> **Session persists for ~20 days.** The bridge auto-reconnects after restarts.

---

## Step 4 — Load TDL Files in Tally

1. Open Tally → Press **F1** (Help) → **TDLs & Add-Ons** → **F4** (Manage Local TDLs).
2. Set "Load selected TDL files on startup" to **Yes**.
3. Add the following TDL files in order:

| Order | File | Purpose |
|---|---|---|
| 1 | `C:\TallyWhatsApp\TDL\WhatsAppHTTP.tdl` | Core (required) |
| 2 | `C:\TallyWhatsApp\TDL\InvoiceSender.tdl` | Send invoices |
| 3 | `C:\TallyWhatsApp\TDL\ReceiptSender.tdl` | Send receipts |
| 4 | `C:\TallyWhatsApp\TDL\PaymentReminder.tdl` | Send reminders |
| 5 | `C:\TallyWhatsApp\TDL\Configuration.tdl` | Menu & test tools |

4. Press **Enter** to save and restart Tally.

---

## Step 5 — Test the Connection

In Tally: **Gateway of Tally** → **W (WhatsApp)** → **T (Test Connection)**

You should see: ✅ Connected and authenticated!

---

## Step 6 — Set Up Party Ledger Mobile Numbers

Each party's ledger must have a **Mobile Number** set (12 digits, country code + number, no `+`):

- India example: `919876543210` (91 = country code)

**In Tally:** Masters → Ledgers → [Party Name] → Mailing Details → Mobile No.

---

## Optional: Run as Windows Service

To have the bridge start automatically at Windows boot:

```powershell
.\install.ps1 -Action service-install
```

To remove the service:

```powershell
.\install.ps1 -Action service-uninstall
```

---

## Checking Status

```powershell
.\install.ps1 -Action status
```

---

## Uninstallation

```powershell
.\install.ps1 -Action uninstall
```

---

## File Locations Reference

| File | Location |
|---|---|
| Bridge executable | `C:\TallyWhatsApp\whatsapp-bridge.exe` |
| TDL scripts | `C:\TallyWhatsApp\TDL\` |
| Bridge config | `whatsapp-bridge\config.development.json` |
| Bridge session data | `whatsapp-bridge\store\` |

