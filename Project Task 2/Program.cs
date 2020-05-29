using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Project_Task_2 {
    class Program {
        [MTAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Display form = new Display();
            Console.WriteLine();
            Application.Run(form);
        }
    }

    class Display : Form {
        private Vector<double> MAX_DIST = new Vector<double>(100), SURF_DIST = new Vector<double>(0.01f);
        private const int MAX_STEPS = 100;
        private PictureBox screen;
        private long tf, mspf;
        private bool running;
        private readonly long nspt;
        private Thread renderThread;

        public Display() {
            nspt = 1000000000 / Stopwatch.Frequency;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            InitalizeComponent();
            renderThread = new Thread(RenderLoop);
            renderThread.Start();

        }

        private void InitalizeComponent() {
            this.Width = 800;
            this.Height = 600;
            this.ClientSize = new System.Drawing.Size(Width, Height);
            this.Text = "PT2";
            this.Visible = true;
            screen = new PictureBox();
            screen.Size = this.ClientSize;
            this.Controls.Add(screen);
            tf = mspf = 0;
        }

        private void RenderLoop() {
            running = true;
            Stopwatch time = new Stopwatch();
            time.Start();
            long lastTime = time.ElapsedTicks * nspt;
            double ammountOfTicks = 60.0;
            double ns = 1000000000.0 / ammountOfTicks;
            double delta = 0;
            long timer = time.ElapsedMilliseconds;
            int frames = 0;
            while(running) {
                long now = time.ElapsedTicks * nspt;
                delta += (now - lastTime) / ns;
                lastTime = now;
                while(delta >= 1) {
                    Tick();
                    delta--;
                }
                if(running)
                    Render(time.ElapsedMilliseconds / 1000f);
                frames++;
                if(time.ElapsedMilliseconds - timer > 1000) {
                    timer += 1000;
                    this.Text = String.Format("Avg MSPF: {0} FPS: {1}", mspf / tf, frames);
                    frames = 0;
                }
            }
        }

        private void Tick() {
            
        }

        private void Render(float iTime) {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var output = new Bitmap(Width, Height);
            var rect = new Rectangle(0, 0, Width, Height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte[] frame = new byte[Width * Height * 4];
            var ptr = bmpData.Scan0;
            Parallel.For(0, Height, y => {
                for(int x = 0; x < Width; x++) {
                    Vector4 col = getColorFromPos(new Vector2(x + .5f, Height - y + .5f), iTime);
                    Marshal.Copy(new byte[4] { (byte)col.X, (byte)col.Y, (byte)col.Z, (byte)col.W }, 0, ptr+((x + Width * y) * 4), 4);
                    //Buffer.BlockCopy(new byte[4] { (byte)col.X, (byte)col.Y, (byte)col.Z, (byte)col.W }, 0, frame,
                    //    (x + Width * y) * 4, 4);
                }
            });
            output.UnlockBits(bmpData);
            screen.Image = output;
            screen.Update();
            frame = null;
            output = null;
            stopwatch.Stop();
            tf++;
            mspf += stopwatch.ElapsedMilliseconds;
            Console.WriteLine("Frame Rendered in {0}ms", stopwatch.ElapsedMilliseconds);
        }

        //byte[][] getColorsFromRegion(object state) {
        //    Stopwatch stopwatch = new Stopwatch();
        //    stopwatch.Start();
        //    Vector<float> vals = (Vector<float>)state;
        //    int X = (int)vals[0];
        //    int Y = (int)vals[1];
        //    int size = X * Y * 4;
        //    IntPtr r1 = Marshal.AllocHGlobal(size);
        //    IntPtr r2 = Marshal.AllocHGlobal(size);
        //    IntPtr r3 = Marshal.AllocHGlobal(size);
        //    IntPtr r4 = Marshal.AllocHGlobal(size);
        //    Console.WriteLine(stopwatch.ElapsedMilliseconds);
        //    for(int y = 0; y < Y; y++) {
        //        for(int x = 0; x < X; x++) {
        //            float tx = x + .5f;
        //            float ty = (Height - y) + .5f;
        //            Vector<short> col = GetColorFromPos(new Vector<float>(new float[8] { tx, ty, tx, ty, tx, ty, tx, ty, }),
        //                new Vector<double>(new double[4] { vals[2], vals[3], vals[4], vals[5] }));
        //            Marshal.Copy(new byte[4] { (byte)col[0], (byte)col[1], (byte)col[2], (byte)col[3] }, 0,
        //                IntPtr.Add(r1, (x + X * y) * 4), 4);
        //            Marshal.Copy(new byte[4] { (byte)col[4], (byte)col[5], (byte)col[6], (byte)col[7] }, 0,
        //                IntPtr.Add(r2, (x + X * y) * 4), 4);
        //            Marshal.Copy(new byte[4] { (byte)col[8], (byte)col[9], (byte)col[10], (byte)col[11] }, 0,
        //                IntPtr.Add(r3, (x + X * y) * 4), 4);
        //            Marshal.Copy(new byte[4] { (byte)col[12], (byte)col[13], (byte)col[14], (byte)col[15] }, 0,
        //                IntPtr.Add(r4, (x + X * y) * 4), 4);
        //        }
        //    }
        //    byte[][] result = new byte[4][];
        //    result[0] = new byte[size];
        //    result[1] = new byte[size];
        //    result[2] = new byte[size];
        //    result[3] = new byte[size];
        //    Marshal.Copy(r1, result[0], 0, size);
        //    Marshal.Copy(r2, result[1], 0, size);
        //    Marshal.Copy(r3, result[2], 0, size);
        //    Marshal.Copy(r4, result[3], 0, size);
        //    Marshal.FreeHGlobal(r1);
        //    Marshal.FreeHGlobal(r2);
        //    Marshal.FreeHGlobal(r3);
        //    Marshal.FreeHGlobal(r4);
        //    stopwatch.Stop();
        //    Interlocked.Add(ref frames, 4);
        //    Interlocked.Add(ref mspf, (int)stopwatch.ElapsedMilliseconds);
        //    Console.WriteLine("4 Frames Rendered in {0}ms", stopwatch.ElapsedMilliseconds);
        //    return result;
        //}

        //struct Vector3x4 {
        //    public Vector<double> X, Y, Z;

        //    public Vector3x4(double[] X, double[] Y, double[] Z) {
        //        this.X = new Vector<double>(X);
        //        this.Y = new Vector<double>(Y);
        //        this.Z = new Vector<double>(Z);
        //    }

        //    public Vector3x4(Vector<double> X, Vector<double> Y, Vector<double> Z) {
        //        this.X = X;
        //        this.Y = Y;
        //        this.Z = Z;
        //    }

        //    public Vector3x4(double[] a, double[] b, double[] c, double[] d) {
        //        this.X = new Vector<double>(new double[4] { a[0], b[0], c[0], d[0] });
        //        this.Y = new Vector<double>(new double[4] { a[1], b[1], c[1], d[1] });
        //        this.Z = new Vector<double>(new double[4] { a[2], b[2], c[2], d[2] });
        //    }

        //    public Vector3x4(double[] a) {
        //        this.X = new Vector<double>(new double[4] { a[0], a[0], a[0], a[0] });
        //        this.Y = new Vector<double>(new double[4] { a[1], a[1], a[1], a[1] });
        //        this.Z = new Vector<double>(new double[4] { a[2], a[2], a[2], a[2] });
        //    }
        //}

        //Vector<short> GetColorFromPos(Vector<float> pos, Vector<double> iTimes) {
        //    Vector<float> uvs = Vector.Divide(Vector.Subtract(pos, Vector.Multiply(.5f,
        //        new Vector<float>(new float[8] { Width, Height, Width, Height, Width, Height, Width, Height }))),
        //        new Vector<float>(new float[8] { Height, Height, Height, Height, Height, Height, Height, Height }));
        //    Vector3x4 col = new Vector3x4(new double[3] { 0.01f, 0.01f, 0.01f });
        //    Vector3x4 ro = new Vector3x4(new double[3] { 0f, 1f, 0f });
        //    Vector3x4 rd = Normalize(new Vector3x4(new double[3] { uvs[0], uvs[1], 1f }, new double[3] { uvs[2], uvs[3], 1f },
        //        new double[3] { uvs[4], uvs[5], 1f }, new double[3] { uvs[6], uvs[7], 1f }));
        //    Vector<double> t = RayMarch(ro, rd);
        //    if(Vector.LessThanAny(t, MAX_DIST)) {
        //        Vector3x4 p = Add(ro, Multiply(rd, t));
        //        Vector3x4 lightPos = new Vector3x4(new double[3] { 0, 5, 6 });
        //        Vector3x4 lightColor = new Vector3x4(new double[3] { 0, 0, 1 });
        //        lightPos.X = Vector.Add(lightPos.X, new Vector<double>(new double[4] { Complex.Sin(iTimes[0]).Real,
        //            Complex.Sin(iTimes[1]).Real, Complex.Sin(iTimes[2]).Real, Complex.Sin(iTimes[3]).Real }));
        //        lightPos.Z = Vector.Add(lightPos.Z, new Vector<double>(new double[4] { Complex.Multiply(Complex.Cos(iTimes[0]), 2f).Real,
        //            Complex.Multiply(Complex.Cos(iTimes[1]), 2f).Real, Complex.Multiply(Complex.Cos(iTimes[2]), 2f).Real,
        //            Complex.Multiply(Complex.Cos(iTimes[3]), 2f).Real }));
        //        Vector3x4 l = Normalize(Sub(lightPos, p));
        //        Vector3x4 n = GetNormal(p);
        //        Vector<double> dif = Vector.Max(Vector.Min(Dot(n, l), Vector<double>.Zero), Vector<double>.One);
        //        Vector<double> d = RayMarch(Add(p, Multiply(n, SURF_DIST)), l);
        //        dif = Vector.ConditionalSelect(Vector.LessThan(d, Length(Sub(lightPos, p))), Vector.Multiply(dif, .1f), dif);
        //        col = Add(col, Multiply(lightColor, dif));
        //    }
        //    col = Multiply(col, new Vector<double>(255));
        //    Vector<short> fragCol = new Vector<short>(new short[16] { (short)col.X[0], (short)col.Y[0], (short)col.Z[0], 255,
        //        (short)col.X[1], (short)col.Y[1], (short)col.Z[1], 255, (short)col.X[2], (short)col.Y[2], (short)col.Z[2], 255,
        //        (short)col.X[3], (short)col.Y[3], (short)col.Z[3], 255 });
        //    return Vector.Max(Vector.Min(fragCol, Vector<short>.Zero), new Vector<short>(255));
        //}

        //Vector3x4 Normalize(Vector3x4 vector) {
        //    Vector<double> distance = Length(vector);
        //    return new Vector3x4(Vector.Divide(vector.X, distance), Vector.Divide(vector.Y, distance),
        //        Vector.Divide(vector.Z, distance));
        //}

        //Vector<double> Length(Vector3x4 vector) {
        //    Vector3x4 square = new Vector3x4(Vector.Multiply(vector.X, vector.X), Vector.Multiply(vector.Y, vector.Y),
        //        Vector.Multiply(vector.Z, vector.Z));
        //    return new Vector<double>(new double[4] { Complex.Sqrt(square.X[0]+square.Y[0]+square.Z[0]).Real,
        //        Complex.Sqrt(square.X[1] + square.Y[1] + square.Z[1]).Real, Complex.Sqrt(square.X[2] + square.Y[2] + square.Z[2]).Real,
        //        Complex.Sqrt(square.X[3] + square.Y[3] + square.Z[3]).Real });
        //}

        //Vector3x4 Add(Vector3x4 a, Vector3x4 b) {
        //    return new Vector3x4(Vector.Add(a.X, b.X), Vector.Add(a.Y, b.Y), Vector.Add(a.Z, b.Z));
        //}

        //Vector3x4 Sub(Vector3x4 a, Vector<double> b) {
        //    return new Vector3x4(Vector.Subtract(a.X, b), Vector.Subtract(a.Y, b), Vector.Subtract(a.Z, b));
        //}

        //Vector3x4 Sub(Vector3x4 a, Vector3x4 b) {
        //    return new Vector3x4(Vector.Subtract(a.X, b.X), Vector.Subtract(a.Y, b.Y), Vector.Subtract(a.Z, b.Z));
        //}

        //Vector3x4 Multiply(Vector3x4 a, Vector<double> b) {
        //    return new Vector3x4(Vector.Multiply(a.X, b), Vector.Multiply(a.Y, b), Vector.Multiply(a.Z, b));
        //}

        //Vector<double> Dot(Vector3x4 a, Vector3x4 b) {
        //    return new Vector<double>(new double[4] {
        //        (Complex.Multiply(a.X[0], b.X[0]) + Complex.Multiply(a.Y[0], b.Y[0]) + Complex.Multiply(a.Z[0], b.Z[0])).Real,
        //        (Complex.Multiply(a.X[1], b.X[1]) + Complex.Multiply(a.Y[1], b.Y[1]) + Complex.Multiply(a.Z[1], b.Z[1])).Real,
        //        (Complex.Multiply(a.X[2], b.X[2]) + Complex.Multiply(a.Y[2], b.Y[2]) + Complex.Multiply(a.Z[2], b.Z[2])).Real,
        //        (Complex.Multiply(a.X[3], b.X[3]) + Complex.Multiply(a.Y[3], b.Y[3]) + Complex.Multiply(a.Z[3], b.Z[3])).Real
        //    });
        //}

        //Vector<double> RayMarch(Vector3x4 ro, Vector3x4 rd) {
        //    Vector<double> dO = new Vector<double>(.0);
        //    Vector<long> fin = new Vector<long>();
        //    for(int i = 0; i < MAX_STEPS; i++) {
        //        Vector3x4 p = Add(ro, Multiply(rd, dO));
        //        Vector<double> dS = GetDist(p);
        //        dO = Vector.ConditionalSelect(fin, dO, Vector.Add(dO, dS));
        //        fin = Vector.BitwiseOr(Vector.GreaterThan(dO, MAX_DIST), Vector.LessThan(dS, SURF_DIST));
        //        if(fin.Equals(new Vector<long>(1)))
        //            break;
        //    }
        //    return dO;
        //}

        //Vector<double> GetDist(Vector3x4 p) {
        //    Vector<double> s = new Vector<double>(new double[4] { 0, 1, 6, 1 });
        //    Vector<double> sD = Vector.Subtract(Length(Sub(p, new Vector<double>(new double[4] { s[0], s[1], s[2], 0 }))),
        //        new Vector<double>(s[3]));
        //    Vector<double> fD = p.Y;
        //    Vector<double> d = Vector.Min(sD, fD);
        //    return d;
        //}

        //Vector3x4 GetNormal(Vector3x4 p) {
        //    Vector<double> d = GetDist(p);
        //    Vector2 e = new Vector2(.01f, 0f);
        //    Vector3x4 n = new Vector3x4(
        //        Vector.Subtract(d, GetDist(Sub(p, new Vector3x4(new double[3] { e.X, e.Y, e.Y })))),
        //        Vector.Subtract(d, GetDist(Sub(p, new Vector3x4(new double[3] { e.Y, e.X, e.Y })))),
        //        Vector.Subtract(d, GetDist(Sub(p, new Vector3x4(new double[3] { e.Y, e.Y, e.X }))))
        //        );
        //    return Normalize(n);
        //}

        Vector4 getColorFromPos(Vector2 pos, float iTime) {
            Vector2 uv = Vector2.Divide(Vector2.Subtract(pos, Vector2.Multiply(.5f, new Vector2(Width, Height))), Height);
            Vector3 col = new Vector3(0.01f);
            Vector3 ro = new Vector3(0f, 1f, 0f);
            Vector3 rd = Vector3.Normalize(new Vector3(uv, 1f));
            float t = RayMarch(ro, rd);
            if(t < 100) {
                Vector3 p = Vector3.Add(ro, Vector3.Multiply(rd, t));
                Vector3 lightPos = new Vector3(0, 5, 6);
                Vector3 lightColor = new Vector3(0, 0, 1f);
                lightPos.X += (float)Complex.Sin(iTime).Real;
                lightPos.Z += (float)Complex.Multiply(Complex.Cos(iTime), 2f).Real;
                Vector3 l = Vector3.Normalize(Vector3.Subtract(lightPos, p));
                Vector3 n = GetNormal(p);
                float dif = Clamp(Vector3.Dot(n, l), 0f, 1f);
                float d = RayMarch(p + n * 0.01f, l);
                if(d < Vector3.Distance(lightPos, p))
                    dif *= .1f;
                col += dif * lightColor;
            }
            Vector4 fragCol = new Vector4(Vector3.Multiply(255f, col), 255f);
            return Vector4.Clamp(fragCol, Vector4.Zero, new Vector4(255f));
        }

        float Clamp(float n, float min, float max) {
            if(n < min)
                return min;
            else if(n > max)
                return max;
            return n;
        }

        float RayMarch(Vector3 ro, Vector3 rd) {
            float dO = .0f;
            for(int i = 0; i < MAX_STEPS; i++) {
                Vector3 p = Vector3.Add(ro, Vector3.Multiply(rd, dO));
                float dS = GetDist(p);
                dO += dS;
                if(dO > 100 || dS < 0.01)
                    break;
            }
            return dO;
        }

        float GetDist(Vector3 p) {
            Vector4 s = new Vector4(0, 1, 6, 1);
            float sD = Vector3.Distance(p, new Vector3(s.X, s.Y, s.Z)) - s.W;
            float fD = p.Y;
            float d = Math.Min(sD, fD);
            return d;
        }

        Vector3 GetNormal(Vector3 p) {
            float d = GetDist(p);
            Vector2 e = new Vector2(.01f, 0f);
            Vector3 n = new Vector3(
                d - GetDist(Vector3.Subtract(p, new Vector3(e.X, e.Y, e.Y))),
                d - GetDist(Vector3.Subtract(p, new Vector3(e.Y, e.X, e.Y))),
                d - GetDist(Vector3.Subtract(p, new Vector3(e.Y, e.Y, e.X)))
                );
            return Vector3.Normalize(n);
        }

        float SoftShadow(Vector3 ro, Vector3 rd, float mint, float tmax) {
            float res = 1.0f;
            float t = mint;
            float ph = 1e10f;

            for(int i = 0; i < 32; i++) {
                float h = GetDist(ro + rd * t);
                float y = h * h / (2.0f * ph);
                float d = (float)Math.Sqrt(h * h - y * y);
                res = Math.Min(res, 10.0f * d / Math.Max(0.0f, t - y));
                ph = h;
                t += h;
                if(res < 0.0001 || t > tmax)
                    break;
            }
            return Math.Min(Math.Max(res, 0.0f), 1.0f);
        }
    }
}
