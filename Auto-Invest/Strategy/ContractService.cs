using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Auto_Invest.Strategy;

namespace Auto_Invest_Test
{
    public class ContractService : IContractService, IBuySaleLogic
    {
        private readonly IDictionary<string, ContractState> _contractStates;
        private int _orderId;

        public ContractService(IDictionary<string, ContractState> contractStates)
        {
            _orderId = 0;
            _contractStates = contractStates;
        }

        #region Implementation of IStrategy

        public Func<ContractState, decimal, decimal> BuyQtyStrategy { get; set; } =
            (contract, price) =>
            {
                if (price <= 0) return 0;
                if (contract.AveragePrice > 0 &&
                    price > contract.AveragePrice) return 0;

                var funding = contract.Funding * contract.FundingRisk;
                var baseLine = contract.BuyBaseLine == 0 ? 1 : contract.BuyBaseLine;

                if (contract.AveragePrice == 0) return funding / price;
                var ratio = (contract.AveragePrice - price) / (contract.AveragePrice * baseLine);

                funding *= ratio;
                var qty = funding / price;

                return qty;
            };

        public Func<ContractState, decimal, decimal> SellQtyStrategy { get; set; } =
            (contract, price) =>
            {
                if (price <= 0) return 0;
                if (contract.AveragePrice > 0 &&
                    price < contract.AveragePrice) return 0;
                if (contract.AveragePrice == 0) return 0;

                var sellMargin = (price - contract.AveragePrice) / contract.AveragePrice;
                var shortQty = contract.ShortFund / price;

                if (contract.Quantity <= 0) return shortQty * sellMargin;
                if (sellMargin < 1) return contract.Quantity * sellMargin;

                var qty = contract.Quantity + (sellMargin - 1) * shortQty;
                return qty;
            };
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
                if (order.PricePerUnit > contract.AveragePrice) return;

                contract.RunState = RunState.BuyRun;
                contract.BuyLimit = order.PricePerUnit;
                contract.BuyQty = BuyQtyStrategy(contract, order.PricePerUnit);

                if (contract.BuyOrderIds.Any()) return;
                _orderId++;
                contract.BuyOrderIds.Add(_orderId);
            });
        }

        public async Task PlaceSellStopOrder(StopOrder order)
        {
            await Task.Run(() =>
            {
                var contract = _contractStates[order.ConId];
                if (order.PricePerUnit < contract.AveragePrice) return;

                contract.RunState = RunState.SellRun;
                contract.SellLimit = order.PricePerUnit;
                contract.SellQty = SellQtyStrategy(contract, order.PricePerUnit);

                if (contract.SelOrderIds.Any()) return;
                _orderId++;
                contract.SelOrderIds.Add(_orderId);
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
            contract.BuyOrderIds = new List<int>();
            contract.BuyQty = 0;
            contract.BuyLimit = 0;

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
            contract.SelOrderIds = new List<int>();
            contract.SellQty = 0;
            contract.SellLimit = 0;

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