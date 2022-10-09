namespace Albedo {

	public interface INetSerializable {

		public void Serialize(Writer writer);

		public void Deserialize(Reader reader);
	}
}
