using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Albedo {

	public sealed class Writer {

		public static readonly UTF8Encoding encoding = new(false, true);
		public const int STRING_BUFFER_MAX_LENGTH = 1024 * 32;
		private readonly byte[] _stringBuffer = new byte[STRING_BUFFER_MAX_LENGTH];

		private byte[] _data = new byte[1024];
		private int _position;

		public ArraySegment<byte> Data {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new(_data, 0, _position);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetPosition(int position) {
			_position = position;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void EnsureCapacity(int value) {
			if (_data.Length < value) {
				int capacity = Math.Max(value, _data.Length * 2);
				Array.Resize(ref _data, capacity);
			}
		}

		/// <summary>Also sets '_position' to 0</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(ArraySegment<byte> data) {
			_data = data.Array;
			_position = 0;
		}

		public Writer() { }

		public Writer(ArraySegment<byte> data) {
			Set(data);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] ToArray() {
			byte[] result = new byte[_position];
			Array.ConstrainedCopy(_data, 0, result, 0, _position);
			return result;
		}

		#region Put

		// Unmanaged

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PutUnmanaged<T>(T value)
			where T : unmanaged {

			int size = Unsafe.SizeOf<T>();
			EnsureCapacity(_position + size);
			Unsafe.As<byte, T>(ref _data[_position]) = value;
			_position += size;
		}

		public void PutByte(byte value) {
			PutUnmanaged(value);
		}

		public void PutSByte(sbyte value) {
			PutUnmanaged(value);
		}

		public const byte TRUE = 1, FALSE = 0;

		public void PutBool(bool value) {
			PutUnmanaged(value ? TRUE : FALSE);
		}

		public void PutShort(short value) {
			PutUnmanaged(value);
		}

		public void PutUShort(ushort value) {
			PutUnmanaged(value);
		}

		public void PutChar(char value) {
			PutUShort(value);
		}

		public void PutInt(int value) {
			PutUnmanaged(value);
		}

		public void PutUInt(uint value) {
			PutUnmanaged(value);
		}

		public void PutFloat(float value) {
			PutUnmanaged(value);
		}

		public void PutLong(long value) {
			PutUnmanaged(value);
		}

		public void PutULong(ulong value) {
			PutUnmanaged(value);
		}

		public void PutDouble(double value) {
			PutUnmanaged(value);
		}

		// String

		/// <param name="maxLength">[!] Limits the number of characters in a string, not its size in bytes</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PutString(string value, int maxLength) {
			if (value == null) {
				PutUShort(0);
				return;
			}

			int length = maxLength > 0 && value.Length > maxLength ? maxLength : value.Length;
			// size in bytes
			length = encoding.GetBytes(value, 0, length, _stringBuffer, 0);

			if (length >= STRING_BUFFER_MAX_LENGTH) {
				PutUShort(0);
				return;
			}

			// size in bytes
			PutUShort(checked((ushort)(length + 1)));
			PutRaw(_stringBuffer, 0, length);
		}

		public void PutString(string value) {
			PutString(value, 0);
		}

		// Raw

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PutRaw(byte[] data, int offset, int length) {
			EnsureCapacity(_position + length);
			Buffer.BlockCopy(data, offset, _data, _position, length);
			_position += length;
		}

		public void PutRaw(byte[] data) {
			PutRaw(data, 0, data.Length);
		}

		#region Array

		// Unmanaged

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PutArray(Array value, int size) {
			if (value == null) {
				PutUShort(0);
				return;
			}
			ushort length = (ushort)value.Length;
			if (length == 0) {
				PutUShort(1);
				return;
			}
			size *= length;
			length++;
			PutUShort(length);
			EnsureCapacity(_position + size);
			Buffer.BlockCopy(value, 0, _data, _position, size);
			_position += size;
		}

		public void PutByteArray(byte[] value) {
			PutArray(value, sizeof(byte));
		}

		public void PutSByteArray(sbyte[] value) {
			PutArray(value, sizeof(sbyte));
		}

		public void PutBoolArray(bool[] value) {
			PutArray(value, sizeof(bool));
		}

		public void PutShortArray(short[] value) {
			PutArray(value, sizeof(short));
		}

		public void PutUShortArray(ushort[] value) {
			PutArray(value, sizeof(ushort));
		}

		public void PutIntArray(int[] value) {
			PutArray(value, sizeof(int));
		}

		public void PutUIntArray(uint[] value) {
			PutArray(value, sizeof(uint));
		}

		public void PutFloatArray(float[] value) {
			PutArray(value, sizeof(float));
		}

		public void PutLongArray(long[] value) {
			PutArray(value, sizeof(long));
		}

		public void PutULongArray(ulong[] value) {
			PutArray(value, sizeof(ulong));
		}

		public void PutDoubleArray(double[] value) {
			PutArray(value, sizeof(double));
		}

		// String

		public void PutStringArray(string[] value) {
			if (value == null) {
				PutUShort(0);
				return;
			}
			ushort length = (ushort)value.Length;
			if (length == 0) {
				PutUShort(1);
				return;
			}
			PutUShort((ushort)(length + 1));
			for (int i = 0; i < length; i++) {
				PutString(value[i]);
			}
		}

		#endregion

		// Misc

		public void Put<T>(T value) where T : INetSerializable {
			value.Serialize(this);
		}

		#endregion
	}
}
