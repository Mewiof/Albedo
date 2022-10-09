﻿using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Albedo {

	public sealed class Reader {

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetPosition(int position) {
			_position = position;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(ArraySegment<byte> data) {
			_data = data;
			_position = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Writer writer) {
			Set(writer.Data);
		}

		public Reader() { }

		public Reader(ArraySegment<byte> data) {
			Set(data);
		}

		public Reader(Writer writer) {
			Set(writer);
		}

		#region Get

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal T GetUnmanaged<T>()
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
			return (char)GetUShort();
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ArraySegment<byte> GetDataSegment(int count) {
			ArraySegment<byte> result = new(_data.Array, _data.Offset + _position, count);
			_position += count;
			return result;
		}

		public ArraySegment<byte> GetRemainingDataSegment() {
			return GetDataSegment(Available);
		}

		#region Array

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T[] GetArray<T>(ushort size) {
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

		#endregion
	}
}
