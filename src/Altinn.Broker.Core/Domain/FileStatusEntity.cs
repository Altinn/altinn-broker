using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altinn.Broker.Core.Domain;
public class FileStatusEntity
{
    public Guid FileId { get; set; }
    public Enums.FileStatus Status { get; set; }
    public DateTimeOffset Date { get; set; }
}