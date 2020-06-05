using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections.Concurrent;
using GLS.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Collections.ObjectModel;

namespace PT2Intrinsics {
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
        private const int MAX_STEPS = 100;
        private PictureBox screen;
        private long tf, mspf;
        private bool running, interlaceAB;
        private readonly long nspt;
        private Thread renderThread;
        private Queue<Keys> keyQueue;
        private Object[] objects;
        private Light[] lights;
        private Vector3 CameraPos;

        struct Light {
            public Vector3 XYZ;
            public Vector3 RGB;
            public float lightIntensity;
            public Light(Vector3 _XYZ, Vector3 _RGB) {
                XYZ = _XYZ;
                RGB = _RGB;
                lightIntensity = 1f;
            }
            public Light(Vector3 _XYZ, Vector3 _RGB, float _li) {
                XYZ = _XYZ;
                RGB = _RGB;
                lightIntensity = _li;
            }
        }

        struct Object {
            public enum ObjectType {
                Sphere = 0,
                Box = 1
            };
            public Vector3 XYZ;
            public Vector3 SXYZ;
            public Vector3 RGB;
            public ObjectType type;
            public Object(Vector3 _XYZ, Vector3 _SXYZ, Vector3 _RGB, ObjectType _type) {
                XYZ = _XYZ;
                SXYZ = _SXYZ;
                RGB = _RGB;
                type = _type;
            }
            public float SDF(Vector3 pos) {
                switch(type) {
                    case 0:
                        return Vector3.Distance(pos, XYZ) - SXYZ.X;
                    default:
                        return 0;
                }
            }
        }

        public Display() {
            nspt = 1000000000 / Stopwatch.Frequency;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            InitalizeComponent();
            renderThread = new Thread(RenderLoop);
            renderThread.Start();
        }

        private void InitalizeComponent() {
            //Initalise Variables
            this.Width = 640;
            this.Height = 480;
            this.Size = new System.Drawing.Size(Width, Height);
            this.Text = "PT2";
            this.Visible = true;
            this.CenterToScreen();
            screen = new PictureBox();
            screen.Size = this.Size;
            screen.Image = new Bitmap(Width, Height);
            this.Controls.Add(screen);
            this.FormClosed += new FormClosedEventHandler(Form1_FormClosed);
            this.KeyDown += Form1_KeyDown;
            keyQueue = new Queue<Keys>();
            tf = mspf = 0;
            interlaceAB = false;
            CameraPos = new Vector3(0, 1, 0);

            //Initalize Objects
            objects = new Object[]{ new Object(new Vector3(0, 1, 6), new Vector3(1), new Vector3(1, 0, 0), Object.ObjectType.Sphere) };

            //Initalize Lights
            lights = new Light[]{ new Light(new Vector3(0, 5, 6), new Vector3(1)) };
        }

        void Form1_FormClosed(object sender, FormClosedEventArgs e) {
            Environment.Exit(0);
        }

