namespace ResonanceServerOrchestrator.Stores;

internal sealed record MatchTombstone(byte[] MatchKeyHash, DateTimeOffset DestroyedAt);
