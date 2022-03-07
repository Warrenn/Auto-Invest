using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Auto_Invest.Strategy
{
    public class ContractManager :
        IContractManager,
        IBuySellLogic,
        IRegisterContractEditor
    {
        private readonly IDictionary<string, Contract> _contracts = new Dictionary<string, Contract>();
        private readonly IDictionary<string, IContractEditor> _contractEditors = new Dictionary<string, IContractEditor>();

        private int _orderId;

        public ContractManager(int orderIdSeed)
        {
            _orderId = orderIdSeed;
        }

        public void RegisterContract(Contract contract)
        {
            _contracts[contract.ConId] = contract;
            contract.RegisterEditor(this);
        }


        #region Implementation of IStrategy

        public async Task<Contract> GetContractState(string conId) =>
            await Task.FromResult(_contracts[conId]);

        public async Task CreateTrigger(TriggerDetails details)
        {
            await Task.Run(() =>
            {
                var editor = _contractEditors[details.ConId];
                editor.SetUpperBound(details.UpperLimit);
                editor.SetLowerBound(details.LowerLimit);
                editor.SetRunState(RunState.TriggerRun);
            });
        }

        public async Task<decimal> GetContractsAverageValue(string conId)
        {
            var contract = _contracts[conId];
            return await Task.FromResult(contract.AveragePrice);
        }

        public async Task PlaceBuyStopOrder(MarketOrder order)
        {
            await Task.Run(() =>
            {
                var contract = _contracts[order.ConId];
                var editor = _contractEditors[order.ConId];

                editor.SetUpperBound(-1);
                editor.SetLowerBound(-1);

                if (order.PricePerUnit > contract.AveragePrice) return;

                editor.SetRunState(RunState.BuyRun);
                editor.SetBuyLimit(order.PricePerUnit);
                editor.SetBuyQty(order.Quantity);

                if (contract.BuyOrderId > 0) return;
                _orderId++;
                editor.SetBuyOrderId(_orderId);
            });
        }

        public async Task PlaceSellStopOrder(MarketOrder order)
        {
            await Task.Run(() =>
            {
                var contract = _contracts[order.ConId];
                var editor = _contractEditors[order.ConId];

                editor.SetUpperBound(-1);
                editor.SetLowerBound(-1);

                if (order.PricePerUnit < contract.AveragePrice) return;

                editor.SetRunState(RunState.SellRun);
                editor.SetSellLimit(order.PricePerUnit);
                editor.SetSellQty(order.Quantity);

                if (contract.SellOrderId > 0) return;
                _orderId++;
                editor.SetSellOrderId(_orderId);
            });
        }

        public void InitializeContract(TickPosition tick)
        {
            var editor = _contractEditors[tick.ConId];
            editor.SetAveragePrice(tick.Position);
        }

        #endregion

        #region Implementation of IBuySaleLogic

        public async Task BuyActionComplete(ActionDetails details) => await Task.Run(() =>
        {
            var contract = _contracts[details.ConId];
            var editor = _contractEditors[details.ConId];
            if (details.Qty <= 0) return;

            var originalQty = contract.Quantity;
            var originalCost = contract.TotalCost;
            var newQuantity = contract.Quantity + details.Qty;
            var newTotalCost = originalCost + details.CostOfOrder;

            editor.SetQuantity(newQuantity);
            editor.SetFunding(contract.Funding - details.CostOfOrder);
            editor.SetTotalCost(newTotalCost);
            editor.SetBuyOrderId(-1);
            editor.SetBuyQty(0);
            editor.SetBuyLimit(0);

            if (originalQty < 0 && newQuantity >= 0)
            {
                editor.SetTotalCost(newQuantity * details.PricePerUnit);
                editor.SetAveragePrice(details.PricePerUnit);
                return;
            }

            if (newQuantity < 0)
            {
                newTotalCost = originalCost + contract.AveragePrice * details.Qty;
                editor.SetTotalCost(newTotalCost);
            }

            editor.SetAveragePrice(Math.Abs(newTotalCost / newQuantity));
        });

        public async Task SellActionComplete(ActionDetails details) => await Task.Run(() =>
        {
            var contract = _contracts[details.ConId];
            var editor = _contractEditors[details.ConId];
            if (details.Qty <= 0) return;

            var originalQty = contract.Quantity;
            var originalCost = contract.TotalCost;
            var newQuantity = originalQty - details.Qty;
            var newTotalCost = originalCost - contract.AveragePrice * details.Qty;

            editor.SetFunding(contract.Funding + details.CostOfOrder);
            editor.SetQuantity(newQuantity);
            editor.SetTotalCost(newTotalCost);
            editor.SetSellOrderId(-1);
            editor.SetSellQty(0);
            editor.SetSellLimit(0);

            if (newQuantity > 0) return;

            if (originalQty > 0 || newQuantity == 0)
            {
                editor.SetTotalCost(newQuantity * details.PricePerUnit);
                editor.SetAveragePrice(details.PricePerUnit);
                return;
            }

            newTotalCost = originalCost - details.CostOfOrder;
            editor.SetTotalCost(newTotalCost);
            editor.SetAveragePrice(Math.Abs(newTotalCost / newQuantity));
        });

        #endregion

        #region Implementation of IRegisterContractEditor

        public void RegisterEditor(Contract state, IContractEditor contractEditor) =>
            _contractEditors[state.ConId] = contractEditor;

        #endregion
    }
}