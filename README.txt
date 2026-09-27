MARS ONE-CLICK QC DEPLOYMENT

1. Save your latest code in D:\CP.
2. Double-click MARS-QC-Deploy.exe.
3. Wait for DEPLOYMENT COMPLETE. The website opens automatically.
4. Share the URL displayed or saved in QC-LINK.txt.

This tool is configured for this PC and the existing .qc-tunnel folder.
It requires the installed .NET 10 SDK, internet, and the existing cloudflared.exe.
Keep the PC awake and online for QC access. No paid hosting is created.

The tool builds before stopping the current app. It backs up and preserves
the existing QC JSON, switches the app, and checks local and public URLs.
If switching or local startup fails, it restores the previous app.
Public-network failures do not undo a locally healthy deployment.

Your D:\CP JSON is NOT copied over QC records during routine deployments.
Use a separate intentional data import when you want to replace QC records.
Existing portal sessions may require signing in again after deployment.

An existing running tunnel keeps its URL. If the tunnel is stopped, this
tool starts a new one and records the new URL. Admin access remains
passwordless as in the current demo; share the link only with QC.

Logs, timestamped backups and previous builds are kept under:
C:\Users\DELL\Documents\ChatGPT\POC Project\.qc-tunnel
Backups are retained; periodically archive older ones to free disk space.

Advanced: --check --unattended builds without replacing the website.
Source code is included in the source folder.
