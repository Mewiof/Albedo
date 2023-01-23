using UnityEngine;

namespace Albedo {

	public partial class NetClient : DataHandler {

		internal void OnSpawnIdentityMessage(Reader reader) {
			ExtNetManager extManager = (ExtNetManager)manager;

			// instantiate
			NetIdentity identity = Object.Instantiate(extManager.playerPrefab);

			// set
			identity.Manager = extManager;
			Messages.ForClient.SpawnIdentity message = new(identity);
			message.Deserialize(reader);

			// add
			extManager.spawned[identity.NetId] = identity;
		}

		internal void OnDespawnIdentityMessage(Reader reader) {
			ExtNetManager extManager = (ExtNetManager)manager;
			uint netId = reader.GetUInt();

			// destroy obj
			if (extManager.spawned.TryGetValue(netId, out NetIdentity identity)) {
				Object.Destroy(identity.gameObject);
			}
			// remove
			_ = extManager.spawned.Remove(netId);
		}
	}
}
