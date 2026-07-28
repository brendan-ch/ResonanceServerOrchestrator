#nullable enable

namespace Resonance.Contracts
{
    public sealed class LeaveMatchDto
    {
        public LeaveMatchDto(PlatformUserInformationDto platformUserInformation)
        {
            PlatformUserInformation = platformUserInformation;
        }

        public PlatformUserInformationDto PlatformUserInformation { get; }
    }
}
