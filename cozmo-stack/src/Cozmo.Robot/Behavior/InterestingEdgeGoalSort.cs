namespace Cozmo.Robot.Behavior;

// fidelity: M11-089, M11-092
/// <summary>
/// The libc++ sort the goal list goes through: main body 0x005E8350..0x005E8712 and its helpers __sort3 0x005E8714,
/// __sort4 0x005E881C, __sort5 0x005E88E0 and __insertion_sort_incomplete 0x005E89DC. The comparator is the engine's
/// <c>ordered(b.score - 1e-5f &gt; a.score)</c>: near ties and NaN compare false, so no secondary key and no stable order.
/// </summary>
public sealed class InterestingEdgeGoalSort
{
    private static readonly float NegativeEpsilon=BitConverter.UInt32BitsToSingle(0xB727C5AC); // 0x005E86EC
    private static bool Less(InterestingEdgeGoal a,InterestingEdgeGoal b)=>b.Score+NegativeEpsilon>a.Score;
    private static void Swap(IList<InterestingEdgeGoal> a,int i,int j){var value=a[i];a[i]=a[j];a[j]=value;}

    private static uint Sort3(IList<InterestingEdgeGoal> a,int x,int y,int z)
    {
        uint swaps=0;
        if(!Less(a[y],a[x]))
        {
            if(!Less(a[z],a[y]))return swaps;
            Swap(a,y,z);swaps=1;
            if(Less(a[y],a[x])){Swap(a,x,y);swaps=2;}
            return swaps;
        }
        if(Less(a[z],a[y])){Swap(a,x,z);return 1;}
        Swap(a,x,y);swaps=1;
        if(Less(a[z],a[y])){Swap(a,y,z);swaps=2;}
        return swaps;
    }
    private static uint Sort4(IList<InterestingEdgeGoal> a,int w,int x,int y,int z)
    {
        uint swaps=Sort3(a,w,x,y);
        if(!Less(a[z],a[y]))return swaps;
        Swap(a,y,z);swaps++;
        if(!Less(a[y],a[x]))return swaps;
        Swap(a,x,y);swaps++;
        if(!Less(a[x],a[w]))return swaps;
        Swap(a,w,x);return swaps+1;
    }
    private static uint Sort5(IList<InterestingEdgeGoal> a,int v,int w,int x,int y,int z)
    {
        uint swaps=Sort4(a,v,w,x,y);
        if(!Less(a[z],a[y]))return swaps;
        Swap(a,y,z);swaps++;
        if(!Less(a[y],a[x]))return swaps;
        Swap(a,x,y);swaps++;
        if(!Less(a[x],a[w]))return swaps;
        Swap(a,w,x);swaps++;
        if(!Less(a[w],a[v]))return swaps;
        Swap(a,v,w);return swaps+1;
    }
    // Saved-record insertion shared by the 6..30 path 0x005E8630..0x005E86B4 and the bounded helper.
    private static void Insert(IList<InterestingEdgeGoal> a,int first,int current)
    {
        var value=a[current];int position=current;
        do{a[position]=a[position-1];position--;}
        while(position!=first && Less(value,a[position-1]));
        a[position]=value;
    }
    private static bool Incomplete(IList<InterestingEdgeGoal> a,int first,int end)
    {
        switch(end-first)
        {
            case 0:case 1:return true;
            case 2:if(Less(a[end-1],a[first]))Swap(a,first,end-1);return true;
            case 3:Sort3(a,first,first+1,end-1);return true;
            case 4:Sort4(a,first,first+1,first+2,end-1);return true;
            case 5:Sort5(a,first,first+1,first+2,first+3,end-1);return true;
        }
        Sort3(a,first,first+1,first+2);
        int moved=0;
        for(int current=first+3;current<end;current++)
            if(Less(a[current],a[current-1]))
            {
                Insert(a,first,current);
                if(++moved==8)return current+1==end; // moves counted, not comparisons; the rest stays unsorted
            }
        return true;
    }
    public void Sort(IList<InterestingEdgeGoal> scores)=>Sort(scores,0,scores.Count);
    private static void Sort(IList<InterestingEdgeGoal> a,int first,int end)
    {
        while(true)
        {
            int n=end-first;
            if(n<=1)return;
            if(n==2){if(Less(a[end-1],a[first]))Swap(a,first,end-1);return;}
            if(n==3){Sort3(a,first,first+1,end-1);return;}
            if(n==4){Sort4(a,first,first+1,first+2,end-1);return;}
            if(n==5){Sort5(a,first,first+1,first+2,first+3,end-1);return;}
            if(n<31)
            {
                Sort3(a,first,first+1,first+2);
                for(int current=first+3;current<end;current++)
                    if(Less(a[current],a[current-1]))Insert(a,first,current);
                return;
            }
            int middle=first+n/2,last=end-1;
            uint swaps=n>=1000?Sort5(a,first,first+n/4,middle,middle+n/4,last):Sort3(a,first,middle,last);
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
                                if(Less(a[first],a[i])){Swap(a,i,j);i++;break;} // 0x005E83DE..0x005E8400, r0 already past i
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
