using System.Collections.Generic;
using UnityEngine;

namespace Albedo {

	[DisallowMultipleComponent]
	public sealed class NetIdentity : MonoBehaviour {

		#region Asset Id
		public int AssetIdHashCode { get; private set; }
		private string _assetId;
		public string AssetId {
			get => _assetId;
			set {
				_assetId = value;
				AssetIdHashCode = _assetId.GetPersistentHashCode();
			}
		}
		#endregion

		public ExtNetManager Manager { get; internal set; }
		public uint NetId { get; internal set; }
		public bool IsLocalPlayer { get; internal set; }
		/// <summary>
		/// Server
		/// </summary>
		public ConnToClientData OwnerConn { get; internal set; }
		/// <summary>
		/// Server
		/// </summary>
		public readonly Dictionary<uint, ConnToClientData> observers = new();

		public void AddObserver(ConnToClientData conn) {
			if (observers.ContainsKey(conn.id)) {//?
				throw new System.ArgumentException(conn.id.ToString());
			}
			observers[conn.id] = conn;
			_ = conn.observing.Add(this);
			IsLocalPlayer = conn.id == OwnerConn.id;
			Manager.server.SendSpawnMessageFor(conn, this);
		}

		public void RemoveObserver(ConnToClientData conn) {
			Manager.server.SendDespawnMessageFor(conn, this);
			_ = conn.observing.Remove(this);
			_ = observers.Remove(conn.id);
		}

		public void ClearObservers() {
			foreach (ConnToClientData conn in observers.Values) {
				RemoveObserver(conn);
			}
		}
	}
}
