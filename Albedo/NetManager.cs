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
		/// <summary>'25500' by default</summary>
		public ushort port = 25500;
		/// <summary>'4' by default</summary>
		public int maxNumOfConnections = 4;
		public Transport transport;
		public NetAuthenticator authenticator;

		/// <summary>Override (called on server and client when invoking 'Init')</summary>
		protected virtual void OnRegisterMessageHandlers() { }

		public virtual void Init() {
			if (transport == null) {
				throw new Exception($"[{name}] '{nameof(transport)}' is null");
			}

			if (authenticator == null) {
				throw new Exception($"[{name}] '{nameof(authenticator)}' is null");
			}

			server = new(transport, this);
			client = new(transport, this);

			authenticator.RegisterMessageHandlers();

			OnRegisterMessageHandlers();
		}

		public virtual void Tick(float delta) {
			if (isServer) {
				server.Tick(ref delta);
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
			ServerOnStarted();
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
			ServerOnStopped();
		}

		public virtual void StartClient(string address, ushort port) {
			if (isClient) {
				throw new Exception($"[{name}] Unable to start client (already active)");
			}
			client.Start(address, port);
			isClient = true;
			ClientOnStarted();
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
			ClientOnStopped();
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
		public virtual void ServerOnClientConnected(ConnToClientData conn) { }

		/// <summary>Override (called on server)</summary>
		public virtual void ServerOnTransportError(Transport.Error error) { }

		/// <summary>Override (called on server)</summary>
		public virtual void ServerOnClientDisconnected(ConnToClientData conn, Transport.DisconnInfo disconnInfo) { }

		// Client

		/// <summary>Override (called on client)</summary>
		public virtual void ClientOnConnected() { }

		/// <summary>Override (called on client)</summary>
		public virtual void ClientOnTransportError(Transport.Error error) { }

		/// <summary>Override (called on client)</summary>
		public virtual void ClientOnDisconnected(Transport.DisconnInfo disconnInfo) { }

		#endregion

		#region Start / Stop Callbacks

		// Server

		/// <summary>Override (called on server)</summary>
		public virtual void ServerOnStarted() { }

		/// <summary>Override (called on server)</summary>
		public virtual void ServerOnStopped() { }

		// Client

		/// <summary>Override (called on client)</summary>
		public virtual void ClientOnStarted() { }

		/// <summary>Override (called on client)</summary>
		public virtual void ClientOnStopped() { }
		#endregion
	}
}
