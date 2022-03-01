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

        public Func<ContractState, decimal, decimal> BuyQtyStrategy { get; set; } =
            (contract, price) => (contract.Funding * 0.2M) / price;

        public Func<ContractState, decimal, decimal> SellQtyStrategy { get; set; } =
            (contract, price) => contract.Quantity * 0.5M;

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
                contract.BuyLimit = order.PricePerUnit;
                contract.BuyQty = BuyQtyStrategy(contract, order.PricePerUnit);
            });
        }

        public async Task PlaceSellStopOrder(StopOrder order)
        {
            await Task.Run(() =>
            {
                var contract = _contractStates[order.ConId];
                contract.RunState = RunState.SellRun;
                contract.SellLimit = order.PricePerUnit;
                contract.SellQty = SellQtyStrategy(contract, order.PricePerUnit);
            });
        }

        #endregion

        #region Implementation of IBuySaleLogic

        public async Task BuyActionComplete(ActionDetails details) => await Task.Run(() =>
        {
            var contract = _contractStates[details.ConId];
            if (details.Qty <= 0) return;

            var originalQty = contract.Quantity;
            contract.Quantity += details.Qty;
            contract.Funding -= details.CostOfOrder;
            contract.TotalCost += details.CostOfOrder;

            if (originalQty < 0 && contract.Quantity >= 0)
            {
                contract.TotalCost = contract.Quantity * details.PricePerUnit;
                contract.AveragePrice = details.PricePerUnit;
                return;
            }

            if (contract.Quantity < 0)
            {
                contract.TotalCost += contract.AveragePrice * details.Qty;
            }

            contract.AveragePrice = contract.TotalCost / contract.Quantity;
        });

        public async Task SellActionComplete(ActionDetails details) => await Task.Run(() =>
        {
            var contract = _contractStates[details.ConId];
            if (details.Qty <= 0) return;

            var originalQty = contract.Quantity;
            contract.Funding += details.CostOfOrder;
            contract.Quantity -= details.Qty;
            contract.TotalCost -= contract.AveragePrice * details.Qty;

            if (contract.Quantity > 0) return;

            if (originalQty > 0 || contract.Quantity == 0)
            {
                contract.TotalCost = contract.Quantity * details.PricePerUnit;
                contract.AveragePrice = details.PricePerUnit;
                return;
            }

            contract.TotalCost -= details.CostOfOrder;
            contract.AveragePrice = contract.TotalCost / contract.Quantity;
        });

        #endregion
    }
}