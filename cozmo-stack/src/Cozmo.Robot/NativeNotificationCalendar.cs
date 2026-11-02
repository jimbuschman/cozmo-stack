using System.Runtime.InteropServices;

namespace Cozmo.Robot;

// Host C calendar is the app-boundary timezone/libc input. It preserves tm_isdst=0
// and lets mktime normalize the source's decremented hour, including historic offsets.
internal static class NativeNotificationCalendar
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Tm
    {
        internal int Second,Minute,Hour,Day,Month,Year,Weekday,YearDay,IsDst;
        internal long GmtOffset;
        internal IntPtr Zone;
    }
    [DllImport("ucrtbase.dll",EntryPoint="_mktime64",CallingConvention=CallingConvention.Cdecl)] private static extern long WindowsMktime(ref Tm value);
    [DllImport("ucrtbase.dll",EntryPoint="_localtime64_s",CallingConvention=CallingConvention.Cdecl)] private static extern int WindowsLocaltime(out Tm result,ref long time);
    [DllImport("libc",EntryPoint="mktime",CallingConvention=CallingConvention.Cdecl)] private static extern long UnixMktime(ref Tm value);
    [DllImport("libc",EntryPoint="localtime_r",CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr UnixLocaltime(ref long time,out Tm result);
    internal static bool TryMktime(DateTime local,out int seconds)
    {
        seconds=0;var value=new Tm{Year=local.Year-1900,Month=local.Month-1,Day=local.Day,Hour=local.Hour-1,Minute=local.Minute,Second=0,IsDst=0};
        try
        {
            long result=OperatingSystem.IsWindows()?WindowsMktime(ref value):OperatingSystem.IsLinux()?UnixMktime(ref value):throw new PlatformNotSupportedException();
            if(result<int.MinValue||result>int.MaxValue)return false;
            seconds=(int)result;return true;
        }
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException){return false;}
    }
    internal static bool TryLocalMinute(long epochSeconds,out float minute)
    {
        minute=0;if(epochSeconds<int.MinValue||epochSeconds>int.MaxValue)return false;
        try
        {
            Tm value;
            if(OperatingSystem.IsWindows()){if(WindowsLocaltime(out value,ref epochSeconds)!=0)return false;}
            else if(OperatingSystem.IsLinux()){if(UnixLocaltime(ref epochSeconds,out value)==IntPtr.Zero)return false;}
            else return false;
            minute=(float)(value.Hour*60f+value.Minute)+(float)(value.Second/60f);return true;
        }
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException){return false;}
    }
    internal static bool TryLocalDay(long epochSeconds,out int year,out int yearDay)
    {
        year=yearDay=0;if(epochSeconds<int.MinValue||epochSeconds>int.MaxValue)return false;
        try
        {
            Tm value;
            if(OperatingSystem.IsWindows()){if(WindowsLocaltime(out value,ref epochSeconds)!=0)return false;}
            else if(OperatingSystem.IsLinux()){if(UnixLocaltime(ref epochSeconds,out value)==IntPtr.Zero)return false;}
            else return false;
            year=value.Year;yearDay=value.YearDay;return true;
        }
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException){return false;}
    }
}
