namespace Auto_Invest_Strategy
{
    public class Simulator : IRegisterContractEditor
    {
        private readonly Contract _original;

        public Simulator(Contract original)
        {
            _original = original;
            Contract = new Contract(
                original.Symbol,
                original.Funding,
                original.TrailingOffset,
                original.TradePercent,
                original.QuantityOnHand,
                original.AveragePrice,
                original.SafetyBands,
                original.MarginProtection);

            Contract.RegisterEditor(this);
        }

        public IContractEditor Editor { get; private set; }
        public Contract Contract { get; }

        #region Implementation of IRegisterContractEditor

        public void RegisterEditor(Contract state, IContractEditor contractEditor)
        {
            Editor = contractEditor;
            Editor.SetUpperBound(_original.UpperBound);
            Editor.SetLowerBound(_original.LowerBound);
            Editor.SetBuyLimit(_original.BuyOrderLimit);
            Editor.SetSellLimit(_original.SellOrderLimit);
            Editor.SetRunState(_original.RunState);
            Editor.SetTrailingBuyOrderId(_original.TrailingBuyOrderId);
            Editor.SetTrailingSellOrderId(_original.TrailingSellOrderId);
            Editor.SetTotalCost(_original.TotalCost);
        }

        #endregion
    }
}
