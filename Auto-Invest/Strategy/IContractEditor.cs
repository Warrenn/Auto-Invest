namespace Auto_Invest.Strategy
{
    public interface IContractEditor
    {
        void SetRunState(RunState newState);
        void SetAveragePrice(decimal newValue);
        void SetTotalCost(decimal newValue);
        void SetQuantity(decimal newValue);
        void SetFunding(decimal newValue);
        void SetUpperBound(decimal newValue);
        void SetLowerBound(decimal newValue);
        void SetSellLimit(decimal newValue);
        void SetBuyLimit(decimal newValue);
        void SetBuyOrderId(int newValue);
        void SetSellOrderId(int newValue);
    }
}