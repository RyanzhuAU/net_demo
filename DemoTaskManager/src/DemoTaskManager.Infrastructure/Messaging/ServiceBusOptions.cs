using System.ComponentModel.DataAnnotations;

namespace DemoTaskManager.Infrastructure.Messaging;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    /// <summary>
    /// Preferred in Azure: <c>contoso.servicebus.windows.net</c>, supplied through the
    /// app setting <c>ServiceBusConnection__fullyQualifiedNamespace</c> and authenticated
    /// with the App Service managed identity.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>Local development fallback only. Never committed, never logged.</summary>
    public string? ConnectionString { get; set; }

    [Required]
    public string QueueName { get; set; } = "task-processing";

    /// <summary>When true the publisher becomes a no-op, which keeps the API runnable offline.</summary>
    public bool Enabled { get; set; } = true;

    public bool UseManagedIdentity => string.IsNullOrWhiteSpace(ConnectionString)
                                      && !string.IsNullOrWhiteSpace(FullyQualifiedNamespace);
}
