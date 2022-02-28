using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Auto_Invest.Strategy;

namespace Auto_Invest_Test
{
    public class ContractService : IContractService, IBuySaleLogic
    {
        private readonly IDictionary<string, ContractState> _contractStates;

        public ContractService(IDictionary<string, ContractState> contractStates)
        {
            _contractStates = contractStates;
        }

        #region Implementation of IStrategy

        public Func<ContractState, decimal> BuyQtyStrategy { get; set; } =
            (contract) => contract.Funding * 0.2M;

        public Func<ContractState, decimal> SellQtyStrategy { get; set; } =
            (contract) => contract.Quantity * 0.2M;

        public async Task<ContractState> GetContractState(string conId) =>
            await Task.FromResult(_contractStates[conId]);

        public async Task CreateTrigger(TriggerDetails details)
        {
            await Task.Run(() =>
            {
                var contract = _contractStates[details.ConId];
                contract.UpperBound = details.UpperLimit;
                contract.LowerBound = details.LowerLimit;
                contract.RunState = RunState.TriggerRun;
            });
        }

        public async Task<decimal> GetContractsAverageValue(string conId)
        {
            var contract = _contractStates[conId];
            return await Task.FromResult(contract.AveragePrice);
        }

        public async Task PlaceBuyStopOrder(StopOrder order)
        {
            await Task.Run(() =>
            {
                var contract = _contractStates[order.ConId];
                contract.RunState = RunState.BuyRun;
                contract.BuyLimit = order.Price;
                contract.BuyQty = BuyQtyStrategy(contract);
            });
        }

        public async Task PlaceSellStopOrder(StopOrder order)
        {
            await Task.Run(() =>
            {
                var contract = _contractStates[order.ConId];
                contract.RunState = RunState.SellRun;
                contract.SellLimit = order.Price;
                contract.SellQty = SellQtyStrategy(contract);
            });
        }

        #endregion

        #region Implementation of IBuySaleLogic

        public async Task BuyActionComplete(ActionDetails details) => await Task.Run(() =>
        {
            var contract = _contractStates[details.ConId];
            if (details.Qty == 0) return;

            var cost = (contract.Quantity * contract.AveragePrice) + details.CostOfOrder;
            var newQty = contract.Quantity + details.Qty;

            contract.Quantity = newQty;
            contract.AveragePrice = details.CostOfOrder / details.Qty;

            if (newQty != 0 && cost != 0)
            {
                contract.AveragePrice = cost / newQty;
            }

            contract.Funding -= details.CostOfOrder;
        });

        public async Task SellActionComplete(ActionDetails details) => await Task.Run(() =>
        {
            var contract = _contractStates[details.ConId];
            if (details.Qty == 0) return;

            var cost = (contract.Quantity * contract.AveragePrice) - (contract.AveragePrice * details.Qty);
            var newQty = contract.Quantity - details.Qty;

            contract.AveragePrice = details.CostOfOrder / details.Qty;
            contract.Quantity = newQty;

            if (newQty != 0 && cost != 0)
            {
                contract.AveragePrice = cost / newQty;
            }


            contract.Funding += details.CostOfOrder;
        });

        #endregion
    }
}