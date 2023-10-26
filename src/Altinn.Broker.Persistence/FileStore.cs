using Altinn.Broker.Core.Models;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Repositories.Interfaces;

namespace Altinn.Broker.Persistence;

public class FileStore : IFileStorage
{
    public FileStore()
    {
    }

    private static bool InDocker => Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")?.ToLower() == "true";

    public async Task<BrokerFileStatusOverview> SaveFile(Guid shipmentId, Stream filestream, BrokerFileInitalize brokerFile)
    {
        BrokerFileStatusOverview brokerFileStatusOverview = new BrokerFileStatusOverview();
        brokerFileStatusOverview.FileId = Guid.NewGuid();
        brokerFileStatusOverview.Checksum = brokerFile.Checksum;
        brokerFileStatusOverview.FileName = brokerFile.FileName;
        brokerFileStatusOverview.SendersFileReference = brokerFile.SendersFileReference;
        brokerFileStatusOverview.FileStatus = Core.Enums.BrokerFileStatus.AwaitingUploadProcessing;

        string basefolder = InDocker ? @"/mnt/storage/" : @"c:\Altinn\storage\";
        if (!Directory.Exists(basefolder))
        {
            Directory.CreateDirectory(basefolder);
        }
        string shipmentFolder = basefolder + (InDocker ? $"/{shipmentId}" : $@"\{shipmentId}");
        if (!Directory.Exists(shipmentFolder))
        {
            Directory.CreateDirectory(shipmentFolder);
        }

        string filePath = InDocker ? $@"{shipmentFolder}/{brokerFileStatusOverview.FileId}" : $@"{shipmentFolder}\{brokerFileStatusOverview.FileId}";
        using (var storestream = File.Create(filePath))
        {
            await filestream.CopyToAsync(storestream);
        }

        brokerFileStatusOverview.FileStatusText = "File uploaded and awaiting processing.";
        brokerFileStatusOverview.FileStatusChanged = DateTime.Now;

        return brokerFileStatusOverview;
    }

    public async Task UploadFile(Stream filestream, string shipmentId, string fileReference)
    {
        string basefolder = InDocker ? @"/mnt/storage/" : @"c:\Altinn\storage\";
        if (!Directory.Exists(basefolder))
        {
            Directory.CreateDirectory(basefolder);
        }
        string shipmentFolder = basefolder + (InDocker ? $"/{shipmentId}" : $@"\{shipmentId}");
        if (!Directory.Exists(shipmentFolder))
        {
            Directory.CreateDirectory(shipmentFolder);
        }

        string filePath = InDocker ? $@"{shipmentFolder}/{fileReference}" : $@"{shipmentFolder}\{fileReference}";
        using (var fileStream = File.Create(filePath))
        {
            await filestream.CopyToAsync(fileStream);
        }
    }
}