using UnityEngine;

namespace Albedo {

	public partial class NetServer : DataHandler {

		private uint _nextNetId;
		/// <summary>
		/// Increments 1
		/// </summary>
		internal uint NextNetId => _nextNetId++;
		internal void ResetNextNetId() {
			_nextNetId = 1U;
		}

		internal void SpawnObserversFor(ConnToClientData conn) {
			ExtNetManager extManager = (ExtNetManager)manager;

			foreach (NetIdentity identity in extManager.spawned.Values) {
				if (!identity.gameObject.activeSelf) {
					continue;
				}

				if (extManager.iM != null) {
					if (extManager.iM.CanSee(identity, conn)) {
						identity.AddObserver(conn);
					}
					continue;
				}

				identity.AddObserver(conn);
			}
		}

		internal void RegisterPlayerObjFor(ConnToClientData conn, NetIdentity identity) {
			ExtNetManager extManager = (ExtNetManager)manager;

			if (conn.identity != null) {
				Debug.LogError("Player object for this connection is already registered");
				return;
			}
			conn.identity = identity;

			identity.Manager = extManager;
			identity.NetId = NextNetId;
			identity.OwnerConn = conn;

			extManager.spawned[identity.NetId] = identity;

			RebuildObserversFor(identity);
		}

		internal void RebuildObserversFor(NetIdentity identity) {
			// default
			foreach (ConnToClientData conn in connections.Values) {
				if (conn.authStage == ConnToClientData.AuthStage.Authenticated) {
					identity.AddObserver(conn);
				}
			}
		}

		internal void SendSpawnMessageFor(ConnToClientData conn, NetIdentity identity) {
			SendMessage(conn.id, Messages.ForClient.SPAWN_IDENTITY, writer =>
				writer.Put(new Messages.ForClient.SpawnIdentity(identity, conn.id == identity.OwnerConn.id)), DeliveryMethod.Reliable);
		}

		internal void SendDespawnMessageFor(ConnToClientData conn, NetIdentity identity) {
			SendMessage(conn.id, Messages.ForClient.DESPAWN_IDENTITY, writer => writer.PutUInt(identity.NetId), DeliveryMethod.Reliable);
		}
	}
}
