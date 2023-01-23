using System;
using UnityEngine;

namespace Albedo {

	[DefaultExecutionOrder(-1000)]
	public class NetManager : MonoBehaviour {

		public const string LOCAL_ADDRESS = "127.0.0.1";

		/// <summary>Read-only</summary>
		public NetServer server;
		/// <summary>Read-only</summary>
		public NetClient client;

		/// <summary>Read-only</summary>
		[HideInInspector]
		public bool isServer, isClient;

		/// <summary>For debugging</summary>
		public string debugName = nameof(NetManager);
		public string address = LOCAL_ADDRESS;
		/// <summary>'25500' by default</summary>
		public ushort port = 25500;
		/// <summary>'4' by default</summary>
		public int maxNumOfConnections = 4;
		public Transport transport;
		public NetAuthenticator authenticator;

		/// <summary>Override. Called on server and client</summary>
		protected virtual void OnRegisterMessageHandlers() { }

		public virtual void Init() {
			if (transport == null) {
				throw new Exception($"[{debugName}] '{nameof(transport)}' is null");
			}

			if (authenticator == null) {
				throw new Exception($"[{debugName}] '{nameof(authenticator)}' is null");
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

		public void StartServer(ushort port) {
			if (isServer) {
				throw new Exception(Utils.GetLogText(debugName, "Unable to start server (already active)"));
			}
			isServer = true;
			server.Start(port);
		}

		public void StartServer() {
			StartServer(port);
		}

		public void StopServer() {
			if (!isServer) {
				throw new Exception(Utils.GetLogText(debugName, "Unable to stop server (inactive)"));
			}
			isServer = false;
			server.Stop();
		}

		public void StartClient(string address, ushort port) {
			if (isClient) {
				throw new Exception(Utils.GetLogText(debugName, "Unable to start client (already active)"));
			}
			isClient = true;
			client.Start(address, port);
		}

		public void StartClient() {
			StartClient(address, port);
		}

		public void StopClient() {
			if (!isClient) {
				throw new Exception(Utils.GetLogText(debugName, "Unable to stop client (inactive)"));
			}
			isClient = false;
			client.Stop();
		}

		public void StartHost() {
			StartServer();
			StartClient(LOCAL_ADDRESS, port);
		}

		public void StopHost() {
			if (isClient) {
				StopClient();
			}
			if (isServer) {
				StopServer();
			}
		}

		#endregion

		private void Awake() {
			Init();
		}

		private void Update() {
			Tick(Time.unscaledDeltaTime);
		}

		private void OnApplicationQuit() {
			StopHost();
		}

		private void OnDestroy() {
			StopHost();
		}
	}
}
