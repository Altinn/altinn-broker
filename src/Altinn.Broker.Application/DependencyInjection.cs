using Altinn.Broker.Application.DownloadFileQuery;
using Altinn.Broker.Application.ExpireFileTransferCommand;
using Altinn.Broker.Application.GetFileTransferDetailsQuery;
using Altinn.Broker.Application.GetFileTransferOverviewQuery;
using Altinn.Broker.Application.GetFileTransfersQuery;
using Altinn.Broker.Application.InitializeFileTransferCommand;
using Altinn.Broker.Application.UploadFileCommand;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Application;
public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeFileTransferCommandHandler>();
        services.AddScoped<UploadFileCommandHandler>();
        services.AddScoped<GetFileTransferOverviewQueryHandler>();
        services.AddScoped<GetFileTransferDetailsQueryHandler>();
        services.AddScoped<DownloadFileQueryHandler>();
        services.AddScoped<ConfirmDownloadCommandHandler>();
        services.AddScoped<GetFileTransfersQueryHandler>();
        services.AddScoped<ExpireFileTransferCommandHandler>();
        services.AddScoped<LegacyGetFilesQueryHandler>();
        services.AddScoped<MalwareScanningResultHandler>();
    }
}
