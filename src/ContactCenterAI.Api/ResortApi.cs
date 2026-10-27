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
        // Web chat: same turn logic as the channel bridge, but the actor is the verified cookie session (or none).
        // The text interpretation only proposes; offers still need explicit confirmation through /offers/{id}/confirm.
        app.MapGet("/v2/resort/chat/profile", (IResortInterpreter interpreter) => Results.Ok(new { mode = interpreter.Mode, providerId = interpreter.ProviderId, examples = ResortConversation.Examples(store.Today()) }));
        app.MapPost("/v2/resort/chat", async (HttpContext h, ResortChatRequest r, ResortConversation conversation, IResortInterpreter interpreter) =>
        {
            var key = h.Request.Headers["Idempotency-Key"].Count == 1 ? h.Request.Headers["Idempotency-Key"].ToString() : "";
            if (key.Length is < 1 or > 128) throw new RequestRejected(400, "REQUEST_KEY_REQUIRED");
            if (r.Language is not ("es" or "en") || string.IsNullOrWhiteSpace(r.Text) || r.Text.Length > 2000) throw new RequestRejected(400, "CHAT_INPUT_INVALID");
            return Results.Ok(await conversation.RespondAsync(r.Text, r.Language == "en", interpreter, h.Items["Actor"] as Actor, channel: false, key, "web", 1, h.RequestAborted));
        });
        app.MapPost("/v2/resort/link-codes", (HttpContext h) => Results.Ok(new { code = store.CreateLinkCode(Actor(h)), expiresInSeconds = 300 }));
        app.MapGet("/v2/resort/link-status", (HttpContext h) => Results.Ok(store.LinkStatus(Actor(h))));
        app.MapPost("/v2/resort/link-confirm", (HttpContext h) => { store.ConfirmLink(Actor(h)); return Results.Ok(new { linked = true }); });
        app.MapPost("/v2/resort/unlink", (HttpContext h) => { store.Unlink(Actor(h)); return Results.NoContent(); });
        app.MapGet("/v2/resort/blocks", (HttpContext h) => Results.Ok(store.Blocks(Actor(h))));
        app.MapPost("/v2/resort/blocks", (HttpContext h, ResortBlockRequest r) => Results.Ok(store.Block(Actor(h), r)));
        app.MapDelete("/v2/resort/blocks/{id}", (HttpContext h, string id, long version) => { store.RemoveBlock(Actor(h), id, version); return Results.NoContent(); });
        app.MapPost("/v2/resort/rates", (HttpContext h, RateUpdate r) => { store.UpdateRate(Actor(h), r.Type, r.Cents); return Results.NoContent(); });
        // The WhatsApp bridge is optional: without a configured key the route is absent and the middleware above still rejects it.
        if (bridgeKey.Length >= 32) app.MapPost("/internal/resort/channel", async (ResortChannelRequest r, ResortConversation conversation) =>
        {
            if (r.Channel != "whatsapp" || r.Endpoint != endpoint || string.IsNullOrEmpty(endpoint) || r.Route is null || r.Text is null || r.EventKey is null || r.Route.Length != 64 || !r.Route.All(Uri.IsHexDigit) || r.Epoch < 1 || r.Text.Length > 2000 || r.EventKey.Length > 128 || r.Language is not ("es" or "en")) throw new RequestRejected(400, "CHANNEL_SCOPE_INVALID");
            return Results.Ok(await conversation.AnswerAsync(r, CancellationToken.None));
        });
    }
}
public sealed record RateUpdate(string Type, long Cents);
public sealed record ResortExamples(string[] Es, string[] En);

