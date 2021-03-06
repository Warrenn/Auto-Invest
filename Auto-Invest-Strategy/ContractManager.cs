using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Auto_Invest_Strategy
{
    public class ContractManager :
        IContractManager,
        IBuySellLogic,
        IRegisterContractEditor,
        IOrderCompletion
    {
        private readonly IContractClient _contractClient;
        private readonly IDictionary<string, Contract> _contracts = new Dictionary<string, Contract>();
        private readonly IDictionary<string, IContractEditor> _contractEditors = new Dictionary<string, IContractEditor>();
        private IOrderFilledProcess _orderFilledProcess;

        public event Func<TriggerDetails, Task> CreateTriggerEvent = _ => Task.CompletedTask;
        public event Func<MarketOrder, Task> PlaceTrailingBuyOrderEvent = _ => Task.CompletedTask;
        public event Func<MarketOrder, Task> PlaceTrailingSellOrderEvent = _ => Task.CompletedTask;
        public event Func<MarketOrder, Task> PlaceEmergencySellOrderEvent = _ => Task.CompletedTask;
        public event Func<MarketOrder, Task> PlaceEmergencyBuyOrderEvent = _ => Task.CompletedTask;
        public event Func<ActionDetails, Task> TrailingBuyCompleteEvent = _ => Task.CompletedTask;
        public event Func<ActionDetails, Task> TrailingSellCompleteEvent = _ => Task.CompletedTask;
        public event Func<EmergencyActionDetails, Task> EmergencyActionCompleteEvent = _ => Task.CompletedTask;

        public ContractManager(IContractClient contractClient)
        {
            _contractClient = contractClient;
            _orderFilledProcess = null;
        }

        public void RegisterContract(Contract contract)
        {
            _contracts[contract.Symbol] = contract;
            contract.RegisterEditor(this);
            _contractClient.ListenForCompletion(contract.Symbol, this);
        }


        #region Implementation of IStrategy

        public async Task<Contract> GetContractState(string symbol) =>
            await Task.FromResult(_contracts[symbol]);

        public async Task CreateTrigger(TriggerDetails details)
        {
            var editor = _contractEditors[details.Symbol];
            editor.SetUpperBound(details.UpperLimit);
            editor.SetLowerBound(details.LowerLimit);
            editor.SetRunState(RunState.TriggerRun);
            await CreateTriggerEvent(details);
        }

        public async Task<decimal> GetContractsAverageValue(string symbol)
        {
            var contract = _contracts[symbol];
            return await Task.FromResult(contract.AveragePrice);
        }

        public async Task PlaceTrailingBuyOrder(MarketOrder order)
        {
            var contract = _contracts[order.Symbol];
            var editor = _contractEditors[order.Symbol];

            editor.SetUpperBound(-1);
            editor.SetLowerBound(-1);
            editor.SetSellLimit(-1);
            editor.SetBuyLimit(order.PricePerUnit);
            editor.SetRunState(RunState.BuyRun);

            if (contract.TrailingSellOrderId > 0)
            {
                await _contractClient.CancelOrder(contract.TrailingSellOrderId);
                editor.SetTrailingSellOrderId(-1);
            }

            var orderResult = await _contractClient.PlaceStopLimit(new StopLimit
            {
                Quantity = order.Quantity,
                OrderId = contract.TrailingBuyOrderId,
                Side = ActionSide.Buy,
                StopPrice = order.PricePerUnit,
                Symbol = order.Symbol
            });

            editor.SetTrailingBuyOrderId(orderResult.OrderId);
            await PlaceTrailingBuyOrderEvent(order);
        }


        public async Task PlaceTrailingSellOrder(MarketOrder order)
        {
            var contract = _contracts[order.Symbol];
            var editor = _contractEditors[order.Symbol];

            editor.SetUpperBound(-1);
            editor.SetLowerBound(-1);
            editor.SetBuyLimit(-1);
            editor.SetSellLimit(order.PricePerUnit);
            editor.SetRunState(RunState.SellRun);

            if (contract.TrailingBuyOrderId > 0)
            {
                await _contractClient.CancelOrder(contract.TrailingBuyOrderId);
                editor.SetTrailingBuyOrderId(-1);
            }

            var orderResult = await _contractClient.PlaceStopLimit(new StopLimit
            {
                Quantity = order.Quantity,
                OrderId = contract.TrailingSellOrderId,
                Side = ActionSide.Sell,
                StopPrice = order.PricePerUnit,
                Symbol = order.Symbol
            });

            editor.SetTrailingSellOrderId(orderResult.OrderId);
            await PlaceTrailingSellOrderEvent(order);
        }

        public async Task PlaceEmergencySellOrder(MarketOrder order)
        {
            var contract = _contracts[order.Symbol];
            var editor = _contractEditors[order.Symbol];

            if (contract.EmergencyOrders.Any(
                _ => _.Action == ActionSide.Sell &&
                     _.PricePerUnit == order.PricePerUnit)) return;

            var orderResult = await _contractClient.PlaceStopLimit(new StopLimit
            {
                Symbol = order.Symbol,
                Quantity = order.Quantity,
                StopPrice = order.PricePerUnit,
                Side = ActionSide.Sell
            });

            editor.AddEmergencyOrder(new EmergencyOrderDetail
            {
                Action = ActionSide.Sell,
                OrderId = orderResult.OrderId,
                PricePerUnit = order.PricePerUnit,
                Size = order.Quantity
            });
            await PlaceEmergencySellOrderEvent(order);
        }

        public async Task PlaceEmergencyBuyOrder(MarketOrder order)
        {
            var contract = _contracts[order.Symbol];
            var editor = _contractEditors[order.Symbol];

            if (contract.EmergencyOrders.Any(
                _ => _.Action == ActionSide.Buy &&
                     _.PricePerUnit == order.PricePerUnit)) return;

            var orderResult = await _contractClient.PlaceStopLimit(new StopLimit
            {
                Symbol = order.Symbol,
                Quantity = order.Quantity,
                StopPrice = order.PricePerUnit,
                Side = ActionSide.Buy
            });

            editor.AddEmergencyOrder(new EmergencyOrderDetail
            {
                Action = ActionSide.Buy,
                OrderId = orderResult.OrderId,
                PricePerUnit = order.PricePerUnit,
                Size = order.Quantity
            });
            await PlaceEmergencyBuyOrderEvent(order);
        }

        public void InitializeContract(TickPosition tick)
        {
            var editor = _contractEditors[tick.Symbol];
            editor.SetAveragePrice(tick.Position);
        }

        #endregion

        public async Task ClearEmergencyOrders(string symbol)
        {
            var contract = _contracts[symbol];
            var editor = _contractEditors[symbol];

            foreach (var detail in contract.EmergencyOrders)
            {
                await _contractClient.CancelOrder(detail.OrderId);
            }

            editor.ResetEmergencyOrders();
        }

        public void RegisterForOrderFilled(IOrderFilledProcess process) => _orderFilledProcess = process;

        #region Implementation of IBuySaleLogic

        public async Task TrailingBuyComplete(ActionDetails details)
        {
            var contract = _contracts[details.Symbol];
            var editor = _contractEditors[details.Symbol];
            if (details.Qty <= 0) return;
            editor.SetTrailingBuyOrderId(-1);
            editor.SetBuyLimit(-1);

            BuyComplete(details, contract, editor);
            await TrailingBuyCompleteEvent(details);
        }

        public void BuyComplete(ActionDetails details, Contract contract, IContractEditor editor)
        {
            var originalQty = contract.QuantityOnHand;
            var originalCost = contract.TotalCost;
            var newQuantity = contract.QuantityOnHand + details.Qty;
            var newTotalCost = originalCost + details.CostOfOrder;

            editor.SetQuantity(newQuantity);
            editor.SetFunding(contract.Funding - details.CostOfOrder - details.Commission);
            editor.SetTotalCost(newTotalCost);

            if (originalQty < 0 && newQuantity >= 0)
            {
                editor.SetTotalCost(newQuantity * details.PricePerUnit);
                editor.SetAveragePrice(details.PricePerUnit);
                return;
            }

            if (newQuantity < 0)
            {
                var average = originalQty == 0 ? details.PricePerUnit : contract.AveragePrice;
                newTotalCost = originalCost + average * details.Qty;
                editor.SetTotalCost(newTotalCost);
            }

            editor.SetAveragePrice(Math.Abs(newTotalCost / newQuantity));
        }

        public async Task TrailingSellComplete(ActionDetails details)
        {
            var contract = _contracts[details.Symbol];
            var editor = _contractEditors[details.Symbol];
            if (details.Qty <= 0) return;
            editor.SetTrailingSellOrderId(-1);
            editor.SetSellLimit(-1);

            SellComplete(details, contract, editor);
            await TrailingSellCompleteEvent(details);
        }

        public void SellComplete(ActionDetails details, Contract contract, IContractEditor editor)
        {
            var originalQty = contract.QuantityOnHand;
            var originalCost = contract.TotalCost;
            var newQuantity = originalQty - details.Qty;
            var newTotalCost = originalCost - contract.AveragePrice * details.Qty;

            editor.SetFunding(contract.Funding + details.CostOfOrder - details.Commission);
            editor.SetQuantity(newQuantity);
            editor.SetTotalCost(newTotalCost);

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
        }

        public async Task EmergencyActionComplete(EmergencyActionDetails details)
        {
            var editor = _contractEditors[details.Symbol];
            var contract = _contracts[details.Symbol];

            if (details.Side == ActionSide.Buy)
            {
                BuyComplete(details, contract, editor);
            }
            else
            {
                SellComplete(details, contract, editor);
            }

            editor.RemoveEmergencyOrderId(details.OrderId);
            await EmergencyActionCompleteEvent(details);
        }

        #endregion

        #region Implementation of IRegisterContractEditor

        public void RegisterEditor(Contract state, IContractEditor contractEditor) =>
            _contractEditors[state.Symbol] = contractEditor;

        #endregion

        #region Implementation of IOrderCompletion

        public async Task OrderCompleted(CompletedOrder order)
        {
            var contract = _contracts[order.Symbol];
            if (contract.EmergencyOrders.Any(_ => _.OrderId == order.OrderId))
            {
                await EmergencyActionComplete(new EmergencyActionDetails
                {
                    Commission = order.Commission,
                    CostOfOrder = order.CostOfOrder,
                    OrderId = order.OrderId,
                    PricePerUnit = order.PricePerUnit,
                    Qty = order.Qty,
                    Symbol = order.Symbol,
                    Side = order.Side
                });
                return;
            }

            var actionDetails = new ActionDetails
            {
                Commission = order.Commission,
                CostOfOrder = order.CostOfOrder,
                OrderId = order.OrderId,
                PricePerUnit = order.PricePerUnit,
                Qty = order.Qty,
                Symbol = order.Symbol
            };

            if (order.Side == ActionSide.Buy)
            {
                await TrailingBuyComplete(actionDetails);
            }
            else
            {
                await TrailingSellComplete(actionDetails);
            }

            _orderFilledProcess?.OrderFilled(new MarketOrder
            {
                Quantity = order.Qty,
                PricePerUnit = order.PricePerUnit,
                Symbol = order.Symbol
            });
        }

        #endregion
    }
}