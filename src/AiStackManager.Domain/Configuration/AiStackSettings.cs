namespace AiStackManager.Domain.Configuration;

public sealed record AiStackSettings
{
    public string InferenceProvider { get; init; } = "ollama";
    public string Model { get; init; } = "qwen25-coder-14b-64k";
    public string OpenClawModelRef { get; init; } = "";
    public string OllamaBaseUrl { get; init; } = "http://127.0.0.1:11434";
    public string HermesBaseUrl { get; init; } = "http://127.0.0.1:11434";
    public string HermesProvider { get; init; } = "ollama";
    public string OpenClawBaseUrl { get; init; } = "http://127.0.0.1:11434";
    public string OpenClawApiKey { get; init; } = "ollama-local";
    public int ContextLength { get; init; } = 65536;
    public int OpenClawPort { get; init; } = 18789;
    public string OpenClawWorkspace { get; init; } = "~/src/test-openclaw";
    public bool OwnOllamaService { get; init; } = true;
    public bool OwnInferenceRuntime { get; init; } = true;
    public bool AutoStartOnHostStartup { get; init; }
    public bool AllowManagementWithoutTokenFromLoopback { get; init; } = true;
    public string ManagementToken { get; init; } = "";

    public string EffectiveOpenClawModelRef =>
        string.IsNullOrWhiteSpace(OpenClawModelRef) ? $"{InferenceProvider}/{Model}" : OpenClawModelRef;
}
