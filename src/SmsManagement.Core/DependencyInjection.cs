using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmsManagement.Core.Services;
using SmsManagement.Core.Validation;

namespace SmsManagement.Core;

public static class DependencyInjection
{
    /// <summary>Registers the Core application services (validation and message use cases).</summary>
    public static IServiceCollection AddSmsManagementCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ISendMessageRequestValidator, SendMessageRequestValidator>();
        services.TryAddScoped<IMessageService, MessageService>();

        return services;
    }
}
