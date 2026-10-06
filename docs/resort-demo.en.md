# Try the resort

[Español](resort-demo.es.md) · [Architecture](resort-booking-v0.12.en.md) · [Evidence and limits](progress/resort-v0.12.md)

Caribbean Horizon is a fictional hotel with one Standard room, one Deluxe and one Suite. Nightly prices are USD 120, 180 and 280. The Suite sleeps four and includes a private jacuzzi. There are no payments or commercial hotel connections.

## Start

After the base demo setup, open Docker Desktop and run `eng/Start-Resort.cmd` on Windows, or `pwsh -File eng/Start-Resort.ps1`. It preserves accounts/data, starts the existing identity service, opens the page and leaves services running in the background. It needs PowerShell 7 and the .NET version in `global.json`. See [setup for another computer](en/getting-started.md).

Open **http://127.0.0.1:7452/resort.html**. Catalog/calendar are public. Sign in with `customer-a` or `customer-b`, password `123456Aa!`. These synthetic accounts have different bookings. `operations-admin` uses the same demo password and can manage maintenance/rates without a private-booking override. A maintenance block cannot displace a stay.

WhatsApp requires [official test setup](integrations/setup.en.md). The launcher restarts the channel host; it does not publish the web UI or modify Meta permissions. The tunnel exposes **only port 7454**. A new tunnel URL after a PC restart requires updating and verifying the Meta callback. Never tunnel the identity, private web or admin routes.

## Link your chat

1. Sign in locally and generate a code.
2. Send the displayed `Link …` command to the test WhatsApp chat.
3. Refresh the web status and confirm the link in that same browser session.

The one-use code expires in five minutes. The link lasts no longer than the fifteen-minute customer session, and cannot authorize after logout. You can unlink early. When it expires, sign in and link again. The bot does not infer membership from a phone number or request your password in chat.

## WhatsApp walkthrough

The examples use October 2026. Later, choose future dates within the next ninety days.

| Send | Expected behavior |
|---|---|
| `What does the Suite with jacuzzi include?` | Structured amenities, capacity and fictional price |
| `Available dates` | Up to five two-night alternatives per category, read from current inventory |
| `Available Suite 2026-10-25 to 2026-10-27 for 2 guests` | Two-night alternatives starting from that date; no stock guarantee |
| `Book Suite 2026-10-25 to 2026-10-27 for 2 guests` | A USD 560 fictional quote, not an executed booking |
| `Confirm <full offer identifier>` | Confirmed stay and `STAY-…` ID; copy the full command returned by the bot |
| `My bookings` | Only the linked customer's stays |
| `Change STAY-… Suite 2026-10-28 to 2026-10-30 for 2 guests` | New quote; original dates persist until confirmation |
| `Cancel STAY-…` | Cancellation quote, not immediate cancellation |
| `/en`, `/es` or `que sea en español` | Persistent language selection |
| `/human` | Simulated human queue; bot pauses |

Each change/cancellation requires its own new quote and explicit confirmation. A bare yes cannot execute. The web page offers the same actions with a review/confirmation button.

Search returns up to five options per category over thirty possible arrival dates, defaulting to two guests/two nights. Specify full dates, category and guests for a booking; the quote summary shows the exact proposed values. Ambiguous dates such as tomorrow or a day range without month/year ask for clarification. This is bounded intent parsing, not general conversational understanding.

## Rules to demonstrate

- Every night must be free on the same unit; checkout does not occupy that night.
- Five-minute quotes hold no inventory. Confirmation rechecks availability, price and state.
- Free changes/cancellation need at least 72 hours before the **current** arrival; moving dates cannot bypass that rule.
- Occupied/maintenance targets reject a change and preserve the original stay.
- Repeated confirmation returns the existing receipt without duplicate effects.
- If the result cannot be verified, check My bookings before proposing a different operation. Uncertain message delivery does not undo a completed booking.

The original `RES-…` profile is preserved separately from new `STAY-…` inventory. Telegram's public adapter exists, but no live bot or private booking extension is activated there.

For a portfolio video: Suite/rate → occupied date → alternative → USD 560 quote → confirmation → updated calendar → change → cancellation. Label real WhatsApp transport, login, persistence and deterministic rules separately from fictional hotel/prices, this profile's simulated intent and simulated human queue. Never publish tokens, link codes, phone numbers or private logs.
