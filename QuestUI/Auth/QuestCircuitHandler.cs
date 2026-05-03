using Microsoft.AspNetCore.Components.Server.Circuits;

namespace QuestUI.Auth;

public sealed class QuestCircuitHandler(CustomAuthStateProvider authStateProvider) : CircuitHandler
{
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        => authStateProvider.LogoutAsync();
}
