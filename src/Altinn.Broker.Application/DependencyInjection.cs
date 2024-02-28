using Altinn.Broker.Application.DeleteFileTransferCommand;
using Altinn.Broker.Application.DownloadFileTransferQuery;
using Altinn.Broker.Application.GetFileTransferDetailsQuery;
using Altinn.Broker.Application.GetFileTransferOverviewQuery;
using Altinn.Broker.Application.GetFileTransfersQuery;
using Altinn.Broker.Application.InitializeFileTransferCommand;
using Altinn.Broker.Application.UploadFileTransferCommand;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Application;
public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeFileTransferCommandHandler>();
        services.AddScoped<UploadFileTransferCommandHandler>();
        services.AddScoped<GetFileTransferOverviewQueryHandler>();
        services.AddScoped<GetFileTransferDetailsQueryHandler>();
        services.AddScoped<DownloadFileTransferQueryHandler>();
        services.AddScoped<ConfirmDownloadCommandHandler>();
        services.AddScoped<GetFileTransfersQueryHandler>();
        services.AddScoped<DeleteFileTransferCommandHandler>();
        services.AddScoped<LegacyGetFilesQueryHandler>();
        services.AddScoped<MalwareScanningResultHandler>();
    }
}
