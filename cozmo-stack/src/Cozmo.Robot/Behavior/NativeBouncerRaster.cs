using Cozmo.Robot.Animation;
using System.Globalization;

namespace Cozmo.Robot.Behavior;

// Bouncer's bounded CV_8UC1/LINE8 paths from shipped OpenCV drawing.cpp.
internal static class NativeBouncerRaster
{
    private static readonly string[] Glyphs = {
        @"H\QFNGLJKOKRLWNZQ[S[VZXWYRYOXJVGSFQF OGMJLOLRMWOZ NYQZSZVY UZWWXRXOWJUG VHSGQGNH",
        @"H\NJPISFS[ NJNKPJRHR[S[",
        @"H\LKLJMHNGPFTFVGWHXJXLWNUQL[ LKMKMJNHPGTGVHWJWLVNTQK[ LZYZY[ K[Y[",
        @"H\MFXFQO MFMGWG WFPO QNSNVOXQYTYUXXVZS[P[MZLYKWLW POSOVPXS TOWQXTXUWXTZ XVVYSZPZMYLW OZLX",
        @"H\UIU[V[ VFV[ VFKVZV UILV LUZUZV",
        @"H\MFLO NGMN MFWFWG NGWG MNPMSMVNXPYSYUXXVZS[P[MZLYKWLW LOMOONSNVOXR TNWPXSXUWXTZ XVVYSZPZMYLW OZLX",
        @"H\VGWIXIWGTFRFOGMJLOLTMXOZR[S[VZXXYUYTXQVOSNRNOOMQ WHTGRGOH PGNJMOMTNXQZ MVOYRZSZVYXV TZWXXUXTWQTO XSVPSOROOPMS QONQMT",
        @"H\KFYFO[ KFKGXG XFN[O[",
        @"H\PFMGLILKMMNNPOTPVQWRXTXWWYTZPZMYLWLTMRNQPPTOVNWMXKXIWGTFPF NGMIMKNMPNTOVPXRYTYWXYWZT[P[MZLYKWKTLRNPPOTNVMWKWIVG WHTGPGMH LXOZ UZXX",
        @"H\WPURRSQSNRLPKMKLLINGQFRFUGWIXMXRWWUZR[P[MZLXMXNZ WMVPSR WNUQRRQRNQLN PRMPLMLLMIPG LKNHQGRGUHWK SGVIWMWRVWTZ UYRZPZMY"
    };
    internal static byte[] Draw(NativeBouncerGame game)
    {
        var pixels = new byte[128 * 64];
        float left=Math.Clamp(game.PaddleX-16f,0f,game.Width), right=Math.Clamp(game.PaddleX+16f,0f,game.Width);
        float top=Math.Clamp(game.PaddleY,0f,game.Height), bottom=Math.Clamp(game.PaddleY+4f,0f,game.Height);
        Rectangle(pixels,OpenCv310.CvRound(left),OpenCv310.CvRound(top),OpenCv310.CvRound(right-left),OpenCv310.CvRound(bottom-top));
        Circle(pixels,OpenCv310.CvRound(Math.Clamp(game.BallX,0f,game.Width)),OpenCv310.CvRound(Math.Clamp(game.BallY,0f,game.Height)));
        string player=game.PlayerScore.ToString(CultureInfo.InvariantCulture), opponent=game.OpponentScore.ToString(CultureInfo.InvariantCulture);
        Text(pixels,player,4,12); Text(pixels,opponent,OpenCv310.CvRound(game.Width-4f-8f*opponent.Length),12);
        return pixels;
    }
    internal static void Rectangle(byte[] p,int x,int y,int w,int h)
    {
        if(w<=0||h<=0)return;
        for(int row=Math.Max(0,y);row<Math.Min(64,y+h);row++)
            for(int col=Math.Max(0,x);col<Math.Min(128,x+w);col++)p[row*128+col]=255;
    }
    internal static void Circle(byte[] p,int x,int y)
    {
        int[] spans={0,2,2,3,2,2,0};
        for(int dy=-3;dy<=3;dy++)for(int dx=-spans[dy+3];dx<=spans[dy+3];dx++)Plot(p,x+dx,y+dy);
    }
    internal static void Text(byte[] p,string text,int x,int y)
    {
        int viewX=x<<16, viewY=(y<<16)-9*32768;
        foreach(char c in text)
        {
            string glyph=Glyphs[c-'0']; int baseX=viewX+10*32768; int? previousX=null,previousY=null;
            for(int i=2;i<glyph.Length;)
            {
                if(glyph[i]==' '){previousX=previousY=null;i++;continue;}
                int px=baseX+(glyph[i++]-'R')*32768, py=viewY+(glyph[i++]-'R')*32768;
                if(previousX is { } ax && previousY is { } ay)Line(p,ax,ay,px,py);
                previousX=px;previousY=py;
            }
            viewX+=10*65536;
        }
    }
    private static void Plot(byte[] p,int x,int y){if((uint)x<128 && (uint)y<64)p[y*128+x]=255;}
    private static void Line(byte[] p,int x1,int y1,int x2,int y2)
    {
        if(!OpenCv310.ClipLine(128<<16,64<<16,ref x1,ref y1,ref x2,ref y2))return;
        int dx=x2-x1,dy=y2-y1,ax=Math.Abs(dx),ay=Math.Abs(dy);
        bool xMajor=ax>ay;
        if((xMajor&&dx<0)||(!xMajor&&dy<0)){(x1,x2)=(x2,x1);(y1,y2)=(y2,y1);dx=-dx;dy=-dy;}
        Plot(p,(x2+32768)>>16,(y2+32768)>>16);
        if(xMajor)
        {
            int step=(int)(((long)dy<<16)/(ax|1)),count=(x2-x1)>>16,x=(x1+32768)>>16,y=y1+32768;
            for(;count>=0;count--,x++,y+=step)Plot(p,x,y>>16);
        }
        else
        {
            int step=(int)(((long)dx<<16)/(ay|1)),count=(y2-y1)>>16,y=(y1+32768)>>16,x=x1+32768;
            for(;count>=0;count--,y++,x+=step)Plot(p,x>>16,y);
        }
    }
}
