namespace OneApi.Common.Dispatch;

/// <summary>
/// Internal gRPC message size cap for LLM dispatch RPCs (Demux.Gateway ↔ OneApi Gateway, etc.).
/// Must match on both client and server; default grpc-dotnet limit is 4 MB.
/// </summary>
public static class GrpcMessageLimits
{
    public const int MaxBytes = 8 * 1024 * 1024;
}
