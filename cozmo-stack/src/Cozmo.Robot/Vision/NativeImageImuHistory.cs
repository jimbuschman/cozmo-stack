using Cozmo.Protocol;
namespace Cozmo.Robot.Vision;

// fidelity: M11-098, M11-033
internal readonly record struct NativeImageImu(uint ImageId,float RateX,float RateY,float RateZ,byte Line,uint ComputedTimestamp);
internal sealed class NativeImageImuHistory
{
    private readonly object _gate=new();
    private readonly List<NativeImageImu> _records=new(20);
    internal void Add(ImageImuData data)
    {
        lock(_gate)
        {
            if(_records.Count==20)_records.RemoveAt(0);
            _records.Add(new(data.ImageId,data.RateX,data.RateY,data.RateZ,data.Line2Number,0));
        }
    }
    internal void Associate(uint id,uint timestamp,int height)
    {
        if(height<=0)throw new ArgumentOutOfRangeException(nameof(height));
        lock(_gate)
        {
            float scale=65f/(float)height, time=(float)timestamp;uint prior=unchecked(id-1);
            for(int i=0;i<_records.Count;i++)
            {
                var item=_records[i];
                if(item.ImageId==id)
                {
                    float delta=(float)(height-item.Line);
                    _records[i]=item with {ComputedTimestamp=ObservableObject.SaturatingU32(time-scale*delta)};
                }
                else if(item.ImageId==prior && item.ComputedTimestamp==0 && i>0 && _records[i-1].ComputedTimestamp!=0)
                {
                    var previous=_records[i-1];float delta=(float)((int)item.Line-previous.Line);
                    _records[i]=item with {ComputedTimestamp=ObservableObject.SaturatingU32(scale*delta+(float)previous.ComputedTimestamp)};
                }
            }
        }
    }
    internal NativeImageImu[] Snapshot(){lock(_gate)return _records.ToArray();}
    internal static bool TryBracket(IReadOnlyList<NativeImageImu> records,uint timestamp,ref NativeImageImu before,ref NativeImageImu after)
    {
        for(int i=1;i<records.Count;i++)
        {
            var candidate=records[i];var prior=records[i-1];
            int difference=unchecked((int)(prior.ComputedTimestamp-timestamp));
            int magnitude=difference<0?unchecked(-difference):difference;
            if(candidate.ComputedTimestamp>timestamp || candidate.ComputedTimestamp==0 && magnitude<=64)
            {before=prior;after=candidate;return true;}
        }
        return false;
    }
}
