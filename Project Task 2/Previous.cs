//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Drawing;
//using System.Drawing.Imaging;
//using System.Linq;
//using System.Numerics;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Threading.Tasks.Dataflow;
//using System.Windows.Forms;
//using Timer = System.Windows.Forms.Timer;

//namespace Project_Task_2 {
//    class Previous {
//        [MTAThread]
//        static void Main() {
//            Application.EnableVisualStyles();
//            Application.SetCompatibleTextRenderingDefault(false);
//            Form1 form = new Form1();
//            Application.Run(form);
//        }
//    }
//    partial class Form1 : Form {
//        private const float MAX_STEPS = 100, MAX_DIST = 100.0f, SURF_DIST = .01f;
//        private readonly float MAX_THREADS;
//        Bitmap _screen;
//        private static Stopwatch time;
//        private static long totMSPF, frames, last;
//        private static Timer graphicsTimer;
//        private static int toProcess;
//        private static ConcurrentQueue<byte[]> queue;

//        public Form1() {
//            MAX_THREADS = 2;
//            InitalizeComponent();
//            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
//            this.Paint += Form1_Paint;
//            new Task(Render).Start();
//            graphicsTimer = new Timer();
//            graphicsTimer.Interval = 1000 / (int)MAX_THREADS;
//            graphicsTimer.Tick += GraphicsTimer_Tick;
//            time.Start();
//            graphicsTimer.Start();
//        }

//        private void GraphicsTimer_Tick(object sender, EventArgs e) {
//            Invalidate();
//        }

//        private void InitalizeComponent() {
//            this.ClientSize = new System.Drawing.Size(640, 480);
//            this.Text = "PT2";
//            this.Visible = true;
//            last = 0;
//            totMSPF = 0;
//            frames = 0;
//            time = new Stopwatch();
//            _screen = new Bitmap(Width, Height);
//            queue = new ConcurrentQueue<byte[]>();
//        }

//        private void Form1_Paint(object sender, PaintEventArgs e) {
//            new Task(Render).Start();
//            if(queue.TryDequeue(out byte[] result)) {
//                Graphics g = e.Graphics;
//                BitmapData _bitmap = _screen.LockBits(ClientRectangle, System.Drawing.Imaging.ImageLockMode.ReadWrite,
//                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
//                IntPtr ptr = _bitmap.Scan0;
//                System.Runtime.InteropServices.Marshal.Copy(result, 0, ptr, result.Length);
//                _screen.UnlockBits(_bitmap);
//                g.DrawImageUnscaled(_screen, 0, 0);
//                this.Text = string.Format("Avg MSPF: {0} FPS: {1}", totMSPF / frames, Math.Round(1000f / (totMSPF / frames), 2));
//            }
//        }

//        private void Render() {
//            last = time.ElapsedMilliseconds;
//            var tasks = new TransformBlock<Vector4, byte[]>(x => getColorsFromRegion(x),
//                new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = (int)MAX_THREADS });
//            toProcess = (int)MAX_THREADS;
//            float iTime = time.ElapsedMilliseconds / 1000f;
//            int colHeight = (int)(Height / MAX_THREADS);
//            for(int i = 0; i < MAX_THREADS; i++)
//                tasks.Post(new Vector4(Width, colHeight, i, iTime));
//            tasks.Complete();
//            while(toProcess > 0)
//                ;
//            Console.WriteLine("Done");
//            IList<byte[]> colors;
//            if(tasks.TryReceiveAll(out colors)) {
//                List<byte> emptyProduct = new List<byte>();
//                colors.ToList().ForEach(x => emptyProduct = emptyProduct.Concat(x).ToList());
//                queue.Enqueue(emptyProduct.ToArray());
//                colors = null;
//                emptyProduct = null;
//            } else
//                throw new TaskCanceledException("Unable to Recieve any Results");
//            frames++;
//            totMSPF += time.ElapsedMilliseconds - last;
//        }

//        byte[] getColorsFromRegion(Vector4 region) {
//            int X = (int)region.X;
//            int Y = (int)region.Y;
//            int Z = (int)region.Z;
//            float iTime = region.W;
//            byte[] colors = new byte[X * Y * 4];
//            int yz = Y * Z;
//            for(int y = 0; y < Y; y++)
//                for(int x = 0; x < X; x++) {
//                    Vector4 col = getColorFromPos(new Vector2(x + .5f, Height - (y + yz) + .5f), iTime);
//                    colors[(x + X * y) * 4 + 3] = (byte)col.W;
//                    colors[(x + X * y) * 4 + 0] = (byte)col.X;
//                    colors[(x + X * y) * 4 + 1] = (byte)col.Y;
//                    colors[(x + X * y) * 4 + 2] = (byte)col.Z;
//                }
//            Interlocked.Decrement(ref toProcess);
//            return colors;
//        }

