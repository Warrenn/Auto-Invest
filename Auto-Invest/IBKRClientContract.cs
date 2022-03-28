using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Auto_Invest_Strategy;

namespace Auto_Invest
{
    public class IBKRClientContract : IContractClient
    {
        #region Implementation of IContractClient

        public async Task CancelOrder(int orderId)
        {
            throw new NotImplementedException();
        }


        public async Task<ContractResult> PlaceStopLimit(StopLimit stopLimit)
        {
            throw new NotImplementedException();
        }

        public void ListenForCompletion(string symbol, IOrderCompletion orderCompletion)
        {
            throw new NotImplementedException();
        }

        public async Task<decimal> GetMarketPrice(string symbol)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
