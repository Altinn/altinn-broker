using System.Diagnostics.CodeAnalysis;

using Altinn.Broker.Core.Enums;
using Altinn.Broker.Core.Models;

namespace Altinn.Broker.Persistence
{
    public interface IFileStore
    {
        Task UploadFile(Stream filestream, string shipmentId, string fileReference);
    }
}
