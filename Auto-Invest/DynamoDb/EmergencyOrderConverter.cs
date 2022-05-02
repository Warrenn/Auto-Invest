using System.Text.Json;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Auto_Invest_Strategy;

namespace Auto_Invest.DynamoDb;

public class EmergencyOrderConverter : IPropertyConverter
{
    #region Implementation of IPropertyConverter

    public DynamoDBEntry ToEntry(object value)
    {
        if (value is not EmergencyOrderDetail[] emergencyOrders) throw new ArgumentOutOfRangeException(nameof(value));
        var data = JsonSerializer.Serialize(emergencyOrders);
        var entry = new Primitive { Value = data };
        return entry;
    }

    public object FromEntry(DynamoDBEntry entry)
    {
        if (entry is not Primitive primitive) return Array.Empty<EmergencyOrderDetail>();
        if (primitive.Value is not string dataValue) return Array.Empty<EmergencyOrderDetail>();
        if (string.IsNullOrWhiteSpace(dataValue)) return Array.Empty<EmergencyOrderDetail>();

        var data = JsonSerializer.Deserialize<EmergencyOrderDetail[]>(dataValue);
        return data ?? Array.Empty<EmergencyOrderDetail>();
    }

    #endregion
}