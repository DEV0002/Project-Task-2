using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace GLS.Intrinsics {
    public struct Vector512<T> : IEquatable<Vector256<T>> where T : struct {
        bool IEquatable<Vector256<T>>.Equals(Vector256<T> other) {
            throw new NotImplementedException();
        }
        public Vector256<T> V1, V2;
        public Vector512(Vector256<T> _V1, Vector256<T> _V2) {
            V1 = _V1;
            V2 = _V2;
        }
        public Vector512(Vector256<T> _V) : this(){
            this.V1 = this.V2 = _V;
        }
        public static Vector512<float> Create(float value) {
            return new Vector512<float>(Vector256.Create(value));
        }
        public static Vector512<double> Create(double value) {
            return new Vector512<double>(Vector256.Create(value));
        }
    }

    public class Avx5 {
        public static Vector512<float> Add(Vector512<float> left, Vector512<float> right) {
            return new Vector512<float>(Avx2.Add(left.V1, right.V1), Avx2.Add(left.V2, right.V2));
        }
        public static Vector512<float> Subtract(Vector512<float> left, Vector512<float> right) {
            return new Vector512<float>(Avx2.Subtract(left.V1, right.V1), Avx2.Subtract(left.V2, right.V2));
        }
        public static Vector512<float> Multiply(Vector512<float> left, Vector512<float> right) {
            return new Vector512<float>(Avx2.Multiply(left.V1, right.V1), Avx2.Multiply(left.V2, right.V2));
        }
        public static Vector512<float> Divide(Vector512<float> left, Vector512<float> right) {
            return new Vector512<float>(Avx2.Divide(left.V1, right.V1), Avx2.Divide(left.V2, right.V2));
        }
        public static Vector512<float> MultiplyAdd(Vector512<float> left, Vector512<float> right, Vector512<float> add) {
            return new Vector512<float>(Fma.MultiplyAdd(left.V1, right.V1, add.V1), Fma.MultiplyAdd(left.V2, right.V2, add.V2));
        }
        public static Vector256<double> Length(Vector512<float> value) {
            Vector128<float> vlow, vhigh;
            Vector128<double> v1d;
            vlow = value.V1.GetLower();
            vhigh = value.V1.GetUpper();
            v1d = Vector128.Create(Avx2.DotProduct(vlow, vlow, 0xFF).GetElement(0), Avx2.DotProduct(vhigh, vhigh, 0xFF).GetElement(0));
            vlow = value.V2.GetLower();
            vhigh = value.V2.GetUpper();
            return Vector256.Create(v1d, Vector128.Create(Avx2.DotProduct(vlow, vlow, 0xFF).GetElement(0), Avx2.DotProduct(vhigh, vhigh, 0xFF).GetElement(0)));
        }
        public static Vector512<float> Sqrt(Vector512<float> value) {
            value.V1 = Avx2.Sqrt(value.V1);
            value.V2 = Avx2.Sqrt(value.V2);
            return value;
        }
    }
}
