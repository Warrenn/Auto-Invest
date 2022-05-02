using Amazon.DynamoDBv2.DataModel;
using Auto_Invest_Strategy;

namespace Auto_Invest.DynamoDb
{
    [DynamoDBTable("auto-invest-contract")]
    public class ContractData
    {
        [DynamoDBHashKey]
        public string Symbol { get; set; } = "";

        [DynamoDBProperty]
        public RunState RunState { get; set; }

        [DynamoDBProperty]
        public decimal AveragePrice { get; set; }

        [DynamoDBProperty]
        public decimal TotalCost { get; set; }

        [DynamoDBProperty]
        public decimal QuantityOnHand { get; set; }

        [DynamoDBProperty]
        public decimal Funding { get; set; }

        [DynamoDBProperty]
        public uint SafetyBands { get; set; }

        [DynamoDBProperty]
        public decimal UpperBound { get; set; }

        [DynamoDBProperty]
        public decimal LowerBound { get; set; }

        [DynamoDBProperty]
        public decimal TrailingOffset { get; set; }

        [DynamoDBProperty]
        public decimal SellOrderLimit { get; set; }

        [DynamoDBProperty]
        public decimal BuyOrderLimit { get; set; }

        [DynamoDBProperty]
        public decimal TradePercent { get; set; }

        [DynamoDBProperty]
        public int TrailingBuyOrderId { get; set; }

        [DynamoDBProperty]
        public int TrailingSellOrderId { get; set; }

        [DynamoDBProperty]
        public decimal MarginProtection { get; set; }

        [DynamoDBProperty]
        public string? ConId { get; set; }

        [DynamoDBProperty]
        public string? AccountId { get; set; }

        [DynamoDBProperty(typeof(EmergencyOrderConverter))]
        public EmergencyOrderDetail[] EmergencyOrders { get; set; } = Array.Empty<EmergencyOrderDetail>();

    }
}
