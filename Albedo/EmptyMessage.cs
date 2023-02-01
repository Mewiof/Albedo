namespace Albedo {

	public readonly struct EmptyMessage : INetSerializable {

		public static readonly EmptyMessage instance = new();

		public void Serialize(Writer writer) { }

		public void Deserialize(Reader reader) { }
	}
}
