using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace Auto_Invest_Test
{

    public class BackTests : TestContractManagementBase
    {
        public IEnumerable<decimal> PolygonValues(DateTime start, DateTime endDate)
        {
            var basePath = Path.GetFullPath("../../../../", Environment.CurrentDirectory);
            var dataPath = Path.Join(basePath, "Data", $"Polygon-{Symbol}");
            var random = new Random((int)DateTime.UtcNow.Ticks);

            var dateIndex = start;
            while (true)
            {
                if (dateIndex > endDate) break;

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

            string FileName(DateTime fileDate) => Path.Join(dataPath, $"{Symbol}-{fileDate:yyyy-MM-dd}.json");
        }

        public IEnumerable<decimal> TickValues(DateTime start, DateTime endDate)
        {
            var basePath = Path.GetFullPath("../../../../", Environment.CurrentDirectory);
            var dataPath = Path.Join(basePath, "Data", $"Tick-{Symbol}");
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

            string FileName(DateTime fileDate) => Path.Join(dataPath, $"{Symbol}-{fileDate:yyyy-MM}.csv");
        }

        [Fact]
        public async Task back_testing_SPGI_polygon()
        {
            Symbol = "SPGI";
            TrailingOffset = 0.1M;
            MarginProtection = 1M;
            Funds = 1000M;

            var start = new DateTime(2016, 2, 1);
            var endDate = new DateTime(2022, 4, 10);

            await RunTest(start, endDate, PolygonValues);
        }

        [Fact]
        public async Task back_testing_NDAQ_polygon()
        {
            Symbol = "NDAQ";
            TrailingOffset = 0.1M;
            MarginProtection = 1M;
            Funds = 1000M;

            var start = new DateTime(2007, 4, 1);
            var endDate = new DateTime(2022, 4, 10);

            await RunTest(start, endDate, PolygonValues);
        }

        [Fact]
        public async Task back_testing_SPGI_tick()
        {
            Symbol = "SPGI";
            TrailingOffset = 0.1M;
            MarginProtection = 1M;
            Funds = 1000M;

            var start = new DateTime(2019, 12, 1);
            var endDate = new DateTime(2022, 3, 1);

            await RunTest(start, endDate, TickValues);
        }

        private async Task RunTest(DateTime start, DateTime endDate, Func<DateTime, DateTime, IEnumerable<decimal>> getValues)
        {
            Trace.WriteLine($"start funding:{Funds:C} ");

            var enumTicks = getValues(start, endDate);
            await simulate_trades(enumTicks);

            var checkC = await ContractManager.GetContractState(Symbol);
            var totalAssets = checkC.Funding + checkC.QuantityOnHand * checkC.AveragePrice;
            var netp = (totalAssets - Funds) / Funds;
            var diffTimeSpan = endDate.Subtract(start);
            var perday = netp / (decimal)diffTimeSpan.TotalDays;

            Trace.WriteLine(
                $"end funding:{checkC.Funding:C} size:{checkC.QuantityOnHand:F} ave price:{checkC.AveragePrice:F} total assets:{totalAssets:C}");
            Trace.WriteLine($"total funds:{(checkC.Funding - Funds) / Funds:P} net with assets:{netp:P} ");
            Trace.WriteLine($"average per year:{perday * 365:P} per month:{perday * 30:P} per day:{perday:P}");
            Trace.WriteLine("DONE");
        }
    }
}
