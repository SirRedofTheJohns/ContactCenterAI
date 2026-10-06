using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ContactCenterAI.Application;
using ContactCenterAI.Domain;
using ContactCenterAI.Infrastructure;
using Microsoft.Data.Sqlite;

namespace ContactCenterAI.Api;

public static class ResortApi
{
    public static void Map(WebApplication app, ResortStore store, string bridgeKey, string endpoint)
    {
        Actor Actor(HttpContext h) => h.Items["Actor"] as Actor ?? throw new RequestRejected(401, "SESSION_REQUIRED");
        app.Use(async (h, next) =>
        {
            if (h.Request.Path.StartsWithSegments("/internal/resort"))
            {
                var limit = h.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 16000;
                if (h.Request.ContentLength > 16000) { h.Response.StatusCode = 413; return; }
                var supplied = h.Request.Headers["X-Resort-Service-Key"].ToString();
                if (bridgeKey.Length < 32 || supplied.Length > 128 || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(bridgeKey))))
                { h.Response.StatusCode = 401; return; }
            }
            try { await next(h); } catch (SqliteException) { throw new OperationalUnavailable(); }
        });
        app.MapGet("/v2/resort/catalog", () => Results.Ok(new { fictional = true, currency = "USD", today = store.Today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), categories = store.Catalog() }));
        app.MapGet("/v2/resort/calendar", (string from, int days) => Results.Ok(store.Calendar(from, days)));
        app.MapGet("/v2/resort/stays", (HttpContext h) => Results.Ok(store.MyStays(Actor(h))));
        app.MapPost("/v2/resort/offers", (HttpContext h, ResortRequest r) => Results.Ok(store.Offer(Actor(h), r, h.Request.Headers["Idempotency-Key"].ToString())));
        app.MapPost("/v2/resort/offers/{id:guid}/confirm", (HttpContext h, Guid id, ResortConfirm c) => c.Decision == "confirm" ? Results.Ok(store.Confirm(Actor(h), id)) : throw new RequestRejected(400, "EXPLICIT_CONFIRMATION_REQUIRED"));
        app.MapGet("/v2/resort/operations/{id:guid}", (HttpContext h, Guid id) => Results.Ok(store.Receipt(Actor(h), id)));
        app.MapPost("/v2/resort/link-codes", (HttpContext h) => Results.Ok(new { code = store.CreateLinkCode(Actor(h)), expiresInSeconds = 300 }));
        app.MapGet("/v2/resort/link-status", (HttpContext h) => Results.Ok(store.LinkStatus(Actor(h))));
        app.MapPost("/v2/resort/link-confirm", (HttpContext h) => { store.ConfirmLink(Actor(h)); return Results.Ok(new { linked = true }); });
        app.MapPost("/v2/resort/unlink", (HttpContext h) => { store.Unlink(Actor(h)); return Results.NoContent(); });
        app.MapGet("/v2/resort/blocks", (HttpContext h) => Results.Ok(store.Blocks(Actor(h))));
        app.MapPost("/v2/resort/blocks", (HttpContext h, ResortBlockRequest r) => Results.Ok(store.Block(Actor(h), r)));
        app.MapDelete("/v2/resort/blocks/{id}", (HttpContext h, string id, long version) => { store.RemoveBlock(Actor(h), id, version); return Results.NoContent(); });
        app.MapPost("/v2/resort/rates", (HttpContext h, RateUpdate r) => { store.UpdateRate(Actor(h), r.Type, r.Cents); return Results.NoContent(); });
        app.MapPost("/internal/resort/channel", async (ResortChannelRequest r, HttpContext h, IOperationalStore identities, TimeProvider clock) =>
        {
            if (r.Channel != "whatsapp" || r.Endpoint != endpoint || string.IsNullOrEmpty(endpoint) || r.Route is null || r.Text is null || r.EventKey is null || r.Route.Length != 64 || !r.Route.All(Uri.IsHexDigit) || r.Epoch < 1 || r.Text.Length > 2000 || r.EventKey.Length > 128 || r.Language is not ("es" or "en")) throw new RequestRejected(400, "CHANNEL_SCOPE_INVALID");
            var service = new ResortConversation(store, identities, clock);
            return Results.Ok(await service.AnswerAsync(r, h.RequestAborted));
        });
    }
}
public sealed record RateUpdate(string Type, long Cents);

