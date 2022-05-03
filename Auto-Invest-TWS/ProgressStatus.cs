using System;

namespace Auto_Invest
{
    [Flags]
    public enum ProgressStatus
    {
        Placed = 1,
        Execution = 2,
        Commission = 4
    }
}
