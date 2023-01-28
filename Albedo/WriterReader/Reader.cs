using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Albedo {

	public static class ReaderPool {

		private static readonly Pool<PooledReader> _pool = new(() => new(new byte[] { }), 1024);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PooledReader Get(ArraySegment<byte> data) {
			PooledReader result = _pool.Get();
			result.Set(data);
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Return(PooledReader value) {
			_pool.Return(value);
		}
	}

	public sealed class PooledReader : Reader, IDisposable {

		internal PooledReader(ArraySegment<byte> data) : base(data) { }

		public void Dispose() {
			ReaderPool.Return(this);
		}
	}

	public class Reader {

		private ArraySegment<byte> _data;
		private int _position;

		public ArraySegment<byte> Data {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _data;
		}
		public int Available {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _data.Count - _position;
		}

		/// <summary>Also sets '_position' to 0</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(ArraySegment<byte> data) {
			_data = data;
			_position = 0;
		}

		public Reader() { }

		public Reader(ArraySegment<byte> data) {
			Set(data);
		}

		#region Get

		// Unmanaged

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T GetUnmanaged<T>()
			where T : unmanaged {

			int size = Unsafe.SizeOf<T>();

			if (Available < size) {
				throw new EndOfStreamException($"[{nameof(GetUnmanaged)}<{typeof(T)}>] Out of range ({Available}/{size})");
			}

			T result = Unsafe.As<byte, T>(ref _data.Array[_data.Offset + _position]);
			_position += size;
			return result;
		}

		public byte GetByte() {
			return GetUnmanaged<byte>();
		}

		public sbyte GetSByte() {
			return GetUnmanaged<sbyte>();
		}

		public bool GetBool() {
			return GetUnmanaged<byte>() == Writer.TRUE;
		}

		public short GetShort() {
			return GetUnmanaged<short>();
		}

		public ushort GetUShort() {
			return GetUnmanaged<ushort>();
		}

		public char GetChar() {
			return (char)GetUnmanaged<ushort>();
		}

		public int GetInt() {
			return GetUnmanaged<int>();
		}

		public uint GetUInt() {
			return GetUnmanaged<uint>();
		}

		public float GetFloat() {
			return GetUnmanaged<float>();
		}

		public long GetLong() {
			return GetUnmanaged<long>();
		}

		public ulong GetULong() {
			return GetUnmanaged<ulong>();
		}

		public double GetDouble() {
			return GetUnmanaged<double>();
		}

		// String

		/// <param name="maxLength">[!] Limits the number of characters in a string, not its size in bytes</param>
		/// <returns>
		/// 'null' if size reaches 'Writer.STRING_BUFFER_MAX_LENGTH'
		/// <para>'string.Empty' if length exceeds 'maxLength'</para>
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string GetString(int maxLength) {
			// size in bytes (+1 for null support)
			ushort size = GetUShort();
			if (size == 0) {
				return null;
			}

			// actual size
			size--;
			if (size >= Writer.STRING_BUFFER_MAX_LENGTH) {
				return null;
			}

			ArraySegment<byte> data = GetDataSegment(size);

			return (maxLength > 0 && Writer.encoding.GetCharCount(data.Array, data.Offset, data.Count) > maxLength) ?
				string.Empty :
				Writer.encoding.GetString(data.Array, data.Offset, data.Count);
		}

		/// <returns>
		/// 'null' if size reaches 'Writer.STRING_BUFFER_MAX_LENGTH'
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string GetString() {
			// size in bytes (+1 for null support)
			ushort size = GetUShort();
			if (size == 0) {
				return null;
			}

			// actual size
			size--;
			if (size >= Writer.STRING_BUFFER_MAX_LENGTH) {
				return null;
			}

			ArraySegment<byte> data = GetDataSegment(size);

			return Writer.encoding.GetString(data.Array, data.Offset, data.Count);
		}

		// Raw

		/// <returns>Raw</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ArraySegment<byte> GetDataSegment(int byteCount) {
			ArraySegment<byte> result = new(_data.Array, _data.Offset + _position, byteCount);
			_position += byteCount;
			return result;
		}

		/// <returns>Raw</returns>
		public ArraySegment<byte> GetRemainingDataSegment() {
			return GetDataSegment(Available);
		}

		#region Array

		// Unmanaged

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T[] GetArray<T>(ushort size)
			where T : unmanaged {

			ushort length = GetUShort();
			if (length == 0) {
				return null;
			}
			if (length == 1) {
				return Array.Empty<T>();
			}

			length--;
			T[] result = new T[length];
			length *= size;
			Buffer.BlockCopy(_data.Array, _data.Offset + _position, result, 0, length);
			_position += length;
			return result;
		}

		public byte[] GetByteArray() {
			return GetArray<byte>(sizeof(byte));
		}

		public sbyte[] GetSByteArray() {
			return GetArray<sbyte>(sizeof(sbyte));
		}

		public bool[] GetBoolArray() {
			return GetArray<bool>(sizeof(bool));
		}

		public short[] GetShortArray() {
			return GetArray<short>(sizeof(short));
		}

		public ushort[] GetUShortArray() {
			return GetArray<ushort>(sizeof(ushort));
		}

		public int[] GetIntArray() {
			return GetArray<int>(sizeof(int));
		}

		public uint[] GetUIntArray() {
			return GetArray<uint>(sizeof(uint));
		}

		public float[] GetFloatArray() {
			return GetArray<float>(sizeof(float));
		}

		public double[] GetDoubleArray() {
			return GetArray<double>(sizeof(double));
		}

		public long[] GetLongArray() {
			return GetArray<long>(sizeof(long));
		}

		public ulong[] GetULongArray() {
			return GetArray<ulong>(sizeof(ulong));
		}

		// String

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string[] GetStringArray() {
			ushort length = GetUShort();
			if (length == 0) {
				return null;
			}
			if (length == 1) {
				return Array.Empty<string>();
			}

			length--;
			string[] result = new string[length];
			for (int i = 0; i < length; i++) {
				result[i] = GetString();
			}
			return result;
		}

		#endregion

		// Misc

		public T Get<T>() where T : INetSerializable, new() {
			T value = new();
			value.Deserialize(this);
			return value;
		}

		#endregion
	}
}
