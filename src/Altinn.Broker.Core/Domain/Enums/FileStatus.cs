namespace Altinn.Broker.Core.Domain.Enums;

public enum FileStatus {
    None = 0,
    Initialized = 1,
    Processing = 2,
    Ready = 3,
    Failed = 4,
    Deleted = 5
}
