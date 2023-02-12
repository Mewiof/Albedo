using System.Net;

namespace Albedo {

	public partial class ConnToClientData {

		public readonly uint id;
		public readonly IPEndPoint endPoint;
		public readonly string address;

		public ConnToClientData(uint id, IPEndPoint endPoint) {
			this.id = id;
			this.endPoint = endPoint;
			address = endPoint.Address.ToString();
		}

		/* The 'Requested' stage to make sure that client will not send
		 * multiple authorization requests while waiting for server's
		 * decision/response
		 */
		public enum AuthStage {
			NotAuthenticated,
			Requested,
			Authenticated
		}

		public AuthStage authStage = AuthStage.NotAuthenticated;

		// For async methods
		public bool disconnected = false;
	}
}
