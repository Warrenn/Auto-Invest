using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class ContractExtended : Contract
    {
        public string? ConId { get; }
        public string? AccountId { get; }

        public ContractExtended(
            string symbol,
            decimal funding,
            decimal trailingOffset,
            decimal tradePercentage,
            decimal initialQuantity,
            decimal averagePrice ,
            uint safetyBands,
            decimal marginProtection,
            string? conId ,
            string? accountId) : base(
             symbol,
             funding,
             trailingOffset,
             tradePercentage,
             initialQuantity,
             averagePrice,
             safetyBands,
             marginProtection)
        {
            ConId = conId;
            AccountId = accountId;
        }

    }
}
