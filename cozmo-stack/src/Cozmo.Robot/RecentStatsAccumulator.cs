namespace Cozmo.Robot;

// fidelity: M1-061 — 0x0082FB64..0x0082FCBA, window AddStat 0x0062F6A2..0x0062F70E.
internal sealed class RecentStatsAccumulator(uint limit)
{
    internal sealed class Window
    {
        internal double Sum, Count, Mean, PreviousMean, M2;
        internal double Max = -double.MaxValue, Min = double.MaxValue;
        internal void Clear()
        {
            Sum=Count=Mean=PreviousMean=M2=0;
            Max=-double.MaxValue;Min=double.MaxValue;
        }
        internal void Add(double value)
        {
            Sum=Sum+value;Count=Count+1;
            if(Max<value)Max=value;
            if(Min>value)Min=value;
            double oldMean=Mean;
            double delta=value-oldMean;
            double newMean=oldMean+delta/Count;
            double term=(value-newMean)*delta;
            M2=M2+term;Mean=newMean;PreviousMean=oldMean;
        }
    }
    internal Window First { get; }=new();
    internal Window Second { get; }=new();
    internal Window Selected=>Second.Count>First.Count?Second:First;
    internal void Add(double value)
    {
        First.Add(value);
        if(First.Count>0.5*(double)limit || Second.Count>BitConverter.Int64BitsToDouble(0x3F50624DD2F1A9FC))Second.Add(value);
        if(First.Count>limit)First.Clear();
        if(Second.Count>limit)Second.Clear();
    }
}