//        Vector4 getColorFromPos(Vector2 pos, float iTime) {
//            Vector2 uv = Vector2.Divide(Vector2.Subtract(pos, Vector2.Multiply(.5f, new Vector2(Width, Height))), Height);
//            Vector3 col = new Vector3(0.01f);
//            Vector3 ro = new Vector3(0f, 1f, 0f);
//            Vector3 rd = Vector3.Normalize(new Vector3(uv, 1f));
//            float t = RayMarch(ro, rd);
//            if(t < MAX_DIST) {
//                Vector3 p = Vector3.Add(ro, Vector3.Multiply(rd, t));
//                Vector3 lightPos = new Vector3(0, 5, 6);
//                Vector3 lightColor = new Vector3(0, 0, 1f);
//                lightPos.X += (float)Math.Sin(iTime);
//                lightPos.Z += (float)Math.Cos(iTime) * 2f;
//                Vector3 l = Vector3.Normalize(Vector3.Subtract(lightPos, p));
//                Vector3 n = GetNormal(p);
//                float dif = Clamp(Vector3.Dot(n, l), 0f, 1f);
//                float d = RayMarch(p + n * SURF_DIST, l);
//                if(d < Vector3.Distance(lightPos, p))
//                    dif *= .1f;
//                col += dif * lightColor;
//            }
//            Vector4 fragCol = new Vector4(Vector3.Multiply(255f, col), 255f);
//            return Vector4.Clamp(fragCol, Vector4.Zero, new Vector4(255f));
//        }

//        float Clamp(float n, float min, float max) {
//            if(n < min)
//                return min;
//            else if(n > max)
//                return max;
//            return n;
//        }

//        float RayMarch(Vector3 ro, Vector3 rd) {
//            float dO = .0f;
//            for(int i = 0; i < MAX_STEPS; i++) {
//                Vector3 p = Vector3.Add(ro, Vector3.Multiply(rd, dO));
//                float dS = GetDist(p);
//                dO += dS;
//                if(dO > MAX_DIST || dS < SURF_DIST)
//                    break;
//            }
//            return dO;
//        }

//        float GetDist(Vector3 p) {
//            Vector4 s = new Vector4(0, 1, 6, 1);
//            float sD = Vector3.Distance(p, new Vector3(s.X, s.Y, s.Z)) - s.W;
//            float fD = p.Y;
//            float d = Math.Min(sD, fD);
//            return d;
//        }

//        Vector3 GetNormal(Vector3 p) {
//            float d = GetDist(p);
//            Vector2 e = new Vector2(.01f, 0f);
//            Vector3 n = new Vector3(
//                d - GetDist(Vector3.Subtract(p, new Vector3(e.X, e.Y, e.Y))),
//                d - GetDist(Vector3.Subtract(p, new Vector3(e.Y, e.X, e.Y))),
//                d - GetDist(Vector3.Subtract(p, new Vector3(e.Y, e.Y, e.X)))
//                );
//            return Vector3.Normalize(n);
//        }

//        float SoftShadow(Vector3 ro, Vector3 rd, float mint, float tmax) {
//            float res = 1.0f;
//            float t = mint;
//            float ph = 1e10f; // big, such that y = 0 on the first iteration

//            for(int i = 0; i < 32; i++) {
//                float h = GetDist(ro + rd * t);
//                float y = h * h / (2.0f * ph);
//                float d = (float)Math.Sqrt(h * h - y * y);
//                res = Math.Min(res, 10.0f * d / Math.Max(0.0f, t - y));
//                ph = h;
//                t += h;
//                if(res < 0.0001 || t > tmax)
//                    break;
//            }
//            return Math.Min(Math.Max(res, 0.0f), 1.0f);
//        }

//        public static IEnumerable<IEnumerable<T>> Combine<T>(IEnumerable<IEnumerable<T>> sequences) {
//            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
//            return sequences.Aggregate(
//                emptyProduct,
//                (accumulator, sequence) =>
//                    from accseq in accumulator
//                    from item in sequence
//                    select accseq.Concat(new[] { item }));
//        }

//        protected override void Dispose(bool disposing) {
//            base.Dispose(disposing);
//        }
//    }
//}