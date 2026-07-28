namespace ResonanceServerOrchestrator.Contracts;

/// <remarks>
/// The ordinals are part of the wire contract: clients may send the numeric form. Pin every
/// value explicitly so inserting a platform can never renumber the existing ones.
/// </remarks>
public enum Platform
{
    Steam = 0,
    Dummy = 1,
}
