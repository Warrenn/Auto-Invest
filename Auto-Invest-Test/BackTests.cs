using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace Auto_Invest_Test
{

    public class BackTests : TestContractManagementBase
    {
        public IEnumerable<decimal> PolygonValues(string symbol, DateTime start, DateTime endDate)
        {
            var basePath = Path.GetFullPath("../../../../", Environment.CurrentDirectory);
            var dataPath = Path.Join(basePath, $"Data\\Polygon-{symbol}");
            var random = new Random((int)DateTime.UtcNow.Ticks);

            var dateIndex = start;
            while (true)
            {
                if (dateIndex >= endDate) break;

                var jsonFileName = FileName(dateIndex);
                dateIndex = dateIndex.AddDays(1);

                if (!File.Exists(jsonFileName)) continue;

                var content = File.ReadAllText(jsonFileName);
                if (string.IsNullOrWhiteSpace(content)) continue;

                var jsonData = JsonConvert.DeserializeObject<TickFileData>(content);
                if (jsonData?.results == null || !jsonData.results.Any()) continue;

                foreach (var candle in jsonData.results)
                {
                    yield return candle.o;

                    if (random.Next(0, 2) == 0)
                    {
                        yield return candle.l;
                        yield return candle.h;
                    }
                    else
                    {
                        yield return candle.h;
                        yield return candle.l;
                    }

                    yield return candle.c;
                }

            }

            string FileName(DateTime fileDate) => Path.Join(dataPath, $"{symbol.ToUpper()}-{fileDate:yyyy-MM-dd}.json");
        }

        public IEnumerable<decimal> TickValues(string symbol, DateTime start, DateTime endDate)
        {
            var basePath = Path.GetFullPath("../../../../", Environment.CurrentDirectory);
            var dataPath = Path.Join(basePath, $"Data\\Tick-{symbol}");
            var random = new Random((int)DateTime.UtcNow.Ticks);

            var dateIndex = start;
            while (true)
            {
                if (dateIndex > endDate) break;

                var fileName = FileName(dateIndex);
                dateIndex = dateIndex.AddMonths(1);

                if (!File.Exists(fileName)) continue;

                using var stream = File.OpenRead(fileName);
                var reader = new StreamReader(stream);
                reader.ReadLine();
                var line = reader.ReadLine();

                while (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split(',');
                    line = reader.ReadLine();
                    if (parts.Length != 7) continue;

                    yield return decimal.Parse(parts[2]);

                    if (random.Next(0, 2) == 0)
                    {
                        yield return decimal.Parse(parts[4]);
                        yield return decimal.Parse(parts[3]);
                    }
                    else
                    {
                        yield return decimal.Parse(parts[3]);
                        yield return decimal.Parse(parts[4]);
                    }

                    yield return decimal.Parse(parts[5]);
                }
            }

            string FileName(DateTime fileDate) => Path.Join(dataPath, $"{symbol.ToUpper()}-{fileDate:yyyy-MM}.csv");
        }

        [Fact]
        public async Task back_testing_SPGI_polygon()
        {
            TrailingOffset = 0.1M;
            MarginProtection = 1M;
            Funds = 1000M;

            Trace.WriteLine($"start funding:{Funds:C} ");

            var enumTicks = PolygonValues("SPGI", new DateTime(2021, 2, 1), new DateTime(2022, 4, 10));
            await simulate_trades(enumTicks);

            var checkC = await ContractManager.GetContractState(SYMBOL);
            var netp = (checkC.Funding + (checkC.QuantityOnHand * checkC.AveragePrice) - Funds) / Funds;

            Trace.WriteLine($"end funding:{checkC.Funding:C} qty:{checkC.QuantityOnHand:F} ave:{checkC.AveragePrice:F} total assets{checkC.Funding + checkC.QuantityOnHand * checkC.AveragePrice:C}");
            Trace.WriteLine($"total % :{(checkC.Funding - Funds) / Funds:P} net with assets % :{netp:P} ");
            Trace.WriteLine("DONE");
        }

        [Fact]
        public async Task back_testing_SPGI_tick()
        {
            TrailingOffset = 0.1M;
            MarginProtection = 1M;
            Funds = 1000M;

            Trace.WriteLine($"start funding:{Funds:C} ");

            var enumTicks = TickValues("SPGI", new DateTime(2019, 12, 1), new DateTime(2022, 3, 1));
            await simulate_trades(enumTicks);

            var checkC = await ContractManager.GetContractState(SYMBOL);
            var netp = (checkC.Funding + (checkC.QuantityOnHand * checkC.AveragePrice) - Funds) / Funds;

            Trace.WriteLine($"end funding:{checkC.Funding:C} qty:{checkC.QuantityOnHand:F} ave:{checkC.AveragePrice:F} total assets{checkC.Funding + checkC.QuantityOnHand * checkC.AveragePrice:C}");
            Trace.WriteLine($"total % :{(checkC.Funding - Funds) / Funds:P} net with assets % :{netp:P} ");
            Trace.WriteLine("DONE");
        }
    }
}
