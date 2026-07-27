using System.Text.Json;
using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;

namespace ResonanceServerOrchestrator.Services;

public sealed class SteamWebApiTicketValidator(
    HttpClient httpClient,
    IOptions<OrchestratorOptions> options,
    TimeProvider timeProvider,
    ILogger<SteamWebApiTicketValidator> logger) : ISteamTicketValidator
{
    private const string AuthenticateUserTicketUrl =
        "https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/";

    private const string AuthenticatedResult = "OK";

    private static readonly TimeSpan SteamWebApiTimeout = TimeSpan.FromSeconds(5);

    public async Task<SteamTicketValidationResult> ValidateAsync(
        string ticketHex, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketHex))
            return SteamTicketValidationResult.Rejected("No Steam authentication ticket was supplied.");

        using var timeout = new CancellationTokenSource(SteamWebApiTimeout, timeProvider);
        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            using var request = BuildAuthenticateUserTicketRequest(ticketHex);
            using var response = await httpClient.SendAsync(request, linkedCancellation.Token);

            if (!response.IsSuccessStatusCode)
                return FailClosed($"Steam answered with HTTP {(int)response.StatusCode}.");

            return Interpret(await response.Content.ReadAsStringAsync(linkedCancellation.Token));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return FailClosed(
                $"Steam did not answer within {SteamWebApiTimeout.TotalSeconds:0.#} seconds.");
        }
        catch (HttpRequestException exception)
        {
            return FailClosed($"Steam could not be reached: {exception.Message}");
        }
        catch (IOException exception)
        {
            return FailClosed($"The response from Steam could not be read: {exception.Message}");
        }
        catch (Exception exception)
        {
            return FailClosed($"Validating the ticket against Steam failed: {exception.Message}");
        }
    }

    private HttpRequestMessage BuildAuthenticateUserTicketRequest(string ticketHex) =>
        new(HttpMethod.Post, AuthenticateUserTicketUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["key"] = options.Value.SteamPublisherWebApiKey,
                ["appid"] = options.Value.SteamAppId.ToString(),
                ["ticket"] = ticketHex,
            }),
        };

    private SteamTicketValidationResult Interpret(string payload)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            return FailClosed($"Steam answered with unparseable JSON: {exception.Message}");
        }

        using (document)
            return InterpretResponseEnvelope(document.RootElement);
    }

    private SteamTicketValidationResult InterpretResponseEnvelope(JsonElement root)
    {
        if (root.ValueKind is not JsonValueKind.Object ||
            !root.TryGetProperty("response", out var response) ||
            response.ValueKind is not JsonValueKind.Object)
            return FailClosed("Steam answered without a 'response' object.");

        if (response.TryGetProperty("error", out var error))
            return RejectTicket($"Steam rejected the ticket: {Describe(error)}");

        if (!response.TryGetProperty("params", out var parameters) ||
            parameters.ValueKind is not JsonValueKind.Object)
            return FailClosed("Steam answered without a 'params' object.");

        return InterpretAuthenticationParameters(parameters);
    }

    private SteamTicketValidationResult InterpretAuthenticationParameters(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("result", out var result) ||
            result.ValueKind is not JsonValueKind.String ||
            !result.ValueEquals(AuthenticatedResult))
            return RejectTicket(
                $"Steam did not report '{AuthenticatedResult}' for the ticket.");

        var steamId = ReadString(parameters, "steamid");
        if (string.IsNullOrWhiteSpace(steamId))
            return FailClosed("Steam authenticated the ticket without naming a steamid.");

        if (TryReadAppId(parameters, out var authenticatedAppId) &&
            authenticatedAppId != options.Value.SteamAppId)
            return RejectTicket(
                $"Steam authenticated the ticket for app {authenticatedAppId}, " +
                $"not app {options.Value.SteamAppId}.");

        if (ReadFlag(parameters, "vacbanned"))
            return SteamTicketValidationResult.RejectedAsBanned(steamId, "The Steam account is VAC banned.");

        if (ReadFlag(parameters, "publisherbanned"))
            return SteamTicketValidationResult.RejectedAsBanned(
                steamId, "The Steam account is publisher banned.");

        return SteamTicketValidationResult.Authenticated(steamId);
    }

    private SteamTicketValidationResult FailClosed(string failureDetail)
    {
        logger.LogWarning("Steam ticket validation failed closed. {FailureDetail}", failureDetail);
        return SteamTicketValidationResult.Rejected(failureDetail);
    }

    private SteamTicketValidationResult RejectTicket(string failureDetail)
    {
        logger.LogDebug("Steam ticket validation rejected a ticket. {FailureDetail}", failureDetail);
        return SteamTicketValidationResult.Rejected(failureDetail);
    }

    private static string Describe(JsonElement error) =>
        error.ValueKind is JsonValueKind.Object
            ? $"{ReadString(error, "errordesc") ?? "no description"} " +
              $"(errorcode {ReadString(error, "errorcode") ?? "unknown"})"
            : "no description";

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
            ? property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null,
            }
            : null;

    private static bool TryReadAppId(JsonElement parameters, out uint appId)
    {
        appId = 0;

        if (!parameters.TryGetProperty("appid", out var property))
            return false;

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetUInt32(out appId),
            JsonValueKind.String => uint.TryParse(property.GetString(), out appId),
            _ => false,
        };
    }

    private static bool ReadFlag(JsonElement parameters, string propertyName) =>
        parameters.TryGetProperty(propertyName, out var property) &&
        property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => bool.TryParse(property.GetString(), out var flag) && flag,
            _ => false,
        };
}
