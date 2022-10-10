using System;

namespace Albedo {

	public class NetManager {

		public const string LOCAL_ADDRESS = "127.0.0.1";

		/// <summary>Read-only</summary>
		public NetServer server;
		/// <summary>Read-only</summary>
		public NetClient client;

		/// <summary>Read-only</summary>
		public bool isServer, isClient;

		/// <summary>For debugging</summary>
		public string name = nameof(NetManager);
		public string address = LOCAL_ADDRESS;
		public ushort port = 25500;
		public int maxNumOfConnections = 4;
		public ITransport transport;
		public NetAuthenticator authenticator;

		/// <summary>Override</summary>
		protected virtual void OnRegisterMessageHandlers() { }

		public virtual void Init() {
			server = new(transport, this);
			client = new(transport, this);

			if (authenticator != null) {
				authenticator.RegisterMessageHandlers();
			}

			OnRegisterMessageHandlers();
		}

		public virtual void Tick() {
			if (isServer) {
				server.Tick();
			}
			if (isClient) {
				client.Tick();
			}
		}

		#region Start / Stop

		public virtual void StartServer(ushort port) {
			if (isServer) {
				throw new Exception($"[{name}] Unable to start server (already active)");
			}
			server.Start(port);
			isServer = true;
			OnServerStarted();
		}

		public void StartServer() {
			StartServer(port);
		}

		public virtual void StopServer() {
			if (!isServer) {
				throw new Exception($"[{name}] Unable to stop server (inactive)");
			}
			server.Stop();
			isServer = false;
			OnServerStopped();
		}

		public virtual void StartClient(string address, ushort port) {
			if (isClient) {
				throw new Exception($"[{name}] Unable to start client (already active)");
			}
			client.Start(address, port);
			isClient = true;
			OnClientStarted();
		}

		public void StartClient() {
			StartClient(address, port);
		}

		public virtual void StopClient() {
			if (!isClient) {
				throw new Exception($"[{name}] Unable to stop client (inactive)");
			}
			client.Stop();
			isClient = false;
			OnClientStopped();
		}

		public virtual void StartHost() {
			StartServer();
			StartClient(LOCAL_ADDRESS, port);
		}

		public virtual void StopHost() {
			StopClient();
			StopServer();
		}

		#endregion

		#region Callbacks

		// Server

		/// <summary>Override (called on server)</summary>
		public virtual void OnClientConnected(ConnToClientData conn) { }

		/// <summary>Override (called on server)</summary>
		public virtual void OnServerTransportError(TransportEventData.Error error) { }

		/// <summary>Override (called on server)</summary>
		public virtual void OnClientDisconnected(ConnToClientData conn, TransportEventData.DisconnInfo disconnInfo) { }

		// Client

		/// <summary>Override (called on client)</summary>
		public virtual void OnConnected() { }

		/// <summary>Override (called on client)</summary>
		public virtual void OnClientTransportError(TransportEventData.Error error) { }

		/// <summary>Override (called on client)</summary>
		public virtual void OnDisconnected(TransportEventData.DisconnInfo disconnInfo) { }

		#endregion

		#region Start / Stop Callbacks

		// Server

		/// <summary>Override (called on server)</summary>
		public virtual void OnServerStarted() { }

		/// <summary>Override (called on server)</summary>
		public virtual void OnServerStopped() { }

		// Client

		/// <summary>Override (called on client)</summary>
		public virtual void OnClientStarted() { }

		/// <summary>Override (called on client)</summary>
		public virtual void OnClientStopped() { }
		#endregion
	}
}
