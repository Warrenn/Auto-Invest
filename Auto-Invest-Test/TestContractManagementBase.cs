using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Auto_Invest_Strategy;
using Moq;
using Newtonsoft.Json;

namespace Auto_Invest_Test;

public class TestContractManagementBase
{
    protected decimal Funds = 1000;
    protected decimal TradePercentage = 10;
    protected decimal InitialSize;
    protected decimal LastTradePrice;
    private const uint SafetyBands = 10;
    protected decimal TrailingOffset = 1;
    protected decimal MarginProtection = 0.01M;
    protected Contract Contract;
    protected readonly Mock<IContractClient> ContractClientMock = new();
    protected ContractManager ContractManager;
    private TrailingBuySellStrategy _strategy;
    protected Dictionary<int, StopLimit> StopLimits;
    protected int MovingAveSize = 1;
    protected string Symbol = "SPGI";

    public async Task simulate_trades(IEnumerable<decimal> trades)
    {
        IOrderCompletion orderCompletion = null;
        var orderId = 1;
        StopLimits = new Dictionary<int, StopLimit>();

        Contract = new Contract(Symbol, Funds, TrailingOffset, TradePercentage, InitialSize, safetyBands: SafetyBands, marginProtection: MarginProtection);
        ContractClientMock
            .Setup(_ => _.ListenForCompletion(Symbol, It.IsAny<IOrderCompletion>()))
            .Callback((string s, IOrderCompletion o) => { orderCompletion = o; });
        ContractClientMock
            .Setup(_ => _.PlaceStopLimit(It.IsAny<StopLimit>()))
            .ReturnsAsync((StopLimit l) =>
            {
                if (l.OrderId < 1) l.OrderId = orderId++;
                StopLimits[l.OrderId] = l;
                return new ContractResult { OrderId = l.OrderId };
            });
        ContractClientMock
            .Setup(_ => _.CancelOrder(It.IsAny<int>()))
            .Callback(async (int id) => await Task.Run(() =>
            {
                if (StopLimits.ContainsKey(id))
                    StopLimits.Remove(id);
            }));

        TrailingBuySellStrategy.MovingAverageSize = MovingAveSize;
        ContractManager = new ContractManager(ContractClientMock.Object);
        ContractManager.RegisterContract(Contract);
        _strategy = new TrailingBuySellStrategy(ContractManager);

        var previousTrade = -1M;
        foreach (var trade in trades)
        {
            if (previousTrade == -1) previousTrade = trade;

            var min = Math.Min(trade, previousTrade);
            var max = Math.Max(trade, previousTrade);
            var limits = StopLimits.Values.ToArray();

            foreach (var limit in limits)
            {
                if (limit == null) continue;
                if (limit.StopPrice < min || limit.StopPrice > max) continue;
                var slippage = limit.Side == ActionSide.Sell ? -0.1M : 0.1M;
                var price = limit.StopPrice + slippage;
                var orderCost = price * limit.Quantity;
                var commission = Math.Max(1M, limit.Quantity * 0.02M);

                await orderCompletion?.OrderCompleted(new CompletedOrder
                {
                    OrderId = limit.OrderId,
                    Commission = commission,
                    CostOfOrder = orderCost,
                    PricePerUnit = price,
                    Qty = limit.Quantity,
                    Side = limit.Side,
                    Symbol = limit.Symbol
                });

                StopLimits.Remove(limit.OrderId);
            }

            await _strategy.Tick(new TickPosition
            {
                Position = trade,
                Symbol = Symbol
            });
            LastTradePrice = trade;

            previousTrade = trade;
        }
        Contract = await ContractManager.GetContractState(Symbol);
    }
}