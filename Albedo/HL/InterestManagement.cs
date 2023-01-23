using System.Collections.Generic;
using UnityEngine;

namespace Albedo {

	[DisallowMultipleComponent]
	public abstract class InterestManagement : MonoBehaviour {

		public abstract bool CanSee(NetIdentity identity, ConnToClientData potentialObserver);

		public abstract void OnRebuildObservers(NetIdentity identity, HashSet<ConnToClientData> potentialObservers);
	}
}
