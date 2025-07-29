using Altinn.Broker.Application.ConfigureResource;
using Altinn.Broker.Application.DownloadFile;
using Altinn.Broker.Application.PurgeFileTransfer;
using Altinn.Broker.Application.GetFileTransferDetails;
using Altinn.Broker.Application.GetFileTransferOverview;
using Altinn.Broker.Application.GetFileTransfers;
using Altinn.Broker.Application.GetResource;
using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Application.Middlewares;
using Altinn.Broker.Application.UploadFile;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Broker.Application;
public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeFileTransferHandler>();
        services.AddScoped<UploadFileHandler>();
        services.AddScoped<GetFileTransferOverviewHandler>();
        services.AddScoped<GetFileTransferDetailsHandler>();
        services.AddScoped<DownloadFileHandler>();
        services.AddScoped<ConfirmDownloadHandler>();
        services.AddScoped<GetFileTransfersHandler>();
        services.AddScoped<PurgeFileTransferHandler>();
        services.AddScoped<LegacyGetFilesHandler>();
        services.AddScoped<MalwareScanningResultHandler>();
        services.AddScoped<ConfigureResourceHandler>();
        services.AddScoped<EventBusMiddleware>();
        services.AddScoped<GetResourceHandler>();
        services.AddScoped<StuckFileTransferHandler>();
        services.AddScoped<SlackStuckFileTransferNotifier>();
    }
}
