using AiStackManager.Domain.Common;
using AiStackManager.Domain.Configuration;

namespace AiStackManager.Domain.Stack;

public enum AiStackPhase { Unknown, Stopped, Starting, Running, Degraded, Stopping, Failed }
public enum ComponentState { Unknown, Missing, Stopped, Starting, Running, Degraded, Failed }

public sealed class AiStackComponent
{
    private readonly List<string> _diagnostics = [];

    public AiStackComponent(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Component name is required.", nameof(name)) : name;
    }

    public string Name { get; }
    public ComponentState State { get; private set; } = ComponentState.Unknown;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    public void MarkRunning(string diagnostic) => Mark(ComponentState.Running, diagnostic);
    public void MarkStopped(string diagnostic) => Mark(ComponentState.Stopped, diagnostic);
    public void MarkFailed(string diagnostic) => Mark(ComponentState.Failed, diagnostic);
    public void MarkDegraded(string diagnostic) => Mark(ComponentState.Degraded, diagnostic);

    private void Mark(ComponentState state, string diagnostic)
    {
        State = state;
        UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(diagnostic))
        {
            _diagnostics.Insert(0, diagnostic);
            if (_diagnostics.Count > 25) _diagnostics.RemoveAt(_diagnostics.Count - 1);
        }
    }
}

public sealed class AiStackAggregate
{
    private readonly Dictionary<string, AiStackComponent> _components = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ollama"] = new("ollama"),
        ["hermes"] = new("hermes"),
        ["openclaw"] = new("openclaw"),
        ["git"] = new("git"),
        ["dotnet"] = new("dotnet")
    };

    private AiStackAggregate(AiStackSettings settings)
    {
        Settings = settings;
        Phase = AiStackPhase.Stopped;
    }

    public AiStackSettings Settings { get; private set; }
    public AiStackPhase Phase { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, AiStackComponent> Components => _components;

    public static Fin<AiStackAggregate> Create(AiStackSettings settings)
    {
        var valid = ValidateSettings(settings);
        if (valid.IsFail) return Fin<AiStackAggregate>.Fail(valid.Error);

        return Fin<AiStackAggregate>.Succ(new AiStackAggregate(settings));
    }

    public Fin ApplySettings(AiStackSettings settings)
    {
        var valid = ValidateSettings(settings);
        if (valid.IsFail) return valid;

        Settings = settings;
        Touch();
        return Fin.Succ();
    }

    public Fin BeginStart()
    {
        if (Phase is AiStackPhase.Starting or AiStackPhase.Running)
            return Fin.Fail(AiStackError.Conflict($"Stack is already {Phase}."));

        Phase = AiStackPhase.Starting;
        Touch();
        return Fin.Succ();
    }

    public void CompleteStart()
    {
        Phase = Components.Values.Any(c => c.State is ComponentState.Failed or ComponentState.Degraded)
            ? AiStackPhase.Degraded
            : AiStackPhase.Running;
        Touch();
    }

    public Fin BeginStop()
    {
        if (Phase is AiStackPhase.Stopped or AiStackPhase.Stopping)
            return Fin.Fail(AiStackError.Conflict($"Stack is already {Phase}."));

        Phase = AiStackPhase.Stopping;
        Touch();
        return Fin.Succ();
    }

    public void CompleteStop()
    {
        Phase = AiStackPhase.Stopped;
        foreach (var component in Components.Values)
            component.MarkStopped("Stopped by stack lifecycle.");
        Touch();
    }

    public void Fail(string component, string reason)
    {
        Get(component).MarkFailed(reason);
        Phase = AiStackPhase.Failed;
        Touch();
    }

    public void Degrade(string component, string reason)
    {
        Get(component).MarkDegraded(reason);
        if (Phase is not AiStackPhase.Failed)
            Phase = AiStackPhase.Degraded;
        Touch();
    }

    public AiStackComponent Get(string component)
    {
        if (!_components.TryGetValue(component, out var value))
        {
            value = new AiStackComponent(component);
            _components.Add(component, value);
        }
        return value;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static Fin ValidateSettings(AiStackSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.InferenceProvider))
            return Fin.Fail(AiStackError.Validation("Inference provider is required."));

        if (string.IsNullOrWhiteSpace(settings.Model))
            return Fin.Fail(AiStackError.Validation("AI model is required."));

        if (settings.ContextLength < 64000)
            return Fin.Fail(AiStackError.Validation("Hermes Agent requires at least 64K context."));

        return Fin.Succ();
    }
}
