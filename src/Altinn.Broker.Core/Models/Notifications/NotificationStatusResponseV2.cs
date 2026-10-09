using Altinn.Broker.Core.Models.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Broker.Core.Models.Notifications;

public class CaseInsensitiveEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            throw new JsonException($"Unable to convert null or empty string to enum {typeof(T)}");

        return Enum.Parse<T>(value, ignoreCase: true);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class NotificationStatusResponseV2
{
    public Guid ShipmentId { get; set; }
    public string SendersReference { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset LastUpdate { get; set; }
    public List<RecipientStatus> Recipients { get; set; } = [];
}

public class RecipientStatus
{
    [JsonConverter(typeof(CaseInsensitiveEnumConverter<NotificationType>))]
    public NotificationType Type { get; set; }
    public string Destination { get; set; } = null!;
    public NotificationStatusV2 Status { get; set; }
    public DateTimeOffset LastUpdate { get; set; }

    public bool IsSent()
    {
        return Type switch
        {
            NotificationType.SMS => Status == NotificationStatusV2.SMS_Accepted || Status == NotificationStatusV2.SMS_Delivered,
            NotificationType.Email => Status == NotificationStatusV2.Email_Succeeded || Status == NotificationStatusV2.Email_Delivered,
            _ => false
        };
    }
} 
