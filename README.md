# HomeEquity Bank Referral Portal — MARS POC

A dedicated **customer-to-customer referral portal** built by **Mars Team** for a HomeEquity Bank presentation. Customers share a referral link, friends submit enquiries, and the back-office team follows up, approves applications and processes simulated rewards.

**Technology:** ASP.NET Core MVC / .NET 10, C# class lists, LINQ and JSON file persistence. The MVC frontend and business backend run in one application; the three portals are separate modules, not separate servers.

**Demo only:** Sign-in is passwordless. WhatsApp/LinkedIn messages, salesperson follow-up, funding and payments are simulated. The illustrative rewards are not an approved HomeEquity Bank offer.

## Contents

- [Start locally](#start-locally)
- [Portal pages](#portal-pages)
- [Current features](#current-features)
- [Sharing simulation](#sharing-simulation)
- [Enquiry and automatic registration](#enquiry-and-automatic-registration)
- [Referral and reward rules](#referral-and-reward-rules)
- [Applications and payment simulation](#applications-and-payment-simulation)
- [Default demo data](#default-demo-data)
- [Presentation walkthrough](#presentation-walkthrough)
- [Persistence and authentication](#persistence-and-authentication)
- [Project structure](#project-structure)
- [Build and verification](#build-and-verification)
- [QC deployment](#qc-deployment)
- [Production scope](#production-scope)
- [Program team](#program-team)

## Start locally

Install the **.NET 10 SDK**. A database server, Node.js and Python are not required to run the website.

```powershell
Set-Location D:\CP
dotnet restore MarsReferral.sln
dotnet run --project src/MarsReferral.Web
```

Open **http://localhost:5081/**. This is the port configured in `src/MarsReferral.Web/Properties/launchSettings.json`.

To choose a port explicitly:

```powershell
dotnet run --project src/MarsReferral.Web --no-launch-profile --urls http://localhost:5080
```

`RunDemo.cmd` runs the project using its launch profile. Its existing console message still mentions 5080; use the URL printed by ASP.NET Core (`Now listening on`), currently 5081 with the default profile.

You can also open `MarsReferral.sln` in Visual Studio, select `MarsReferral.Web` as the startup project and use the `MarsReferral` profile. Stop the app with **Ctrl+C**. Restarting retains saved records but requires signing in again.

## Portal pages

Paths below use the same host and port as your running application.

| Page | Path | Purpose |
|---|---|---|
| Portal selector | `/` | Choose a portal |
| Admin / back office | `/admin` | Operations dashboard and work queues |
| Admin sign-in | `/admin/login` | Enter the demo administrator session |
| Customer portal | `/customer` | Personal dashboard, referrals and rewards |
| Customer sign-in | `/customer/login` | Select an existing demo customer |
| Registration | `/join` | Optional standalone registration and onboarding |
| Customer sharing | `/customer/sharing` | WhatsApp / LinkedIn simulation |
| Customer enquiries | `/customer/enquiries` | People I referred and My own enquiries |
| Referral landing page | `/referral?code=MARS000001` | Submit an enquiry using a customer's code |
| Enquiry confirmation | `/referral/thanks` | Continue to Customer Portal after submission |
| Sales follow-up | `/admin/enquiries` | Review enquiries and simulate follow-up |
| Customer records | `/admin/customers` | Search, referral controls and customer shadowing |
| Application approvals | `/admin/applications` | Approve amounts and confirm funding |
| Reward approvals | `/admin/rewards` | Review rewards and simulate payments |
| Customer guide | `/customer/guide` | Current referral rules and printable guide |
| HomeEquity Bank solutions | `/customer/heb` | Product information and official source links |

**Admin is back office only.** Sharing demo and Referral guide have been removed from its navigation and routes. `/admin/sharing` and `/admin/guide` are not available. Customer sharing and guide pages remain available.

## Current features

- HomeEquity Bank name and logo across the shared header, footer, referral previews and printed-page branding, with a visible POC label.
- Teal, mint, yellow and white presentation styling, larger typography, responsive cards and bold person names.
- Separate Admin, Customer and Registration & Onboarding experiences.
- Visible **Refer a Friend** section with a personal link, copy action and a WhatsApp / LinkedIn button with icons.
- Direct referral popup that creates a friend's demo account with name, email and phone; it attaches the referring customer's code.
- Enquiry submission with automatic registration and sign-in for **new** customers.
- Participant-specific enquiry tracking and a dummy salesperson, **Sarah Mitchell**.
- Application amount approval, funding confirmation, reward review, failed-payment retry and paid confirmation.
- Customer shadow sessions, messages, activity logs, filtering, paginated record views and print styling.

## Sharing simulation

Open **Customer Portal → Refer a Friend** and choose WhatsApp or LinkedIn. Enter the recipient's name and compose the message. The referral link is added automatically.

There is **one contact textbox** whose label and validation depend on the selected channel:

| Channel | Enter | Example |
|---|---|---|
| WhatsApp | Number including country code; 10–15 digits | `+1 416 555 0123` |
| LinkedIn | Profile ID, `@`-prefixed ID, or HTTPS `/in/` profile link | `sophie-campbell` or `https://www.linkedin.com/in/sophie-campbell` |

Switching channels clears the previous contact value. Validation runs in the browser and on the server. WhatsApp numbers are stored in normalized `+digits` form; LinkedIn IDs are stored as canonical profile URLs. Other website URLs and company-page links are rejected.

1. Review the live message and link preview.
2. Click **Simulate sending invitation** to create a tracked invitation.
3. Use **Simulate delivery** to change Sent to Delivered.
4. Open the recipient landing page to submit an enquiry.

The saved contact appears in the recipient preview and invitation history. Older invitations without contact details display a clear “Not recorded” message. **No message is sent to WhatsApp or LinkedIn, and the demo does not verify that the entered account exists.**

## Enquiry and automatic registration

### New customer

```text
Open referral link
    → Enter name, email, phone and enquiry topic; confirm consent
    → Submit enquiry
    → Create and sign in to the customer demo account automatically
    → Continue to Customer Portal
    → My enquiries
```

The form explains the account creation and referral rules. A successful submission:

- Saves the enquiry and assigns it to **Sarah Mitchell**.
- Creates a ready-to-use customer account with the submitted phone number.
- Attaches the referring customer's code to the new profile.
- Shows: **“Thanks for contacting us. Our salesperson will get back to you shortly.”**
- Displays **Continue to Customer Portal** and **View my enquiry** buttons.

New enquirers do **not** need a separate registration, onboarding or sign-in step. The enquiry and account changes are committed together. Invalid submissions do not leave a partially created account, and repeated submissions do not create duplicate pending enquiries or accounts.

### Existing customer

An existing email can be reused only when the request has the **matching authenticated customer session**. An email or posted customer ID alone does not authorize access to an existing account. Shadow sessions do not count as that customer's own authenticated identity for this flow.

For a matching signed-in account, no duplicate profile is created. A referral is attached if the profile has none; an existing referral remains unchanged. When the enquiry link belongs to a different referrer, the confirmation explains that the saved profile referral has not been replaced.

Standalone registration at `/join` still uses the original introduction/onboarding step. This is separate from the automatic enquiry-registration path.

### Who sees the enquiry?

| Viewer | Visibility |
|---|---|
| Referring customer | **People I referred:** recipient name, reference, salesperson, date and follow-up status |
| Enquirer | **My own enquiries:** their own enquiry and contact details, matched to their account email |
| Admin | All enquiries, contact details and the **Simulate follow-up** action |
| Unrelated customer | No access to another person's enquiry |

An admin follow-up changes **New** to **Contacted**. Both participants see the updated status when they open or refresh their enquiry page. Referrers do not see the enquirer's phone, email or enquiry topic in this view.

## Referral and reward rules

| Participant | Illustrative reward |
|---|---|
| Referring customer | **$50 CAD** |
| Referred customer | **$25 CAD** |

- One sharing code can refer many people.
- A referred customer attaches one code to their **customer profile**, not separately to each application.
- New customers and existing customers without a code can attach a valid, active code.
- A saved code cannot be replaced. Self-referrals and circular relationships are blocked.
- Only the first qualifying funding event after attachment creates the reward pair; later applications do not duplicate it.
- Registration, enquiry submission and application approval alone do not earn rewards.
- Earlier funding is not rewarded retroactively. An application created before attachment may qualify if funding occurs after attachment.
- Rejected rewards do not create a new entitlement on a later application.
- Pausing a code prevents new attachments without removing existing relationships.
- Funding notifications and referral messages are stored inside the demo; they are not externally delivered.

## Applications and payment simulation

Application purposes: **Home purchase, Refinance, Home improvement, Reverse mortgage and Mortgage**.

1. Customer creates an application with a requested amount.
2. Admin opens **Application Approval** and approves an amount.
3. The approved amount must be at least 1,000, no greater than requested, and have at most two decimal places. Requested amounts are limited to 1,000–10,000,000 demo units.
4. Admin separately confirms funding. Approval itself does not mean the account has received funds.
5. Qualifying funding creates the **$25 / $50 CAD** rewards in Pending status.
6. Admin opens **Reward approvals**, approves a reward, then clicks **Simulate payout**.
7. Enter a payment reference and select the simulated outcome.

**Confirmed success** sets the reward to Paid. **Failed** leaves it unpaid and enables **Retry payout**. Duplicate payments are rejected. Confirmed payment references must be unique and contain 4–40 letters, numbers or hyphens. Pending or rejected rewards cannot be paid.

Application amounts are displayed as **demo units**; referral rewards are displayed in **CAD**. No bank transfer or real funding integration occurs.

## Default demo data

The source JSON includes sample customer/application/reward scenarios. Startup presentation migration supplies the current Canadian/Western names and $25/$50 reward values. The current QC dataset can contain additional records created during demonstrations.

| Customer | Sharing code | Starting referral state |
|---|---|---|
| **Liam Thompson** | `MARS000001` | Active referrer, no attached code |
| **Olivia Martin** | `MARS000002` | Attached to **Liam Thompson** |
| **Ethan Wilson** | `MARS000003` | No attached code |
| **Emma Anderson** | `MARS000004` | Attached to **Liam Thompson** |
| **Noah Bennett** | `MARS000005` | Attached to **Liam Thompson** |
| **Charlotte Clark** | `MARS000006` | Sharing code paused |

When the enquiry list is empty and eligible referring customers exist, startup adds five fictional enquiries:

| Person | Topic | Status |
|---|---|---|
| **Grace Miller** | Reverse mortgage | New |
| **Oliver Brooks** | Income Solution | New |
| **Ava Johnson** | General enquiry | New |
| **William Taylor** | Reverse mortgage | Contacted |
| **Sophie Campbell** | Income Solution | Contacted |

All are assigned to **Sarah Mitchell**. Seeding does not add duplicates or overwrite a non-empty enquiry list. These sample enquiries are presentation records, not automatic customer-account registrations; new submissions through the enquiry form use the automatic registration flow.

## Presentation walkthrough

1. Open Customer Portal as **Liam Thompson** and show **Refer a Friend**.
2. Choose WhatsApp, enter a demo number and preview the message. Switch to LinkedIn to demonstrate the same textbox accepting a profile ID/link.
3. Simulate sending and delivery; open the tracked recipient landing page.
4. Submit an enquiry with a **new fictional email**. Show automatic account creation and **Continue to Customer Portal**.
5. Open **My enquiries** in the new customer's portal. In another browser/session, show the matching entry under **People I referred** for **Liam Thompson**.
6. Enter Admin and open **Sales follow-up**. Simulate contacting the enquirer, then refresh both customer views.
7. Create an application as the new customer. Admin approves the amount and separately confirms funding.
8. Show the **$25 / $50 CAD** reward pair. Approve a reward, simulate failure, then retry successfully.
9. Use **Customers & shadow** to inspect a customer; return to Admin and review the audit trail.

Shadow mode replaces the browser's customer session. A banner identifies the selected customer, actions are attributed to admin, and **Return to Admin** clears the shadow session. A valid admin session is required throughout. Use separate browser sessions when demonstrating both participants at the same time.

## Persistence and authentication

Data is loaded from `App_Data/referral-data.json` under the application's content root. During local development this is `src/MarsReferral.Web/App_Data/referral-data.json`.

The snapshot contains **Customers, Applications, Rewards, Messages, Audit, Invitations, Enquiries** and **PresentationVersion**. Older JSON files can omit the newer sharing fields. `PresentationVersion` records the one-time presentation migration; startup also calls the empty-enquiry-list seeder.

- C# lists hold the working data; LINQ performs lookups, validation, filtering, totals and ordering.
- Every successful mutation writes the complete snapshot using an atomic file replacement.
- Failed operations restore the previous in-memory snapshot.
- An exclusive lease prevents two app processes from writing the same data file.
- Missing, malformed or inconsistent JSON stops startup instead of silently resetting saved data.
- The file is outside `wwwroot`; it is not a public website asset.
- Stop the app before manually editing or restoring its JSON. Preserve a backup first.

| Portal | Cookie | Authentication scheme |
|---|---|---|
| Admin | `MarsReferral.Admin` | `MarsAdmin` |
| Customer | `MarsReferral.Customer` | `MarsCustomer` |
| Onboarding | `MarsReferral.Onboarding` | `MarsOnboarding` |

Customer sessions do not grant admin access. Customer actions use the authenticated identity rather than posted customer IDs. Mutating MVC actions require anti-forgery tokens. Session keys are ephemeral, so a restart/deployment requires signing in again.

## Project structure

```text
src/
  MarsReferral.Core/
    ReferralService.cs       Domain records, referral/application/reward rules
    ReferralPersistence.cs   JSON persistence, rollback and exclusive lease
    ReferralSharing.cs       Invitations, contact validation and enquiries
    EnquiryRegistration.cs   Atomic enquiry + customer registration
    ReferralDirect.cs        Direct customer-to-customer account referral
    PresentationRefresh.cs   One-time demo presentation migration
  MarsReferral.Web/
    Controllers/             Portal routes, identity scope and commands
    Models/                  Form validation, portal sessions and safe name emphasis
    Views/                   Gateway, back office, customer and enquiry pages
    wwwroot/css/             Responsive styling and HomeEquity Bank theme
    wwwroot/js/              Sharing previews, contact switching and UI interactions
    wwwroot/images/brand/    Local logo assets and source information
    App_Data/                Required JSON snapshot
    Program.cs               App setup and startup data initialization
tests/MarsReferral.Tests/    Executable business and persistence regression checks
tools/                      Existing HTTP smoke checks and optional guide tools
```

The online customer guide reflects the current referral policy. Existing PDFs in `wwwroot/guides` are static artifacts; they are not regenerated when website features or reward values change.

## Build and verification

```powershell
Set-Location D:\CP
dotnet build MarsReferral.sln -c Release
dotnet run --project tests/MarsReferral.Tests -c Release
```

The current executable suite reports **33 business-rule tests**, followed by the persistence, sharing/enquiry and enquiry auto-registration suites. Coverage includes referral ownership, duplicate protection, funding/reward transitions, payout retry, JSON rollback, contact validation, automatic registration, authenticated account reuse and concurrent submissions.

`tools/http_smoke.py` is a **legacy smoke script**. It still expects older page headings and `/admin/guide`, which has been removed, so it is not a current acceptance check. Update its assertions before reusing it. It also creates persistent records: run any revised version against a disposable data copy and pass the actual local URL explicitly.

Additional targeted HTTP checks during development verified enquiry visibility, automatic sign-in for new enquirers, customer-only sharing, removed Admin marketing routes, saved social contact details and the deployed sharing button. Those development check scripts live in the local working workspace, not in this repository's test project.

## QC deployment

### Current presentation PC

The maintained one-click deployer on this PC is:

```powershell
& 'C:\Users\DELL\Documents\ChatGPT\POC Project\deliverables\MARS-QC-Deploy\MARS-QC-Deploy.exe' --unattended
```

It publishes **D:\CP**, stages the build, stops the tracked QC app, backs up and carries forward its JSON, switches builds, and checks local/public health. A failed local health check restores the previous app. Logs and timestamped backups are under `.qc-tunnel` in that workspace.

| Item | Current location |
|---|---|
| QC application | `C:\Users\DELL\Documents\ChatGPT\POC Project\.qc-tunnel\cp-publish` |
| QC data | `...\.qc-tunnel\cp-publish\App_Data\referral-data.json` |
| Local QC listener | `http://127.0.0.1:5084` |
| Current public URL file | `C:\Users\DELL\Documents\ChatGPT\POC Project\.qc-tunnel\public-url.txt` |
| Deployer source | `...\deliverables\MARS-QC-Deploy\source\Program.cs` |

Read the current public link instead of bookmarking an older tunnel URL:

```powershell
Get-Content 'C:\Users\DELL\Documents\ChatGPT\POC Project\.qc-tunnel\public-url.txt'
```

The project-root `QC-LINK.txt` is an older saved link. The project-root `MARS-QC-Deploy.exe` also differs from the maintained build above; use the maintained path for this PC's updates.

This is a **temporary Cloudflare tunnel**, not permanent hosting. The PC, QC app and tunnel must remain running and online. Restarting the tunnel may change the public URL. After deploying, refresh with **Ctrl+F5** and sign in again. Updating the source alone does not update the published site; there is no browser URL that deploys code.

### Manual publish / another server

```powershell
Set-Location D:\CP
dotnet publish src/MarsReferral.Web/MarsReferral.Web.csproj -c Release -o artifacts/publish
```

Use a separate staging directory. Stop the existing server app and back up its `App_Data/referral-data.json`; copy that live snapshot into the staged build before switching to it. Do not replace existing QC/customer data with the source seed JSON. Launch from the publish directory, for example:

```powershell
Set-Location D:\CP\artifacts\publish
dotnet MarsReferral.Web.dll --urls http://127.0.0.1:5084
```

The target needs the compatible **.NET 10 ASP.NET Core runtime** for this framework-dependent publish. For IIS, install the appropriate Hosting Bundle and configure the published application. Another server also needs its own domain, HTTPS/reverse-proxy setup and process management; the PC-specific deployer paths do not configure those automatically.

## Production scope

This POC uses a passwordless demo identity model and a single-process JSON store. Production requires verified customer identity and account recovery, proper admin authentication, a transactional database, authoritative funding integration, approved referral terms, consent handling, and real messaging/payment provider integrations. A LinkedIn ID or WhatsApp number entered in the demo is only a simulated destination.

## Program team

| Person | Name |
|---|---|
| 1 | **Rajni Kaushal** |
| 2 | **Kuldip Solanki** |
| 3 | **Sushmitha Amaran** |
| 4 | **V Karim** |

**Team: Mars Team**
"# ReferralProgram" 