        void Form1_KeyDown(object sender, KeyEventArgs e) {
            keyQueue.Enqueue(e.KeyCode);
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
                    RenderNormal(time.ElapsedMilliseconds / 1000f);
                frames++;
                if(time.ElapsedMilliseconds - timer > 1000) {
                    timer += 1000;
                    Console.Title = String.Format("Avg MSPF: {0} FPS: {1}", mspf / tf, frames);
                    frames = 0;
                }
            }
        }

        private void Tick() {
            if(keyQueue.TryDequeue(out Keys currentKey))
                switch(currentKey) {
                    case Keys.Escape:
                        InvokeClose();
                        break;
                    case Keys.Left:
                        CameraPos.X-=0.1f;
                        break;
                    case Keys.Right:
                        CameraPos.X+=0.1f;
                        break;
                }
        }

        private void InvokeClose() {
            if(this.InvokeRequired)
                this.Invoke(new UpdateDelegate(InvokeClose));
            else
                this.Close();
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

        #region Normal

        private void RenderNormal(float iTime) {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Bitmap output = new Bitmap(screen.Image);
            var rect = new Rectangle(0, 0, Width, Height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var ptr = bmpData.Scan0;
            int AB = interlaceAB ? 1 : 0;
            Parallel.For(0, Height / 2, ty => {
                int y = ty * 2 + AB;
                Vector3 newCol, mix;
                Vector3 col = getColorFromPos(new Vector2(Width + .5f, y + .5f), iTime);
                for(int x = 1; x < Width; x += 2) {
                    Marshal.Copy(new byte[4] { (byte)col.X, (byte)col.Y, (byte)col.Z, 255 }, 0,
                        ptr + (((x - 1) + Width * y) * 4), 4);
                    newCol = getColorFromPos(new Vector2((x + 1) + .5f, Height - y + .5f), iTime);
                    mix = Lerp(col, newCol, 0.5f);
                    Marshal.Copy(new byte[4] { (byte)mix.X, (byte)mix.Y, (byte)mix.Z, 255 }, 0, ptr + ((x + Width * y) * 4), 4); //Lerping
                    col = newCol;
                    //Marshal.Copy(new byte[4] { (byte)((col.X + newCol.X) / 2), (byte)((col.Y + newCol.Y) / 2),
                    //    (byte)((col.Z + newCol.Z) / 2), (byte)255 }, 0, ptr + ((x + Width * y) * 4), 4); Averaging
                    //Buffer.BlockCopy(new byte[4] { (byte)col.X, (byte)col.Y, (byte)col.Z, (byte)col.W }, 0, frame,
                    //    (x + Width * y) * 4, 4); BlockCopy instead or Marshal.Copy
                }
            });
            interlaceAB = !interlaceAB;
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

        Vector3 getColorFromPos(Vector2 pos, float iTime) {
            Vector3 col = new Vector3(0f); //Ambient Light
            Vector3 rd = Vector3.Normalize(new Vector3(Vector2.Divide(Vector2.Subtract(pos,
                Vector2.Multiply(.5f, new Vector2(Width, Height))), Height), 1f));
            float t = RayMarch(CameraPos, rd);
            if(t < 100) {
                Vector3 p = Vector3.Add(CameraPos, Vector3.Multiply(rd, t));
                foreach(Light light in lights) {
                    Vector3 l = Vector3.Normalize(Vector3.Subtract(light.XYZ, p));
                    Vector3 n = GetNormal(p);
                    float dif = Clamp(Vector3.Dot(n, l), 0f, 1f);
                    float d = RayMarch(p + n * 0.01f, l);
                    if(d < Vector3.Distance(light.XYZ, p))
                        dif *= 0.2f;
                    col += dif * light.RGB * GetSampleColor(p) * light.lightIntensity;
                }
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

        Vector3 GetSampleColor(Vector3 p) {
            Vector3 sC = new Vector3(0);
            float sD = 100;
            foreach(Object obj in objects) {
                float sdf = obj.SDF(p);
                sD = Math.Min(sD, sdf);
                if(sD == sdf)
                    sC = obj.RGB;
            }
            float fD = p.Y;
            float d = Math.Min(sD, fD);
            if(d == sD)
                return sC;
            return new Vector3(0f, 1f, 0f); //Floor Color
        }

        float GetDist(Vector3 p) {
            float sD = 100;
            foreach(Object obj in objects)
                sD = Math.Min(sD, obj.SDF(p));
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

        #endregion

        //IGNORE! No Performance Benifints
        #region Intrinsics

        private void RenderIntrinsics(float iTime) {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Bitmap output = new Bitmap(screen.Image);
            var rect = new Rectangle(0, 0, Width, Height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var ptr = bmpData.Scan0;
            int AB = interlaceAB ? 1 : 0;
            Parallel.For(0, Height / 2, ty => {
                int y = ty * 2 + AB;
                int x = 0;
                Vector256<ushort> newCol, mix;
                Vector256<ushort> col = GetColorFromPositions(Vector256.Create((float)x, (float)y, x + 2f, y, x + 4f, y, x + 6f, y),
                        new Vector512<float>(Vector256.Create(0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f)));
                for(x = 1; x < Width; x += 8) {
                    UnpackColorsFromVector(col, ptr, ((x-1) + Width * y) * 4, 8);
                    newCol = GetColorFromPositions(Vector256.Create((float)x + 1f, (float)y, x + 3f, y, x + 5f, y, x + 7f, y),
                        new Vector512<float>(Vector256.Create(0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f)));
                    mix = Avx2.Average(col, newCol);
                    UnpackColorsFromVector(mix, ptr, (x + Width * y) * 4, 8);
                    col = newCol;
                }
            });
            interlaceAB = !interlaceAB;
            output.UnlockBits(bmpData);
            SetImage(output);
            UpdateScreen();
            output = null;
            stopwatch.Stop();
            tf++;
            mspf += stopwatch.ElapsedMilliseconds;
            //Console.WriteLine("Frame Rendered in {0}ms", stopwatch.ElapsedMilliseconds);
        }

        private void UnpackColorsFromVector(Vector256<ushort> uvec, IntPtr ptr, int offset, int arroffset) {
            Vector256<byte> vec = uvec.AsByte();
            Marshal.Copy(new byte[4] { vec.GetElement(1), vec.GetElement(3), vec.GetElement(5), 255 },
                0, ptr + offset, 4);
            Marshal.Copy(new byte[4] { vec.GetElement(9), vec.GetElement(11), vec.GetElement(13), 255 },
                0, ptr + offset + arroffset, 4);
            Marshal.Copy(new byte[4] { vec.GetElement(17), vec.GetElement(19), vec.GetElement(21), 255 },
                0, ptr + offset + (arroffset * 2), 4);
            Marshal.Copy(new byte[4] { vec.GetElement(25), vec.GetElement(27), vec.GetElement(29), 255 },
                0, ptr + offset + (arroffset * 3), 4);
        }

        /*
         * Vector3 getColorFromPos(Vector2 pos, float iTime) {
            Vector3 col = new Vector3(0.01f);
            Vector3 ro = new Vector3(0f, 1f, 0f);
            Vector3 rd = Vector3.Normalize(new Vector3(Vector2.Divide(Vector2.Subtract(pos,
                Vector2.Multiply(.5f, new Vector2(Width, Height))), Height), 1f));
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
         */



        Vector256<ushort> GetColorFromPositions(Vector256<float> pos, Vector512<float> ro) {
            Vector256<ushort> col = Vector256.Create((ushort)(0));
            Vector256<float> uv;
            //0.5 * Resolution
            uv = Avx2.Multiply(Vector256.Create(0.5f),
                Vector256.Create((float)Width, (float)Height, (float)Width, (float)Height,
                (float)Width, (float)Height, (float)Width, (float)Height));
            //pos - (0.5 * Resolution)
            uv = Avx2.Subtract(pos, uv);
            //(pos - (0.5 * Resolution)) / Height
            uv = Avx2.Divide(uv, Vector256.Create((float)Height));
            //new Vector3(uv, 1f)
            Vector512<float> rd = new Vector512<float>(Vector256.Create(uv.GetElement(0), uv.GetElement(1), 1f, 0f,
                uv.GetElement(2), uv.GetElement(3), 1f, 0f), Vector256.Create(uv.GetElement(4), uv.GetElement(5), 1f, 0f,
                uv.GetElement(6), uv.GetElement(7), 1f, 0f));
            Vector256<double> t = RayMarch(ro, rd);
            Vector256<double> mask1 = Avx2.Compare(t, Vector256.Create(100d), FloatComparisonMode.OrderedLessThanNonSignaling);
            if(Avx2.MoveMask(mask1) > 0) {
                col = Vector256.Create((ushort)(64 << 8));
            }
            return col;
        }

        private Vector256<double> RayMarch(Vector512<float> ro, Vector512<float> rd) {
            Vector256<double> dO, dS, mask1, mask2, n, iterations, mask3, a, one;
            Vector512<float> p;
            float x, y, z, w;
            dO = Vector256.Create(.0);
            iterations = Vector256.Create(100.0);
            n = Vector256.Create(.0);
            one = Vector256.Create(1.0);
        repeat:
            x = (float)dO.GetElement(0); y = (float)dO.GetElement(1); z = (float)dO.GetElement(2); w = (float)dO.GetElement(3);
            p = new Vector512<float>(Vector256.Create(x, x, x, x, y, y, y, y), Vector256.Create(z, z, z, z, w, w, w, w));
            p = Avx5.Add(ro, Avx5.Multiply(rd, p));
            dS = GetDist(p);
            dO = Avx2.Add(dO, dS);
            mask1 = Avx2.Compare(dO, Vector256.Create(100d), FloatComparisonMode.OrderedLessThanOrEqualNonSignaling);
            mask2 = Avx2.Compare(dS, Vector256.Create(.01), FloatComparisonMode.OrderedGreaterThanOrEqualNonSignaling);
            mask3 = Avx2.Compare(n, iterations, FloatComparisonMode.OrderedLessThanNonSignaling);
            a = Avx2.Or(mask1, mask2);
            a = Avx2.And(a, mask3);
            n = Avx2.Add(n, Avx2.And(a, one));
            if(Avx2.MoveMask(a) > 0)
                goto repeat;
            return dO;
        }

        private Vector256<double> GetDist(Vector512<float> p) {
            Vector512<float> s = new Vector512<float>(Vector256.Create(0f, 1f, 6f, 0f, 0f, 1f, 6f, 0f));
            Vector256<double> sD = Avx2.Subtract(Avx5.Length(Avx5.Subtract(p, s)), Vector256.Create(0d));
            Vector256<double> fD = Vector256.Create(p.V1.GetLower().GetElement(1), p.V1.GetUpper().GetElement(1),
                p.V2.GetLower().GetElement(1), p.V2.GetUpper().GetElement(1));
            Vector256<double> d = Avx2.Min(sD, fD);
            return d;
        }

        #endregion
    }
}
