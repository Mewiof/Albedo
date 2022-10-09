namespace Albedo.Tests {

	[TestFixture]
	[Category("WriterReader")]
	public class WriterReader {

		// Unmanaged

		[Test]
		public void WriteReadByte() {
			Writer writer = new();
			writer.PutByte(byte.MinValue);
			writer.PutByte(byte.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetByte(), Is.EqualTo(byte.MinValue));
				Assert.That(reader.GetByte(), Is.EqualTo(byte.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadSByte() {
			Writer writer = new();
			writer.PutSByte(sbyte.MinValue);
			writer.PutSByte(sbyte.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetSByte(), Is.EqualTo(sbyte.MinValue));
				Assert.That(reader.GetSByte(), Is.EqualTo(sbyte.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadBool() {
			Writer writer = new();
			writer.PutBool(false);
			writer.PutBool(true);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetBool(), Is.EqualTo(false));
				Assert.That(reader.GetBool(), Is.EqualTo(true));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadShort() {
			Writer writer = new();
			writer.PutShort(short.MinValue);
			writer.PutShort(short.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetShort(), Is.EqualTo(short.MinValue));
				Assert.That(reader.GetShort(), Is.EqualTo(short.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadUShort() {
			Writer writer = new();
			writer.PutUShort(ushort.MinValue);
			writer.PutUShort(ushort.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetUShort(), Is.EqualTo(ushort.MinValue));
				Assert.That(reader.GetUShort(), Is.EqualTo(ushort.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadChar() {
			Writer writer = new();
			writer.PutChar(' ');
			writer.PutChar('Ё');

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetChar(), Is.EqualTo(' '));
				Assert.That(reader.GetChar(), Is.EqualTo('Ё'));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadInt() {
			Writer writer = new();
			writer.PutInt(int.MinValue);
			writer.PutInt(int.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetInt(), Is.EqualTo(int.MinValue));
				Assert.That(reader.GetInt(), Is.EqualTo(int.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadUInt() {
			Writer writer = new();
			writer.PutUInt(uint.MinValue);
			writer.PutUInt(uint.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetUInt(), Is.EqualTo(uint.MinValue));
				Assert.That(reader.GetUInt(), Is.EqualTo(uint.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadFloat() {
			Writer writer = new();
			writer.PutFloat(float.MinValue);
			writer.PutFloat(float.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetFloat(), Is.EqualTo(float.MinValue));
				Assert.That(reader.GetFloat(), Is.EqualTo(float.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadLong() {
			Writer writer = new();
			writer.PutLong(long.MinValue);
			writer.PutLong(long.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetLong(), Is.EqualTo(long.MinValue));
				Assert.That(reader.GetLong(), Is.EqualTo(long.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadULong() {
			Writer writer = new();
			writer.PutULong(ulong.MinValue);
			writer.PutULong(ulong.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetULong(), Is.EqualTo(ulong.MinValue));
				Assert.That(reader.GetULong(), Is.EqualTo(ulong.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadDouble() {
			Writer writer = new();
			writer.PutDouble(double.MinValue);
			writer.PutDouble(double.MaxValue);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetDouble(), Is.EqualTo(double.MinValue));
				Assert.That(reader.GetDouble(), Is.EqualTo(double.MaxValue));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		// String

		[Test]
		public void WriteReadString() {
			Writer writer = new();
			writer.PutString("123456789", 8);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetString(), Is.EqualTo("12345678"));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		[Test]
		public void WriteReadString1() {
			Writer writer = new();
			writer.PutString(string.Empty);
			writer.PutString("123aBcаБв");
			writer.PutString(null);
			writer.PutString("اختبار");

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetString(), Is.EqualTo(string.Empty));
				Assert.That(reader.GetString(), Is.EqualTo("123aBcаБв"));
				Assert.That(reader.GetString(), Is.EqualTo(null));
				Assert.That(reader.GetString(), Is.EqualTo("اختبار"));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		// Raw

		[Test]
		public void WriteReadWriteRaw() {
			Writer writer = new();

			byte[] arr = new byte[] { byte.MinValue, byte.MaxValue };

			writer.PutRaw(arr);
			writer.PutRaw(arr);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetDataSegment(2).ToArray(), Is.EqualTo(arr));
				Assert.That(reader.GetRemainingDataSegment().ToArray(), Is.EqualTo(arr));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}

		#region Array
		[Test]
		public void WriteReadByteArray() {
			Writer writer = new();

			byte[]
				arr1 = null,
				arr2 = new byte[] { byte.MinValue, byte.MaxValue },
				arr3 = Array.Empty<byte>();

			writer.PutByteArray(arr1);
			writer.PutByteArray(arr2);
			writer.PutByteArray(arr3);

			Reader reader = new(writer);

			Assert.Multiple(() => {
				Assert.That(reader.GetByteArray(), Is.EqualTo(arr1));
				Assert.That(reader.GetByteArray(), Is.EqualTo(arr2));
				Assert.That(reader.GetByteArray(), Is.EqualTo(arr3));

				Assert.That(reader.Available, Is.EqualTo(0));
			});
		}
		#endregion
	}
}
