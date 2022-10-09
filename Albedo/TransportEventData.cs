using System;

namespace Albedo {

	public struct TransportEventData {

		public enum Type {
			Conn,
			Data,
			Error,
			Disconn
		}

		public Type type;
		public uint connId;
		public ArraySegment<byte> segment;
	}
}
