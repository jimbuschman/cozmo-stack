namespace Cozmo.Robot.Behavior;

// fidelity: M11-089
/// <summary>5E8350..5E8712 main body. Four unread libc++ helpers remain explicit, including small sorts.</summary>
public sealed class InterestingEdgeGoalSort
{
    private static readonly float NegativeEpsilon=BitConverter.UInt32BitsToSingle(0xB727C5AC);
    public Func<IList<InterestingEdgeGoal>,int[],uint>? Sort3_5E8714 {get;set;}
    public Func<IList<InterestingEdgeGoal>,int[],uint>? Sort4_5E881C {get;set;}
    public Func<IList<InterestingEdgeGoal>,int[],uint>? Sort5_5E88E0 {get;set;}
    public Func<IList<InterestingEdgeGoal>,int,int,bool>? Incomplete5E89DC {get;set;}
    private sealed class MissingSort : Exception {}
    private static bool Less(InterestingEdgeGoal a,InterestingEdgeGoal b)=>b.Score+NegativeEpsilon>a.Score;
    private static void Swap(IList<InterestingEdgeGoal> a,int i,int j){var value=a[i];a[i]=a[j];a[j]=value;}
    private static uint Small(Func<IList<InterestingEdgeGoal>,int[],uint>? helper,string address,IList<InterestingEdgeGoal> a,params int[] indexes)
    {
        if(helper is {} call)return call(a,indexes);
        SteppedBehavior.ReportMissing($"M11-092 libc++ sort helper {address}: exact ascending/equal-score permutation is required here; no stable-sort substitute");
        throw new MissingSort();
    }
    private bool Incomplete(IList<InterestingEdgeGoal> a,int first,int end)
    {
        if(Incomplete5E89DC is {} call)return call(a,first,end);
        SteppedBehavior.ReportMissing("M11-092 libc++ incomplete insertion helper 0x005E89DC");throw new MissingSort();
    }
    public bool TrySort(IList<InterestingEdgeGoal> scores)
    {
        try{Sort(scores,0,scores.Count);return true;}
        catch(MissingSort){return false;}
    }
    private void Sort(IList<InterestingEdgeGoal> a,int first,int end)
    {
        while(true)
        {
            int n=end-first;
            if(n<=1)return;
            if(n==2){if(Less(a[end-1],a[first]))Swap(a,first,end-1);return;}
            if(n==3){Small(Sort3_5E8714,"0x005E8714",a,first,first+1,end-1);return;}
            if(n==4){Small(Sort4_5E881C,"0x005E881C",a,first,first+1,first+2,end-1);return;}
            if(n==5){Small(Sort5_5E88E0,"0x005E88E0",a,first,first+1,first+2,first+3,end-1);return;}
            if(n<31)
            {
                Small(Sort3_5E8714,"0x005E8714",a,first,first+1,first+2);
                for(int current=first+3;current<end;current++)
                    if(Less(a[current],a[current-1]))
                    {
                        var value=a[current];int position=current;
                        do{a[position]=a[position-1];position--;}
                        while(position!=first && Less(value,a[position-1]));
                        a[position]=value;
                    }
                return;
            }
            int middle=first+n/2,last=end-1;
            uint swaps=n>=1000?Small(Sort5_5E88E0,"0x005E88E0",a,first,first+n/4,middle,middle+n/4,last)
                :Small(Sort3_5E8714,"0x005E8714",a,first,middle,last);
            int i=first,j=last;
            if(!Less(a[i],a[middle]))
            {
                bool restart=false;
                while(true)
                {
                    if(i==--j)
                    {
                        i++;j=end;
                        if(!Less(a[first],a[--j]))
                        {
                            while(true)
                            {
                                if(i==j)return;
                                if(Less(a[first],a[i])){Swap(a,i,j);break;}
                                i++;
                            }
                        }
                        if(i==j)return;
                        while(true)
                        {
                            while(!Less(a[first],a[i]))i++;
                            while(Less(a[first],a[--j])){}
                            if(i>=j)break;
                            Swap(a,i,j);i++;
                        }
                        first=i;restart=true;break;
                    }
                    if(Less(a[j],a[middle])){Swap(a,i,j);swaps++;break;}
                }
                if(restart)continue;
            }
            i++;
            if(i<j)
                while(true)
                {
                    while(Less(a[i],a[middle]))i++;
                    while(!Less(a[--j],a[middle])){}
                    if(i>j)break;
                    Swap(a,i,j);swaps++;
                    if(middle==i)middle=j;
                    i++;
                }
            if(i!=middle && Less(a[middle],a[i])){Swap(a,i,middle);swaps++;}
            if(swaps==0)
            {
                bool left=Incomplete(a,first,i),right=Incomplete(a,i+1,end);
                if(right){if(left)return;end=i;continue;}
                if(left){first=i+1;continue;}
            }
            if(i-first<end-i){Sort(a,first,i);first=i+1;}
            else{Sort(a,i+1,end);end=i;}
        }
    }
}
