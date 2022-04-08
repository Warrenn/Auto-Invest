namespace Auto_Invest_Strategy
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
        void SetTrailingBuyOrderId(int newValue);
        void SetTrailingSellOrderId(int newValue);
        void ResetEmergencyOrders();
        void AddEmergencyOrder(EmergencyOrderDetail detail);
        void RemoveEmergencyOrderId(int orderId);
    }
}