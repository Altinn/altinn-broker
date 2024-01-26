using Altinn.Broker.Application.DeleteFileCommand;
using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Application.GetFileDetailsQuery;
using Altinn.Broker.Application.GetFileOverviewQuery;
using Altinn.Broker.Application.GetFilesQuery;
using Altinn.Broker.Application.InitializeFileCommand;
using Altinn.Broker.Application.UploadFileCommand;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Application;
public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeFileCommandHandler>();
        services.AddScoped<UploadFileCommandHandler>();
        services.AddScoped<GetFileOverviewQueryHandler>();
        services.AddScoped<GetFileDetailsQueryHandler>();
        services.AddScoped<DownloadFileQueryHandler>();
        services.AddScoped<ConfirmDownloadCommandHandler>();
        services.AddScoped<GetFilesQueryHandler>();
        services.AddScoped<DeleteFileCommandHandler>();
        services.AddScoped<LegacyGetFilesQueryHandler>();
    }
}
