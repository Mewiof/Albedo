using System;

namespace Albedo {

	public struct TransportEventData {

		public enum Type {
			Conn,
			Data,
			Error,
			Disconn
		}

		public enum Error {// TODO
		}

		public struct DisconnInfo {// TODO
		}

		public Type type;
		public uint connId;
		public ArraySegment<byte> segment;
		public Error error;
		public DisconnInfo disconnInfo;
	}
}
