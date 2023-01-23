using System.Collections.Generic;

namespace Albedo {

	public partial class ConnToClientData {

		public NetIdentity identity;
		internal readonly HashSet<NetIdentity> observing = new();
	}
}
