using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        private delegate void SetImageDelegate(Bitmap img);
        private delegate void SetTextDelegate(string str);
        private delegate void UpdateDelegate();
        private Vector<double> MAX_DIST = new Vector<double>(100), SURF_DIST = new Vector<double>(0.01f);
        private const int MAX_STEPS = 100;
        private PictureBox screen;
        private long tf, mspf, toProcess;
        private bool running, checkerboard;
        private readonly long nspt;
        private readonly int MAX_THREADS;
        private Thread renderThread;
        private ConcurrentDictionary<Vector2, Vector3> rayDirections;

        public Display() {
            MAX_THREADS = Environment.ProcessorCount - 2;
            nspt = 1000000000 / Stopwatch.Frequency;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            InitalizeComponent();
            renderThread = new Thread(RenderLoop);
            renderThread.Start();
        }

        private void InitalizeComponent() {
            this.Width = 640;
            this.Height = 480;
            this.ClientSize = new System.Drawing.Size(Width, Height);
            this.Text = "PT2";
            this.Visible = true;
            this.CenterToScreen();
            screen = new PictureBox();
            screen.Size = this.ClientSize;
            screen.Image = new Bitmap(Width, Height);
            this.Controls.Add(screen);
            this.FormClosed += new FormClosedEventHandler(Form1_FormClosed);
            tf = mspf = 0;
            rayDirections = new ConcurrentDictionary<Vector2, Vector3>();
            Parallel.For(0, Height, y => {
                for(int x = 0; x < Width; x++) {
                    Vector2 pos = new Vector2(x + 0.5f, Height - y + 0.5f);
                    rayDirections.TryAdd(pos, Vector3.Normalize(new Vector3(Vector2.Divide(Vector2.Subtract(pos,
                        Vector2.Multiply(.5f, new Vector2(Width, Height))), Height), 1f)));
                }
            });
            checkerboard = false;
        }

        void Form1_FormClosed(object sender, FormClosedEventArgs e) {
            Environment.Exit(0);
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
                    SetText(String.Format("Avg MSPF: {0} FPS: {1}", mspf / tf, frames));
                    frames = 0;
                }
            }
        }

        private void Tick() {
            
        }

        private void Render(float iTime) {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Bitmap output = new Bitmap(screen.Image);
            var rect = new Rectangle(0, 0, Width, Height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var ptr = bmpData.Scan0;
            int AB = checkerboard ? 1 : 0;
            Parallel.For(0, Width/2, tx => {
                int x = tx * 2 + AB;
                Vector3 col = getColorFromPos(new Vector2(x + .5f, Height + .5f), iTime);
                for(int y = 1; y < Height; y += 2) {
                    Marshal.Copy(new byte[4] { (byte)col.X, (byte)col.Y, (byte)col.Z, 255 }, 0,
                        ptr + ((x + Width * (y - 1)) * 4), 4);
                    Vector3 newCol = getColorFromPos(new Vector2(x + .5f, Height - (y + 1) + .5f), iTime);
                    Vector3 mix = Lerp(col, newCol, 0.5f);
                    Marshal.Copy(new byte[4] { (byte)mix.X, (byte)mix.Y, (byte)mix.Z, 255 }, 0, ptr + ((x + Width * y) * 4), 4); //Lerping
                    col = newCol;
                    //Marshal.Copy(new byte[4] { (byte)((col.X + newCol.X) / 2), (byte)((col.Y + newCol.Y) / 2),
                    //    (byte)((col.Z + newCol.Z) / 2), (byte)255 }, 0, ptr + ((x + Width * y) * 4), 4); Averaging
                    //Buffer.BlockCopy(new byte[4] { (byte)col.X, (byte)col.Y, (byte)col.Z, (byte)col.W }, 0, frame,
                    //    (x + Width * y) * 4, 4); BlockCopy instead or Marshal.Copy
                }
            });
            checkerboard = !checkerboard;
            output.UnlockBits(bmpData);
            SetImage(output);
            UpdateScreen();
            output = null;
            stopwatch.Stop();
            tf++;
            mspf += stopwatch.ElapsedMilliseconds;
            Console.WriteLine("Frame Rendered in {0}ms", stopwatch.ElapsedMilliseconds);
        }

        Vector3 Lerp(Vector3 a, Vector3 b, float t) {
            return new Vector3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        private void SetImage(Bitmap img) {
            if(screen.InvokeRequired) {
                var invoke = new SetImageDelegate(SetImage);
                screen.Invoke(invoke, new object[] { img });
            } else
                screen.Image = img;
        }

        private void UpdateScreen() {
            if(screen.InvokeRequired) {
                var invoke = new UpdateDelegate(UpdateScreen);
                screen.Invoke(invoke);
            } else
                screen.Update();
        }

        private void SetText(string str) {
            if(this.InvokeRequired) {
                var invoke = new SetTextDelegate(SetText);
                this.Invoke(invoke, new object[] { str });
            } else
                this.Text = str;
        }

        Vector3 getColorFromPos(Vector2 pos, float iTime) {
            Vector3 col = new Vector3(0.01f);
            Vector3 ro = new Vector3(0f, 1f, 0f);
            if(!rayDirections.TryGetValue(pos, out Vector3 rd))
                throw new ArgumentNullException(String.Format("Unable to get Ray Direction for x:{0} y:{1}", pos.X - .5f, pos.Y - .5f));
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
            return Vector3.Clamp(Vector3.Multiply(255f, col), Vector3.Zero, new Vector3(255f));
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
    }
}