public sealed class ResortConversation(ResortStore store, IOperationalStore identities, TimeProvider clock)
{
    private static string Money(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture) + " USD";
    public async Task<ResortChannelResult> AnswerAsync(ResortChannelRequest input, CancellationToken ct)
    {
        var en = input.Language == "en";
        ResortChannelResult Reply(string code, string es, string english) => new(en ? english : es, code);
        try
        {
            if (ResortLanguage.LinkCode(input.Text) is string code)
            { store.SubmitLinkCode(code, input.Route, input.Epoch); return Reply("LINK_PENDING", "Código recibido. Vuelve a la página del resort en tu PC y pulsa Confirmar vínculo. No envíes tu contraseña aquí.", "Code received. Return to the resort page on your PC and confirm the link. Never send your password here."); }
            var proposal = ResortLanguage.Parse(input.Text, clock.GetUtcNow());
            var confirm = ResortLanguage.Confirmation(input.Text);
            if (proposal is null && confirm is null)
                return Reply("CONFIRM_REFERENCE_REQUIRED", "Para confirmar, copia el comando Confirmar seguido del identificador completo de la oferta. Un sí solo no cambia una reserva.", "To confirm, copy Confirm followed by the full offer identifier. Yes alone does not change a booking.");
            if (proposal?.Action == "catalog")
            {
                var catalog = store.Catalog().Where(c => proposal.Type is null || c.Id == proposal.Type);
                return new(string.Join("\n\n", catalog.Select(c => $"{(en ? c.NameEn : c.NameEs)} · {Money(c.NightlyCents)} {(en ? "per night; fictional rate" : "por noche; tarifa ficticia")} · {(en ? "guests" : "huéspedes")}: {c.Capacity}\n{string.Join(", ", en ? c.AmenitiesEn : c.AmenitiesEs)}")) + (en ? "\nProvide arrival/departure YYYY-MM-DD and guest count to check dates." : "\nIndica entrada/salida YYYY-MM-DD y huéspedes para consultar fechas."), "RESORT_CATALOG");
            }
            if (proposal?.Action == "availability")
            {
                var nights = 2;
                if (proposal.Arrival is not null && proposal.Departure is not null && DateOnly.TryParseExact(proposal.Arrival, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) && DateOnly.TryParseExact(proposal.Departure, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) nights = d.DayNumber - a.DayNumber;
                var options = store.Catalog().Where(c => proposal.Type is null || c.Id == proposal.Type).Where(c => c.Capacity >= proposal.Guests)
                    .Select(c => $"{(en ? c.NameEn : c.NameEs)} ({Money(c.NightlyCents)} / {(en ? "night" : "noche")}):\n{string.Join("\n", store.Available(c.Id, nights, proposal.Guests, proposal.Arrival))}");
                return new((en ? $"Available options for {nights} nights; " : $"Opciones disponibles para {nights} noches; ") + (en ? "observed " : "consulta ") + clock.GetUtcNow().ToString("u", CultureInfo.InvariantCulture) + "\n" + string.Join("\n\n", options) + (en ? "\nAvailability is rechecked at confirmation. Specify full dates and guests." : "\nSe revalida al confirmar. Especifica fechas completas y huéspedes."), "RESORT_AVAILABILITY");
            }
            var session = store.LinkedSession(input.Route, input.Epoch);
            var actor = session is null ? null : await identities.FindSessionAsync(session.Value, clock.GetUtcNow(), ct);
            if (actor is null || actor.MemberRef is null || !actor.Roles.HasFlag(ActorRoles.Customer))
                return Reply("CHANNEL_LINK_REQUIRED", "Para gestionar reservas propias, abre el resort en tu PC, inicia sesión y usa Vincular WhatsApp. Después podrás reservar, cambiar y cancelar aquí.", "To manage your bookings, open the resort on your PC, sign in and link WhatsApp. Then you can book, change and cancel here.");
            if (confirm is Guid offerId)
            {
                var receipt = store.Confirm(actor, offerId, input.Route, input.Epoch);
                if (receipt.Status != "Completed") return Error(receipt.ReasonCode, en);
                var outcome = receipt.ReasonCode switch { "CREATE_COMPLETED" => en ? "Booking confirmed" : "Reserva confirmada", "CHANGE_COMPLETED" => en ? "Dates updated" : "Fechas actualizadas", "CANCEL_COMPLETED" => en ? "Booking cancelled" : "Reserva cancelada", _ => en ? "Action confirmed" : "Acción confirmada" };
                return new(outcome + "\n" + StayText(receipt.Stay!, en) + (en ? "\nFictional booking; no money charged." : "\nReserva ficticia; no se realizó ningún cobro."), "RESORT_COMPLETED");
            }
            if (proposal!.Action == "list")
            { var stays = store.MyStays(actor); return new(stays.Length == 0 ? (en ? "You have no demo stays." : "No tienes estadías de demo.") : string.Join("\n\n", stays.Take(8).Select(s => StayText(s, en))), "RESORT_MY_STAYS"); }
            if (proposal.Action != "cancel" && (proposal.Arrival is null || proposal.Departure is null || proposal.Action == "create" && proposal.Type is null))
                return Reply("RESORT_DETAILS_REQUIRED", "Indica categoría (Estándar, Deluxe o Suite), entrada y salida YYYY-MM-DD y huéspedes. Ejemplo: Reservar Suite 2026-10-20 a 2026-10-22 para 2 personas. Para cambiar, incluye el código STAY de tu reserva.", "Specify category (Standard, Deluxe or Suite), arrival/departure YYYY-MM-DD and guests. Example: Book Suite 2026-10-20 to 2026-10-22 for 2 guests. For a change, include your STAY booking code.");
            var offer = store.Offer(actor, proposal, input.EventKey, input.Route, input.Epoch);
            var r = offer.Request;
            return new($"{(en ? "Proposal — not booked yet" : "Propuesta — todavía no ejecutada")}: {(r.Action == "create" ? (en ? "book" : "reservar") : r.Action == "change" ? (en ? "change dates" : "cambiar fechas") : (en ? "cancel" : "cancelar"))}\n{r.StayId} {r.Type}: {r.Arrival} → {r.Departure}; {r.Guests} {(en ? "guests" : "huéspedes")}\n{(en ? "Fictional total" : "Total ficticio")}: {Money(offer.TotalCents)}" + (r.Action == "change" ? $"\n{(en ? "Previous total / difference" : "Total anterior / diferencia")}: {Money(offer.PreviousTotalCents)} / {Money(offer.TotalCents - offer.PreviousTotalCents)}" : "") + $"\n{(en ? "Expires" : "Vence")}: {offer.ExpiresAt:u}\n{(en ? "Confirm" : "Confirmar")} {offer.Id}", "RESORT_OFFER");
        }
        catch (RequestRejected rejected) { return Error(rejected.Code, en); }
    }
    private static string StayText(ResortStay s, bool en) => $"{s.Id} · {s.Type} · {s.Arrival} → {s.Departure}\n{s.Guests} {(en ? "guests" : "huéspedes")} · {Money(s.TotalCents)} · {s.Status}";
    private static ResortChannelResult Error(string code, bool en)
    {
        var text = code switch
        {
            "DATES_OCCUPIED" => en ? "Those nights are occupied or blocked. Your previous booking is unchanged. Ask for available dates." : "Esas noches están ocupadas o bloqueadas. Tu reserva anterior se conserva. Pide fechas disponibles.",
            "OUTSIDE_FREE_WINDOW" => en ? "Changes/cancellation require at least 72 hours before the current arrival. No booking was changed." : "Cambios/cancelaciones requieren al menos 72 horas antes de la llegada actual. No se cambió la reserva.",
            "OFFER_EXPIRED" or "PRICE_CHANGED" or "VERSION_CONFLICT" => en ? "The proposal expired or its price/state changed. Request a new proposal." : "La propuesta venció o cambió su precio/estado. Solicita una propuesta nueva.",
            "LINK_CODE_INVALID" or "LINK_RATE_LIMITED" => en ? "Link code invalid/expired or too many attempts. Generate a new code in your local authenticated page." : "Código inválido/vencido o demasiados intentos. Genera otro en tu página local con sesión.",
            "RESOURCE_NOT_FOUND" => en ? "That booking/proposal is unavailable for your current linked account." : "Esa reserva/propuesta no está disponible para tu cuenta vinculada actual.",
            _ => en ? "Check full dates, category, guest capacity and current linked session. No action was confirmed." : "Revisa fechas completas, categoría, capacidad y sesión vinculada vigente. No se confirmó ninguna acción."
        };
        return new(text + " [" + code + "]", code);
    }
}

public sealed class ResortRecoveryWorker(ResortStore store, IOperationalStore identities, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { foreach (var session in store.QueuedSessions()) { var actor = await identities.FindSessionAsync(session, clock.GetUtcNow(), stoppingToken); if (actor is not null && actor.Roles.HasFlag(ActorRoles.Customer)) store.Recover(actor); else store.RejectRevokedQueue(session); } }
            catch (Exception error) when (error is SqliteException or RequestRejected or OperationalUnavailable) { /* Codes and durable queue suffice; never export payload exceptions. */ }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
