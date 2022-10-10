using System;
using System.Net;

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
		public EndPoint endPoint;
		public ArraySegment<byte> segment;
		public Error error;
		public DisconnInfo disconnInfo;
	}
}
