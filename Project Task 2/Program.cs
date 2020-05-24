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
using System.Threading.Tasks.Dataflow;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Project_Task_2 {
    class Program {
        [MTAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Display form = new Display();
            Application.Run(form);
        }
    }

    class Display : Form {
        private const float MAX_STEPS = 100, MAX_DIST = 100.0f, SURF_DIST = .01f;
        private PictureBox screen;
        private Timer graphicsTimer;
        private ConcurrentQueue<MemoryStream> queue;
        private int iTime, frames, mspf;

        public Display() {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            InitalizeComponent();
            var task = new Task(Render, null);
            task.Start();
            task.Wait();
            task.Dispose();
            graphicsTimer.Start();
        }

        private void InitalizeComponent() {
            this.Width = 640;
            this.Height = 480;
            this.ClientSize = new System.Drawing.Size(Width, Height);
            this.Text = "PT2";
            this.Visible = true;
            screen = new PictureBox();
            screen.Size = this.ClientSize;
            this.Controls.Add(screen);
            queue = new ConcurrentQueue<MemoryStream>();
            graphicsTimer = new Timer();
            graphicsTimer.Interval = 1000 / 20;
            graphicsTimer.Tick += (object sender, EventArgs e) => {
                if(!ThreadPool.QueueUserWorkItem(Render, iTime += graphicsTimer.Interval))
                    Console.Error.WriteLine("Failed to start next render");
                if(queue.TryDequeue(out MemoryStream stream)) {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    screen.Image = ImageFromRawARGBStream(stream, Width, Height);
                    stream.Dispose();
                    stream = null;
                    frames++;
                    stopwatch.Stop();
                    mspf += (int)stopwatch.ElapsedMilliseconds;
                    this.Text = String.Format("Avg MSPF: {0} FPS: {1}", mspf / frames, frames / (mspf / frames));
                }
            };
            iTime = mspf = frames = 0;
        }

        public static Image ImageFromRawARGBStream(MemoryStream stream, int width, int height) {
            var output = new Bitmap(width, height);
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var ptr = bmpData.Scan0;
            Marshal.Copy(stream.ToArray(), 0, ptr, (int)stream.Length);
            output.UnlockBits(bmpData);
            return output;
        }

        private void Render(object state) {
            int iTime = 0;
            if(state != null)
                iTime = (int)state;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            queue.Enqueue(getColorsFromRegion(new Vector3(Width, Height, iTime/1000f)));
            stopwatch.Stop();
            Console.WriteLine("Frame Rendered in {0}ms", stopwatch.ElapsedMilliseconds);
        }

        MemoryStream getColorsFromRegion(Vector3 region) {
            int X = (int)region.X;
            int Y = (int)region.Y;
            float iTime = region.Z;
            MemoryStream stream = new MemoryStream(X * Y * 4);
            for(int y = 0; y < Y; y++)
                for(int x = 0; x < X; x++) {
                    Vector4 col = getColorFromPos(new Vector2(x + .5f, Height - y + .5f), iTime);
                    stream.Write(new byte[4] { (byte)col.X, (byte)col.Y, (byte)col.Z, (byte)col.W }, 0, 4);
                }
            return stream;
        }

        Vector4 getColorFromPos(Vector2 pos, float iTime) {
            Vector2 uv = Vector2.Divide(Vector2.Subtract(pos, Vector2.Multiply(.5f, new Vector2(Width, Height))), Height);
            Vector3 col = new Vector3(0.01f);
            Vector3 ro = new Vector3(0f, 1f, 0f);
            Vector3 rd = Vector3.Normalize(new Vector3(uv, 1f));
            float t = RayMarch(ro, rd);
            if(t < MAX_DIST) {
                Vector3 p = Vector3.Add(ro, Vector3.Multiply(rd, t));
                Vector3 lightPos = new Vector3(0, 5, 6);
                Vector3 lightColor = new Vector3(0, 0, 1f);
                lightPos.X += (float)Math.Sin(iTime);
                lightPos.Z += (float)Math.Cos(iTime) * 2f;
                Vector3 l = Vector3.Normalize(Vector3.Subtract(lightPos, p));
                Vector3 n = GetNormal(p);
                float dif = Clamp(Vector3.Dot(n, l), 0f, 1f);
                float d = RayMarch(p + n * SURF_DIST, l);
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
                if(dO > MAX_DIST || dS < SURF_DIST)
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
            float ph = 1e10f; // big, such that y = 0 on the first iteration

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
