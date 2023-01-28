using System;
using UnityEngine;

namespace Albedo {

	[DefaultExecutionOrder(-1000)]
	public class NetManager : MonoBehaviour {

		public const string LOCAL_ADDRESS = "127.0.0.1";

		public Logger Logger { get; private set; }

		public NetServer Server { get; private set; }
		public NetClient Client { get; private set; }

		public string address = LOCAL_ADDRESS;
		public ushort port = 25500;
		public int maxNumOfConnections = 4;
		public Transport transport;
		public NetAuthenticator authenticator;

		/// <summary>Override. Called on server and client during manager initialization</summary>
		protected virtual void OnInit() { }

		internal virtual void Init() {
			Logger = new(name);

			if (transport == null) {
				throw new Exception(Logger.GetTaggedParamDescText(nameof(transport), "is null"));
			}

			if (authenticator == null) {
				throw new Exception(Logger.GetTaggedParamDescText(nameof(authenticator), "is null"));
			}

			Server = new(transport, this);
			Client = new(transport, this);

			authenticator.RegisterMessageHandlers();

			OnInit();
		}

		internal virtual void Tick(float delta) {
			if (transport.IsServer) {
				Server.Tick(ref delta);
			}
			if (transport.IsClient) {
				Client.Tick();
			}
		}

		#region Start / Stop

		public void StartServer(ushort port) {
			Server.Start(port);
		}

		public void StartServer() {
			StartServer(port);
		}

		public void StopServer() {
			Server.Stop();
		}

		public void StartClient(string address, ushort port) {
			Client.Start(address, port);
		}

		public void StartClient() {
			StartClient(address, port);
		}

		public void StopClient() {
			Client.Stop();
		}

		public void StartHost() {
			StartServer();
			StartClient(LOCAL_ADDRESS, port);
		}

		public void StopHost() {
			if (transport.IsClient) {
				StopClient();
			}
			if (transport.IsServer) {
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
