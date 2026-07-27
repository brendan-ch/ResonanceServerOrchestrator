using ResonanceServerOrchestrator.Services;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class DisabledSteamTicketValidatorTests
{
    private readonly DisabledSteamTicketValidator _validator = new();

    [Theory]
    [InlineData("14000000AABBCCDD")]
    [InlineData("not-a-real-ticket")]
    [InlineData("")]
    public async Task ValidateAsync_AnyTicket_IsAccepted(string ticketHex)
    {
        var result = await _validator.ValidateAsync(ticketHex, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.False(result.IsBanned);
        Assert.Null(result.FailureDetail);
    }

    [Fact]
    public async Task ValidateAsync_AssertsNoIdentity()
    {
        var result = await _validator.ValidateAsync("14000000AABBCCDD", CancellationToken.None);

        Assert.Null(result.SteamId);
    }

    [Fact]
    public async Task ValidateAsync_CompletesEvenWhenTheCallerHasAlreadyCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await _validator.ValidateAsync("14000000AABBCCDD", cancellation.Token);

        Assert.True(result.IsValid);
    }
}
