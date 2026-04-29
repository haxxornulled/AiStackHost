using AiStackManager.Api.Hosted;
using AiStackManager.Application.Abstractions;
using AiStackManager.Application.Health;
using AiStackManager.Application.Models;
using AiStackManager.Application.Review;
using AiStackManager.Application.Stack;
using AiStackManager.Domain.Configuration;
using AiStackManager.Infrastructure.Commands;
using AiStackManager.Infrastructure.Configuration;
using AiStackManager.Infrastructure.Hermes;
using AiStackManager.Infrastructure.Inference;
using AiStackManager.Infrastructure.Ollama;
using AiStackManager.Infrastructure.OpenClaw;
using Autofac;

namespace AiStackManager.Api.Composition;

public static class DependencyInjection
{
    public static void RegisterAiStack(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<AiStackSettings>(builder.Configuration.GetSection("AiStack"));
        builder.Services.AddHostedService<AiStackLifecycleService>();
        builder.Services.AddHostedService<AiStackHealthBackgroundService>();
        builder.Host.ConfigureContainer<ContainerBuilder>(container => container.RegisterModule<AiStackAutofacModule>());
    }
}

public sealed class AiStackAutofacModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<InMemoryAiStackStateStore>()
            .As<IAiStackStateStore>()
            .SingleInstance();

        builder.RegisterType<InMemoryInferenceModelSelectionStore>()
            .As<IInferenceModelSelectionStore>()
            .SingleInstance();

        builder.RegisterType<ProcessCommandRunner>()
            .As<ICommandRunner>()
            .SingleInstance();

        builder.RegisterType<OllamaService>()
            .AsSelf()
            .As<IInferenceProvider>()
            .As<IInferenceProvider<OllamaProviderOptions>>()
            .SingleInstance();

        builder.RegisterType<InferenceProviderRegistry>()
            .As<IInferenceProviderRegistry>()
            .SingleInstance();

        builder.RegisterType<HermesService>()
            .As<IHermesService>()
            .SingleInstance();

        builder.RegisterType<OpenClawService>()
            .As<IOpenClawService>()
            .SingleInstance();

        builder.RegisterType<AiStackOrchestrator>()
            .SingleInstance();

        builder.RegisterType<InferenceModelManager>()
            .SingleInstance();

        builder.RegisterType<ReviewWorkflow>()
            .SingleInstance();

        builder.RegisterType<AiStackHealthMonitor>()
            .SingleInstance();
    }
}
