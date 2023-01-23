using System.Collections.Generic;
using UnityEngine;

namespace Albedo {

	public class ExtNetManager : NetManager {

		[SerializeField] internal InterestManagement iM;

		public readonly Dictionary<uint, NetIdentity> spawned = new();

		internal void Spawn(NetIdentity obj, ConnToClientData ownerConn) {
			//obj.Set(this, server.NextNetId, ownerConn.id,)
		}

		/// <summary>
		/// Call the base! Called on server and client
		/// </summary>
		protected override void OnRegisterMessageHandlers() {
			client.RegisterMessageHandler(Messages.ForClient.SPAWN_IDENTITY, client.OnSpawnIdentityMessage);
			client.RegisterMessageHandler(Messages.ForClient.DESPAWN_IDENTITY, client.OnDespawnIdentityMessage);
		}

		/// <summary>
		/// Call the base! Called on server
		/// </summary>
		//public override void ServerOnStarted() {
		//	server.ResetNextNetId();
		//}
	}
}
