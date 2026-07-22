# Support Ticket Fix: Nullable SponsorId + Admin/Sponsor Recipient Choice

## Bug Fix — Tourist support submission crash

**Root cause:** `SupportTicket.SponsorId` was non-nullable `int`, and `TouristSupportController.CreateSupport` never set it. When a tourist submitted a ticket, `SponsorId` defaulted to `0`, causing a PostgreSQL FK violation on `SaveChanges()`.

**Schema change:**
- Make `SupportTicket.SponsorId` nullable (`int?`).
- Update `TouristContext.OnModelCreating` to configure the `SupportTicket -> Sponsor` relationship as optional (`DeleteBehavior.SetNull`).
- Add `SponsorResponse` (`string?`) and `SponsorRespondedDate` (`DateTime?`) columns.
- EF migration: `MakeSupportTicketSponsorIdNullable`.

**Code fixes:**
- `Models/SupportTicket.cs` — change `SponsorId` to `int?`, add sponsor response fields.
- `Data/TouristContext.cs` — optional FK with `SetNull`.
- `Controllers/AdminSupportController.cs` — null-safe `SponsorId` checks in `Index` filters, stat boxes, and `Details`; add `"Tourist -> Sponsor"` submitter type; pass sponsor response fields to details VM.
- `Controllers/TouristSupportController.cs` — inject `INotificationService`; add recipient routing (`Admin`/`Sponsor`); validate sponsor choice; populate `AvailableSponsors` via `SelectList` on `SupportTicketVM`.
- `Controllers/SponsorNotificationController.cs` — confirm nullable comparisons still work; add `TouristTickets()`, `TouristTicketDetails()`, `RespondToTouristTicket()` actions.
- `Services/SupportTicketService.cs` — add `GetTouristTicketsForSponsor` and `GetTouristTicketForSponsor`.
- `ViewComponents/SupportBellViewComponent.cs` — safe as-is.

**View changes:**
- `Views/TouristSupport/Index.cshtml` — add recipient type dropdown + conditional sponsor dropdown (JS toggle).
- `Views/TouristSupport/Details.cshtml` — show sponsor response section when present.
- `Views/AdminSupport/Index.cshtml` — add "Routed to" column; update badge colors for three submitter types.
- `Views/AdminSupport/Details.cshtml` — show sponsor response section.
- `Views/SponsorNotification/TouristTickets.cshtml` — new list view for sponsor's routed tickets.
- `Views/SponsorNotification/TouristTicketDetails.cshtml` — new detail + reply form.
- `Views/Shared/Components/SupportBell/Default.cshtml` — add "Tourist Tickets" dropdown link.
- `Views/Shared/_Layout.cshtml` — update `notifTarget` JS to route `TouristSupportTicket` notifications.

**Localization:**
- Append keys to `Resources/SharedResource.en.resx` and `.ar.resx` for recipient choice, sponsor response labels, tourist-ticket inbox titles, etc.

**Validation:**
- Full `dotnet build` passes (0 errors).
- EF migration applied successfully.
