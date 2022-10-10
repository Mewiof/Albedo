using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Albedo {

	public class ConnToClientData {

		public uint id;
		public EndPoint endPoint;

		public enum AuthStage {
			NotAuthenticated,
			Requested,
			Authenticated
		}

		public AuthStage authStage;

		public void Set(uint id, EndPoint endPoint) {
			this.id = id;
			this.endPoint = endPoint;
			authStage = AuthStage.NotAuthenticated;
		}
	}
}