public sealed class ResortConversation(ResortStore store, IOperationalStore identities, TimeProvider clock)
{
    private static readonly DeterministicResortInterpreter Rules = new();
    private static string Money(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture) + " USD";
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static ResortExamples Examples(DateOnly today) => new(
        [ "¿Qué incluye la Suite?", $"Disponibilidad Suite {Iso(today.AddDays(14))} a {Iso(today.AddDays(16))}", $"Reservar Deluxe {Iso(today.AddDays(21))} a {Iso(today.AddDays(23))} para 2 personas", "Mis reservas", "¿Cuál es la política de cancelación?"],
        [ "What does the Suite include?", $"Available Suite {Iso(today.AddDays(14))} to {Iso(today.AddDays(16))}", $"Book Deluxe {Iso(today.AddDays(21))} to {Iso(today.AddDays(23))} for 2 guests", "My bookings", "What is the cancellation policy?"]);
    // WhatsApp bridge: the actor comes only from a confirmed link bound to a live session; the channel stays deterministic.
    public async Task<ResortChannelResult> AnswerAsync(ResortChannelRequest input, CancellationToken ct)
    {
        var en = input.Language == "en";
        try
        {
            if (ResortLanguage.LinkCode(input.Text) is string code)
            { store.SubmitLinkCode(code, input.Route, input.Epoch); return new(en ? "Code received. Return to the resort page on your PC and confirm the link. Never send your password here." : "Código recibido. Vuelve a la página del resort en tu PC y pulsa Confirmar vínculo. No envíes tu contraseña aquí.", "LINK_PENDING"); }
            var session = store.LinkedSession(input.Route, input.Epoch);
            var actor = session is null ? null : await identities.FindSessionAsync(session.Value, clock.GetUtcNow(), ct);
            var turn = await RespondAsync(input.Text, en, Rules, actor, channel: true, input.EventKey, input.Route, input.Epoch, ct);
            return new(turn.Text, turn.Code);
        }
        catch (RequestRejected rejected) { return new(Error(rejected.Code, en), rejected.Code); }
    }
    public async Task<ResortTurn> RespondAsync(string text, bool en, IResortInterpreter interpreter, Actor? actor, bool channel, string key, string route, long epoch, CancellationToken ct)
    {
        var today = store.Today();
        string T(string es, string english) => en ? english : es;
        int? statedGuests = null; // what the user actually said; ResortRequest.Guests keeps the parser default for v0.12 compatibility
        ResortTurn Turn(string code, string message, ResortRequest? interpreted = null, string[]? missing = null, string[]? assumptions = null) =>
            new(code, message, interpreter.Mode, interpreted, missing ?? [], assumptions ?? [], StatedGuests: statedGuests);
        try
        {
            var safe = ResortLanguage.SanitizeChannelText(text);
            // Offer-bound confirmation is a closed command, never interpreted by a model and never a bare "yes".
            var confirm = ResortLanguage.Confirmation(safe);
            ResortInterpretation interpretation = confirm is null ? await interpreter.InterpretAsync(safe, en ? "en" : "es", today, ct) : new(null, null, false, "command");
            if (interpretation.Error is string modelError)
                return Turn(modelError, T($"La IA local no devolvió una interpretación válida ({modelError}). No se usó otro intérprete y no se preparó ninguna acción.", $"The local AI did not return a valid interpretation ({modelError}). No other interpreter was used and no action was prepared."));
            var proposal = interpretation.Request; statedGuests = interpretation.Guests;
            if (proposal is null && confirm is null)
                return ResortLanguage.IsConfirmation(safe)
                    ? Turn("CONFIRM_REFERENCE_REQUIRED", channel ? T("Para confirmar, copia el comando Confirmar seguido del identificador completo de la oferta. Un sí solo no cambia una reserva.", "To confirm, copy Confirm followed by the full offer identifier. Yes alone does not change a booking.") : T("Para confirmar usa el botón Confirmar de la propuesta. Un sí escrito no cambia una reserva.", "Use the proposal's Confirm button. A typed yes does not change a booking."))
                    : Turn("RESORT_OUT_OF_SCOPE", T("No tengo información para responder eso en esta demo y no voy a inventarla. Puedo mostrar habitaciones y precios, consultar disponibilidad, explicar la política y preparar, cambiar o cancelar reservas ficticias.", "I have no information to answer that in this demo and will not make it up. I can show rooms and prices, check availability, explain the policy and prepare, change or cancel fictional bookings.")) with { Suggestions = Suggestions(today, en) };
            if (proposal?.Action == "policy")
                return Turn("RESORT_POLICY", T("Política de la demo: estadías de 1 a 14 noches, hasta 90 días de anticipación. Cambios y cancelaciones sin penalidad hasta 72 horas antes de la llegada (15:00, hora de Santo Domingo). Una propuesta vence a los 5 minutos y no retiene la habitación: se revalida al confirmar. No hay cobros reales.", "Demo policy: stays of 1–14 nights, up to 90 days ahead. Free changes and cancellations until 72 hours before arrival (15:00 Santo Domingo time). A proposal expires after 5 minutes and does not hold the room: it is rechecked at confirmation. No real charges."), proposal);
            if (proposal?.Action == "catalog")
            {
                var catalog = store.Catalog().Where(c => proposal.Type is null || c.Id == proposal.Type).ToArray();
                return Turn("RESORT_CATALOG", string.Join("\n\n", catalog.Select(c => $"{(en ? c.NameEn : c.NameEs)} · {Money(c.NightlyCents)} {(en ? "per night; fictional rate" : "por noche; tarifa ficticia")} · {(en ? "guests" : "huéspedes")}: {c.Capacity}\n{string.Join(", ", en ? c.AmenitiesEn : c.AmenitiesEs)}")) + (en ? "\nProvide arrival/departure YYYY-MM-DD and guest count to check dates." : "\nIndica entrada/salida YYYY-MM-DD y huéspedes para consultar fechas."), proposal) with { Catalog = catalog };
            }
            if (proposal?.Action == "availability")
            {
                var assumptions = new List<string>();
                var nights = 2;
                if (proposal.Arrival is not null && proposal.Departure is not null && DateOnly.TryParseExact(proposal.Arrival, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) && DateOnly.TryParseExact(proposal.Departure, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) nights = d.DayNumber - a.DayNumber;
                else assumptions.Add(T("Sin fechas completas: se buscan estadías de 2 noches desde mañana.", "No full dates: searching 2-night stays from tomorrow."));
                var guests = interpretation.Guests ?? 1;
                if (interpretation.Guests is null) assumptions.Add(T("Sin número de huéspedes: se muestran todas las habitaciones; la capacidad se valida al reservar.", "No guest count: all rooms are shown; capacity is checked when booking."));
                var fitting = store.Catalog().Where(c => proposal.Type is null || c.Id == proposal.Type).Where(c => c.Capacity >= guests).ToArray();
                if (fitting.Length == 0) return Turn("STAY_INPUT_INVALID", T($"Ninguna habitación admite {guests} huéspedes; la capacidad máxima es 4 (Suite).", $"No room fits {guests} guests; the maximum capacity is 4 (Suite)."), proposal);
                var options = fitting.Select(c => new ResortAvailability(c.Id, c.NightlyCents, c.Capacity, store.Available(c.Id, nights, guests, proposal.Arrival))).ToArray();
                var body = string.Join("\n\n", options.Select(o => { var c = fitting.Single(x => x.Id == o.Type); return $"{(en ? c.NameEn : c.NameEs)} ({Money(c.NightlyCents)} / {(en ? "night" : "noche")}):\n{(o.Ranges.Length == 0 ? T("sin fechas libres en ese rango", "no free dates in that range") : string.Join("\n", o.Ranges))}"; }));
                return Turn("RESORT_AVAILABILITY", (en ? $"Available options for {nights} nights; " : $"Opciones disponibles para {nights} noches; ") + (en ? "observed " : "consulta ") + clock.GetUtcNow().ToString("u", CultureInfo.InvariantCulture) + "\n" + body + (en ? "\nAvailability is rechecked at confirmation. Specify full dates and guests." : "\nSe revalida al confirmar. Especifica fechas completas y huéspedes."), proposal, assumptions: assumptions.ToArray()) with { Availability = options };
            }
            // Everything below acts on a private account: the actor is resolved by the server, never from the text.
            if (actor is null || actor.PrincipalId is null || actor.MemberRef is null || !actor.Roles.HasFlag(ActorRoles.Customer))
                return channel
                    ? Turn("CHANNEL_LINK_REQUIRED", T("Para gestionar reservas propias, abre el resort en tu PC, inicia sesión y usa Vincular WhatsApp. Después podrás reservar, cambiar y cancelar aquí.", "To manage your bookings, open the resort on your PC, sign in and link WhatsApp. Then you can book, change and cancel here."), proposal)
                    : actor?.PrincipalId is not null
                        ? Turn("VERIFIED_CUSTOMER_REQUIRED", T("Esta cuenta no es de cliente: no puede consultar ni preparar reservas.", "This account is not a customer account: it cannot view or prepare bookings."), proposal)
                        : Turn("SIGN_IN_REQUIRED", T("Para ver o preparar reservas inicia sesión como cliente (customer-a o customer-b). Habitaciones, precios y disponibilidad no requieren sesión.", "Sign in as a customer (customer-a or customer-b) to view or prepare bookings. Rooms, prices and availability need no sign-in."), proposal);
            if (confirm is Guid offerId)
            {
                var receipt = store.Confirm(actor, offerId, route, epoch);
                if (receipt.Status != "Completed") return Turn(receipt.ReasonCode, Error(receipt.ReasonCode, en)) with { Receipt = receipt };
                var outcome = receipt.ReasonCode switch { "CREATE_COMPLETED" => en ? "Booking confirmed" : "Reserva confirmada", "CHANGE_COMPLETED" => en ? "Dates updated" : "Fechas actualizadas", "CANCEL_COMPLETED" => en ? "Booking cancelled" : "Reserva cancelada", _ => en ? "Action confirmed" : "Acción confirmada" };
                return Turn("RESORT_COMPLETED", outcome + "\n" + StayText(receipt.Stay!, en) + (en ? "\nFictional booking; no money charged." : "\nReserva ficticia; no se realizó ningún cobro.")) with { Receipt = receipt };
            }
            if (proposal!.Action == "list")
            { var stays = store.MyStays(actor); return Turn("RESORT_MY_STAYS", stays.Length == 0 ? (en ? "You have no demo stays." : "No tienes estadías de demo.") : string.Join("\n\n", stays.Take(8).Select(s => StayText(s, en))), proposal) with { Stays = stays }; }
            if (proposal.Action is "change" or "cancel" && proposal.StayId is null)
            {
                var own = store.MyStays(actor).Where(s => s.Status == "Confirmed").ToArray();
                return Turn("STAY_REFERENCE_REQUIRED", T("¿Qué reserva? Indica su código STAY", "Which booking? Include its STAY code") + (own.Length == 0 ? T(". No tienes reservas confirmadas.", ". You have no confirmed bookings.") : ": " + string.Join(", ", own.Select(s => s.Id)) + "."), proposal, ["stayId"]) with { Stays = own };
            }
            var request = proposal;
            if (request.Action != "cancel")
            {
                var missing = new List<string>();
                if (request.Action == "create" && request.Type is null) missing.Add("type");
                if (request.Arrival is null || request.Departure is null) missing.Add(ResortLanguage.DateCount(safe) == 1 ? "departure" : "dates");
                if (interpretation.GuestsAmbiguous) missing.Add("guests-exact");
                else if (interpretation.Guests is int stated) request = request with { Guests = stated };
                // A change without a stated guest count keeps the booking's own count instead of a default.
                else if (request.Action == "change") request = request with { Guests = store.MyStays(actor).FirstOrDefault(s => s.Id == request.StayId)?.Guests ?? throw new RequestRejected(404, "RESOURCE_NOT_FOUND") };
                else missing.Add("guests");
                if (missing.Count > 0) return Turn("RESORT_DETAILS_REQUIRED", Clarify(missing, request, today, en), request, missing.ToArray());
            }
            var offer = store.Offer(actor, request, key, route, epoch);
            var r = offer.Request;
            return Turn("RESORT_OFFER", $"{(en ? "Proposal — not booked yet" : "Propuesta — todavía no ejecutada")}: {(r.Action == "create" ? (en ? "book" : "reservar") : r.Action == "change" ? (en ? "change dates" : "cambiar fechas") : (en ? "cancel" : "cancelar"))}\n{r.StayId} {r.Type}: {r.Arrival} → {r.Departure}; {r.Guests} {(en ? "guests" : "huéspedes")}\n{(en ? "Fictional total" : "Total ficticio")}: {Money(offer.TotalCents)}" + (r.Action == "change" ? $"\n{(en ? "Previous total / difference" : "Total anterior / diferencia")}: {Money(offer.PreviousTotalCents)} / {Money(offer.TotalCents - offer.PreviousTotalCents)}" : "") + $"\n{(en ? "Expires" : "Vence")}: {offer.ExpiresAt:u}" + (channel ? $"\n{(en ? "Confirm" : "Confirmar")} {offer.Id}" : ""), request) with { Offer = offer };
        }
        catch (RequestRejected rejected) { return Turn(rejected.Code, Error(rejected.Code, en)); }
    }
    private static string[] Suggestions(DateOnly today, bool en) => en ? Examples(today).En : Examples(today).Es;
    private static string Clarify(List<string> missing, ResortRequest request, DateOnly today, bool en)
    {
        var parts = missing.Select(m => m switch
        {
            "type" => en ? "the room (Standard, Deluxe or Suite)" : "la habitación (Estándar, Deluxe o Suite)",
            "departure" => en ? "the check-out date (YYYY-MM-DD)" : "la fecha de salida (AAAA-MM-DD)",
            "dates" => en ? "check-in and check-out dates (YYYY-MM-DD)" : "las fechas de entrada y salida (AAAA-MM-DD)",
            "guests-exact" => en ? "one exact guest count" : "un número exacto de huéspedes",
            _ => en ? "the number of guests" : "el número de huéspedes"
        });
        string a = Iso(today.AddDays(14)), d = Iso(today.AddDays(16)), type = request.Type ?? "suite";
        var example = request.Action == "change"
            ? (en ? $"Change {request.StayId} {a} to {d}" : $"Cambiar {request.StayId} {a} a {d}")
            : (en ? $"Book {type} {request.Arrival ?? a} to {request.Departure ?? d} for 2 guests" : $"Reservar {type} {request.Arrival ?? a} a {request.Departure ?? d} para 2 personas");
        return (en ? "To prepare the proposal I still need " : "Para preparar la propuesta me falta ") + string.Join(en ? " and " : " y ", parts) + (en ? ". Example: " : ". Ejemplo: ") + example;
    }
    private static string StayText(ResortStay s, bool en) => $"{s.Id} · {s.Type} · {s.Arrival} → {s.Departure}\n{s.Guests} {(en ? "guests" : "huéspedes")} · {Money(s.TotalCents)} · {s.Status}";
    private static string Error(string code, bool en) => code switch
    {
        "DATES_OCCUPIED" => en ? "Those nights are occupied or blocked. Your previous booking is unchanged. Ask for available dates." : "Esas noches están ocupadas o bloqueadas. Tu reserva anterior se conserva. Pide fechas disponibles.",
        "OUTSIDE_FREE_WINDOW" => en ? "Changes/cancellation require at least 72 hours before the current arrival. No booking was changed." : "Cambios/cancelaciones requieren al menos 72 horas antes de la llegada actual. No se cambió la reserva.",
        "OFFER_EXPIRED" or "PRICE_CHANGED" or "VERSION_CONFLICT" => en ? "The proposal expired or its price/state changed. Request a new proposal." : "La propuesta venció o cambió su precio/estado. Solicita una propuesta nueva.",
        "LINK_CODE_INVALID" or "LINK_RATE_LIMITED" => en ? "Link code invalid/expired or too many attempts. Generate a new code in your local authenticated page." : "Código inválido/vencido o demasiados intentos. Genera otro en tu página local con sesión.",
        "RESOURCE_NOT_FOUND" => en ? "That booking/proposal is unavailable for your current account." : "Esa reserva/propuesta no está disponible para tu cuenta actual.",
        "STAY_INPUT_INVALID" => en ? "Those dates or guests are outside the rules: 1–14 nights, from today up to 90 days ahead, and within the room's capacity. No action was prepared." : "Esas fechas o huéspedes no cumplen las reglas: 1 a 14 noches, desde hoy hasta 90 días, y dentro de la capacidad de la habitación. No se preparó ninguna acción.",
        "SENSITIVE_PAYMENT_DATA" => en ? "Do not send card numbers: this demo has no payments. The message was not processed." : "No envíes números de tarjeta: esta demo no tiene pagos. El mensaje no se procesó.",
        "STAY_NOT_CONFIRMED" => en ? "That booking is already cancelled." : "Esa reserva ya está cancelada.",
        _ => en ? "Check full dates, category, guest capacity and current linked session. No action was confirmed." : "Revisa fechas completas, categoría, capacidad y sesión vinculada vigente. No se confirmó ninguna acción."
    } + " [" + code + "]";
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
