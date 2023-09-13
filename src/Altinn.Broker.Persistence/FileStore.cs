namespace Altinn.Broker.Persistence;

public class FileStore : IFileStore
{
    public FileStore()
    {
    }


    private static bool InDocker => Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")?.ToLower() == "true";
    public async Task<string> UploadFile(Stream filestream, string shipmentId, string fileReference)
    {   
        string basefolder = InDocker ? @"/mnt/storage/" : @"c:\Altinn\storage\";
        if(!Directory.Exists(basefolder))
        {
            Directory.CreateDirectory(basefolder);
        }
        string shipmentFolder = basefolder + (InDocker ? $"/{shipmentId}" : $@"\{shipmentId}");
        if(!Directory.Exists(shipmentFolder))
        {
            Directory.CreateDirectory(shipmentFolder);
        }

        string filePath = InDocker ? $@"{shipmentFolder}/{fileReference}" : $@"{shipmentFolder}\{fileReference}";
        using (var fileStream = File.Create(filePath))
        {
            await filestream.CopyToAsync(fileStream);
        }
        return filePath;
    }
}

