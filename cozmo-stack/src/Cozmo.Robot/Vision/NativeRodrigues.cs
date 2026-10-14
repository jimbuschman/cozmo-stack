namespace Cozmo.Robot.Vision;
// fidelity: M11-067
// CV_32F Rodrigues callers; shipped OpenCV double Jacobi SVD, no NEON or fused operations.
internal static class NativeRodrigues
{
    private static readonly double JacobiEps=10*BitConverter.Int64BitsToDouble(0x3CB0000000000000);
    private static readonly double JacobiMinVal=BitConverter.Int64BitsToDouble(0x0010000000000000);
    private static readonly double SmallS=BitConverter.Int64BitsToDouble(0x3EE4F8B588E368F1); // 1e-5, cvRodrigues2 small-s bound.
    private const ulong RngSeed=0x12345678,RngCoeff=0xF83F630A;
    private static double NonNegative(double v)=>v<0?0:v; // R180: NaN and signed zero retained.
    private static double Hypot(double a,double b)=>OpenCvSolveSvd.Hypot(a,b);
    internal static Mat3 VectorToMatrix(Vec3 vector)
    {
        double x=(float)vector.X,y=(float)vector.Y,z=(float)vector.Z;
        double n=y*y;n+=x*x;n+=z*z;double theta=Math.Sqrt(n); // IEEE sqrt.
        if(theta<BitConverter.Int64BitsToDouble(0x3CB0000000000000))return Mat3.Identity;
        double c=Math.Cos(theta),s=Math.Sin(theta); // EQUIVALENT_IMPLEMENTATION M13-023(e): unshipped bionic cos/sin.
        double c1=1-c,inv=theta!=0?1/theta:0;x*=inv;y*=inv;z*=inv;
        double[] axis={x,y,z},skew={0,-z,y,z,0,-x,-y,x,0};var result=new double[9];
        for(int i=0;i<3;i++)for(int j=0;j<3;j++)
        {int k=i*3+j;double v=c1*(axis[i]*axis[j]);v+=c*(i==j?1:0);v+=s*skew[k];result[k]=(float)v;}
        return new Mat3(result);
    }
    internal static Vec3 MatrixToVector(Mat3 matrix)
    {
        var original=new double[9];
        for(int i=0;i<3;i++)for(int j=0;j<3;j++){double element=(float)matrix[i,j];if(!(element>=-100&&element<100))return Vec3.Zero;original[i*3+j]=element;}
        // _SVDcompute transposes the input into At before the row-pair Jacobi walk.
        var at=new double[9];for(int i=0;i<3;i++)for(int j=0;j<3;j++)at[i*3+j]=original[j*3+i];
        var w=new double[3];var vt=new double[9];JacobiSvd(at,w,vt,3,3,3);
        // GEMM_A_T: U is the transpose of At, Vt rows are retained.
        var r=new double[9];for(int i=0;i<3;i++)for(int j=0;j<3;j++){double sum=0;for(int k=0;k<3;k++)sum+=at[k*3+i]*vt[k*3+j];r[i*3+j]=sum*1d;}
        double x=r[7]-r[5],y=r[2]-r[6],z=r[3]-r[1];
        double v=y*y;v+=x*x;v+=z*z;v*=0.25;double s=Math.Sqrt(v); // IEEE sqrt.
        double c=r[0]+r[4];c+=r[8];c-=1;c*=0.5;if(c>1)c=1;if(c< -1)c=-1;
        double theta=Math.Acos(c); // EQUIVALENT_IMPLEMENTATION M13-023(e): bionic acos.
        if(s<SmallS)
        {
            if(c>0)return Vec3.Zero;
            x=Math.Sqrt(NonNegative((r[0]+1)*0.5));
            y=Math.Sqrt(NonNegative((r[4]+1)*0.5))*(r[1]<0?-1:1);
            z=Math.Sqrt(NonNegative((r[8]+1)*0.5))*(r[2]<0?-1:1);
            if(Math.Abs(x)<Math.Abs(y)&&Math.Abs(x)<Math.Abs(z)&&(r[5]>0)!=(y*z>0))z=-z;
            v=y*y;v+=x*x;v+=z*z;double scale=theta/Math.Sqrt(v);x*=scale;y*=scale;z*=scale;
        }
        else{double scale=(1/(s+s))*theta;x*=scale;y*=scale;z*=scale;}
        return new((float)x,(float)y,(float)z);
    }
    internal static void JacobiSvd(double[] at, double[] wOut, double[] vt, int m, int n, int n1)
    {
        var w = new double[n];
        int maxIter = Math.Max(m, 30);
        for (int i = 0; i < n; i++)
        {
            double sd = 0;
            for (int k = 0; k < m; k++) { double t = at[i * m + k]; sd += (double)t * t; }
            w[i] = sd;
            for (int k = 0; k < n; k++) vt[i * n + k] = 0;
            vt[i * n + i] = 1d;
        }

        for (int iter = 0; iter < maxIter; iter++)
        {
            bool changed = false;
            for (int i = 0; i < n - 1; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double a = w[i], p = 0, b = w[j];
                    int ai = i * m, aj = j * m;
                    for (int k = 0; k < m; k++) p += (double)at[ai + k] * at[aj + k];
                    double ab = a * b;
                    if (Math.Abs(p) <= (double)JacobiEps * Math.Sqrt(ab)) continue;

                    p *= 2;
                    double beta = a - b, gamma = Hypot(p, beta);
                    double c, s;
                    if (beta < 0)
                    {
                        double delta = (gamma - beta) * 0.5;
                        s = (double)Math.Sqrt(delta / gamma);
                        c = (double)(p / ((gamma*s)+(gamma*s)));
                    }
                    else
                    {
                        c = (double)Math.Sqrt((gamma + beta) / (gamma+gamma));
                        s = (double)(p / ((gamma*c)+(gamma*c)));
                    }

                    a = b = 0;
                    for (int k = 0; k < m; k++)
                    {
                        double ak = at[ai + k], bk = at[aj + k];
                        double t0 = s * bk + c * ak;
                        double t1 = c * bk + (-s) * ak;
                        at[ai + k] = t0; at[aj + k] = t1;
                        a += (double)t0 * t0; b += (double)t1 * t1;
                    }
                    w[i] = a; w[j] = b;
                    changed = true;

                    int vi = i * n, vj = j * n;
                    for (int k = 0; k < n; k++)
                    {
                        double xi = vt[vi + k], xj = vt[vj + k];
                        double t0 = s * xj + c * xi;
                        double t1 = c * xj + (-s) * xi;
                        vt[vi + k] = t0; vt[vj + k] = t1;
                    }
                }
            if (!changed) break;
        }

        for (int i = 0; i < n; i++)
        {
            double sd = 0;
            for (int k = 0; k < m; k++) { double t = at[i * m + k]; sd += (double)t * t; }
            w[i] = Math.Sqrt(sd);
        }

        for (int i = 0; i < n - 1; i++)
        {
            int j = i;
            for (int k = i + 1; k < n; k++) if (w[j] < w[k]) j = k;
            if (i != j)
            {
                (w[i], w[j]) = (w[j], w[i]);
                for (int k = 0; k < m; k++) (at[i * m + k], at[j * m + k]) = (at[j * m + k], at[i * m + k]);
                for (int k = 0; k < n; k++) (vt[i * n + k], vt[j * n + k]) = (vt[j * n + k], vt[i * n + k]);
            }
        }
        for (int i = 0; i < n; i++) wOut[i] = (double)w[i];
        ulong rng = RngSeed;
        for (int i = 0; i < n1; i++)
        {
            double sd = i < n ? w[i] : 0;
            int tries = 100;
            while (sd <= JacobiMinVal)
            {
                double val0 = (double)(1.0 / m);
                for (int k = 0; k < m; k++)
                {
                    rng = (ulong)(uint)rng * RngCoeff + (rng >> 32);
                    at[i * m + k] = ((uint)rng & 256u) != 0 ? val0 : -val0;
                }
                for (int pass = 0; pass < 2; pass++)
                    for (int j = 0; j < i; j++)
                    {
                        sd = 0;
                        for (int k = 0; k < m; k++) { double prod = at[i * m + k] * at[j * m + k]; sd += prod; }
                        double asum = 0d;
                        for (int k = 0; k < m; k++)
                        {
                            double t = (double)((double)at[i * m + k] - sd * at[j * m + k]);
                            at[i * m + k] = t;
                            asum += Math.Abs(t);
                        }
                        double thr = JacobiEps * 100d;
                        asum = asum > thr ? 1d / asum : 0d;
                        for (int k = 0; k < m; k++) at[i * m + k] *= asum;
                    }
                sd = 0;
                for (int k = 0; k < m; k++) { double t = at[i * m + k]; sd += (double)t * t; }
                sd = Math.Sqrt(sd);
                if (--tries == 0) break;
            }
            double scale = sd > JacobiMinVal ? (double)(1.0 / sd) : 0d;
            for (int k = 0; k < m; k++) at[i * m + k] *= scale;
        }
    }

}
