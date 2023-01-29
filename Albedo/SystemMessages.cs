namespace Albedo {

	public static class SystemMessages {

		public const ushort
			// Server only
			AUTH_REQUEST = ushort.MaxValue,
			// Client only
			AUTH_RESPONSE = ushort.MaxValue,
			INT_REQUEST = ushort.MaxValue - 1,
			INT_RESPONSE = ushort.MaxValue - 2;
	}
}
