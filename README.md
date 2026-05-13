# PIM Tray

A tiny Windows tray app that lets you activate one or more Microsoft Entra ID **Privileged Identity Management (PIM)** roles with a single click - reason and duration included - without ever opening a browser.

Built for IT pros and admins who activate PIM roles many times a day and are tired of the portal click-marathon.

> Author: [Thomas Marcussen](https://thomasmarcussen.com) - Microsoft MVP, Technology Architect
> Blog: <https://blog.thomasmarcussen.com>

---

## Why

The Entra portal works, but activating a role takes 5 to 8 clicks, plus a tab switch, plus typing a reason every single time. PIM Tray collapses that into:

1. Left-click the tray icon
2. Tick the role(s) you want
3. Type one reason, pick one duration
4. **Activate**

That's it - one dialog, one round-trip, one balloon notification when each role is live (or pending approval).

---

## Features

- **System-tray native** Win32 app. Sits quietly in the notification area; opens on left-click, full menu on right-click.
- **Real interactive sign-in** via MSAL - handles MFA, Conditional Access and the rest of Entra's auth surface the proper way. No password is ever typed into the app.
- **Auto-discovery of eligible roles**: after sign-in the app calls Microsoft Graph and lists every role the user is eligible for, including scope (Directory vs. resource).
- **Multi-select batch activation**: tick several roles, fill in one reason + one duration, activate them all in one go. Partial-success is reported clearly so you can retry only the failed ones.
- **Reason + duration enforced**: the activation form requires a justification (good hygiene + matches most PIM policies). Duration is a configurable dropdown - default 1/2/4/8 hours, override in config.
- **Token cache persisted** to `%LOCALAPPDATA%\PIMTray\msal_cache.bin` (DPAPI-encrypted), so you only sign in once.
- **Per-user config** stored at `%APPDATA%\PIMTray\appsettings.json`. Created on first run if missing.
- **Code-signed** EV certificate (DigiCert). The MSI installer is signed too.
- **MSI installer** with Start menu shortcut, ARP entry, major-upgrade support.

---

## Install

**Option A - MSI (recommended)**

1. Download `PIMTray.msi` from the [Releases](../../releases) page.
2. Double-click. Per-machine install to `C:\Program Files\PIM Tray\`.
3. Start menu -> **PIM Tray -> PIM Tray**.

**Option B - Build from source**

```powershell
git clone https://github.com/<you>/PIMTray.git
cd PIMTray
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The exe lands in `bin\Release\net9.0-windows\win-x64\publish\PIMTray.exe`.

**Requirements**

- Windows 10 / 11 (x64)
- .NET 9 Windows Desktop Runtime (target machine)
- An Entra ID app registration with the right delegated Graph permissions (see [Configuration](#configuration))
- The signed-in user has at least one **eligible** PIM role

---

## Configuration

On first run the app creates `%APPDATA%\PIMTray\appsettings.json`:

```json
{
  "AzureAd": {
    "TenantId": "common",
    "ClientId": "14d82eec-204b-4c2f-b7e8-296a70dab67e",
    "RedirectUri": "http://localhost",
    "Cloud": "Public",
    "GraphResource": null,
    "GraphBaseUrl": null
  },
  "Pim": {
    "DefaultDurationHours": 1,
    "DurationOptionsHours": [ 1, 2, 4, 8 ]
  }
}
```

| Field                       | Description                                                                                                |
| --------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `AzureAd.TenantId`          | `common` for multi-tenant, or your tenant GUID for single-tenant.                                          |
| `AzureAd.ClientId`          | Default is the public **Microsoft Graph PowerShell** app (`14d82eec-...`). Replace with your own app reg.  |
| `AzureAd.RedirectUri`       | Must match the redirect URI configured on the app registration. `http://localhost` is the standard choice. |
| `AzureAd.Cloud`             | Cloud environment: `Public`, `USGovernment` (GCC/GCC High), `USDod`, `China`, or `Germany`.               |
| `AzureAd.GraphResource`     | Optional override for Graph resource root used in scopes (for example `https://graph.microsoft.us`).       |
| `AzureAd.GraphBaseUrl`      | Optional override for Graph REST base URL (for example `https://graph.microsoft.us/v1.0`).                 |
| `Pim.DefaultDurationHours`  | Pre-selected duration in the Activate dialog.                                                              |
| `Pim.DurationOptionsHours`  | The list of durations shown in the dropdown.                                                               |

For GCC High, set `AzureAd.Cloud` to `USGovernment`.

### Required Graph permissions (delegated)

- `RoleEligibilitySchedule.Read.Directory`
- `RoleAssignmentSchedule.ReadWrite.Directory`
- `User.Read`

### Bring-your-own app registration (recommended for production)

1. Entra admin center -> **App registrations -> New registration**. Single-tenant is fine.
2. **Authentication -> Add a platform -> Mobile and desktop applications**, redirect URI `http://localhost`. Allow public client flows = **Yes**.
3. **API permissions** -> Microsoft Graph -> add the three delegated scopes above. **Grant admin consent**.
4. Copy **Application (client) ID** and **Directory (tenant) ID** into `appsettings.json`.

After admin consent, end users never see a consent prompt.

---

## How it works

```
+----------------+    interactive   +-----------------------+
| PIMTray.exe    | ---------------> | Microsoft Identity    |
| (WinForms tray)| <----- token --- | (MSAL public client)  |
+----------------+                  +-----------------------+
        |
        | https://graph.microsoft.com/v1.0/roleManagement/directory/
        v
+-----------------------------------------------------------+
| GET  roleEligibilitySchedules?$filter=principalId eq ...  |
| POST roleAssignmentScheduleRequests {action: selfActivate}|
+-----------------------------------------------------------+
```

- **AuthService** (`Auth/AuthService.cs`) wraps MSAL's `PublicClientApplication`. Uses Win32 broker-less interactive sign-in (default browser) with the right scopes. Cache is DPAPI-encrypted on disk so subsequent launches go silent.
- **PimService** (`Pim/PimService.cs`) talks raw Graph REST. Two endpoints: list eligibilities, create activation requests.
- **TrayApplicationContext** + **MainForm** drive the UI. Both the tray right-click menu and the main window stay in sync via events.

No background polling, no telemetry, no callbacks.

---

## Project layout

```
PIMTray/
├── Auth/                    MSAL wrapper
├── Pim/                     Graph PIM client
├── UI/
│   ├── TrayApplicationContext.cs    NotifyIcon + context menu
│   ├── MainForm.cs                  Main window, role list, MenuStrip
│   ├── ActivateRoleForm.cs          Reason + duration dialog (single + multi)
│   └── AboutForm.cs                 About dialog
├── Resources/
│   ├── tray.ico             App icon
│   ├── PIMTray.rc           Win32 resource (version info + manifest + icon)
│   └── PIMTray.res          Compiled resource (gitignored - rebuild with rc.exe)
├── installer/
│   └── PIMTray.wxs          WiX 5 installer definition
├── AppConfig.cs             appsettings.json loader (writes defaults if missing)
├── AppIcon.cs               Loads tray.ico from embedded resource
├── Program.cs               Entry point + unhandled-exception handler
├── PIMTray.csproj
└── app.manifest             High-DPI / supportedOS manifest (embedded via .res)
```

---

## Build the installer

Requires [WiX Toolset v5](https://wixtoolset.org/) as a global .NET tool:

```powershell
dotnet tool install --global wix --version 5.0.2
wix build -arch x64 -out installer\PIMTray.msi installer\PIMTray.wxs
```

Sign (optional, EV cert example):

```powershell
signtool sign /sha1 <thumbprint> /fd SHA256 `
  /tr http://timestamp.digicert.com /td SHA256 `
  installer\PIMTray.msi
```

---

## Roadmap / ideas

- "Open settings file" tray entry
- Group eligibilities (PIM for Groups)
- Azure Resource PIM (subscription / resource group / resource scopes)
- Approval-pending status polling
- Auto-start at logon checkbox in About / Settings
- Optional dark mode

PRs welcome.

---

## License

MIT - see [LICENSE](LICENSE).

---

## Credits

Built by [Thomas Marcussen](https://thomasmarcussen.com) - Microsoft MVP, Technology Architect.
Contact: <Thomas@ThomasMarcussen.com>
