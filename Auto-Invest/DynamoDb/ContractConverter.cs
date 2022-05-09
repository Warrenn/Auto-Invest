using Auto_Invest_Strategy;

namespace Auto_Invest.DynamoDb
{
    public class ContractConverter : IRegisterContractEditor
    {
        private readonly ContractData _dynamoData;

        public ContractConverter(ContractData dynamoData)
        {
            _dynamoData = dynamoData;
            Contract = new ContractExtended(
                _dynamoData.Symbol,
                _dynamoData.Funding,
                _dynamoData.TrailingOffset,
                _dynamoData.TradePercent,
                _dynamoData.QuantityOnHand,
                _dynamoData.AveragePrice,
                _dynamoData.SafetyBands,
                _dynamoData.MarginProtection,
                _dynamoData.ConId,
                _dynamoData.AccountId);

            Contract.RegisterEditor(this);
        }
        #region Implementation of IRegisterContractEditor

        public ContractExtended Contract { get; }

        public void RegisterEditor(Contract state, IContractEditor contractEditor)
        {
            contractEditor.SetUpperBound(_dynamoData.UpperBound);
            contractEditor.SetLowerBound(_dynamoData.LowerBound);
            contractEditor.SetBuyLimit(_dynamoData.BuyOrderLimit);
            contractEditor.SetSellLimit(_dynamoData.SellOrderLimit);
            contractEditor.SetRunState(_dynamoData.RunState);
            contractEditor.SetTrailingBuyOrderId(_dynamoData.TrailingBuyOrderId);
            contractEditor.SetTrailingSellOrderId(_dynamoData.TrailingSellOrderId);
            contractEditor.SetTotalCost(_dynamoData.TotalCost);
        }

        #endregion
    }
}
