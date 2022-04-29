using System.Collections.Generic;
using System.Linq;

namespace Auto_Invest_Strategy
{
    public class MovingAverage
    {
        private readonly int _size;
        private IList<decimal> _positions;

        public MovingAverage(int size)
        {
            _size = size;
            Reset();
        }

        public void Reset() => _positions = new List<decimal>();

        public void Add(decimal value)
        {
            if (_positions.Count == _size)
            {
                _positions.RemoveAt(0);
            }

            _positions.Add(value);
        }

        public decimal CurrentAverage => _positions.Average();
    }
}
