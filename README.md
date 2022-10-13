## Albedo

> [:warning:] Conceived as an experimental lib for my experimental MMO project. I don't guarantee continuous repository support or avoiding breaking changes.

[Discord](https://discord.gg/Gg8bKVB7fs) (not related to this repository)

Requirements: `Godot 4`, `.NET 6.0+`.

###### Features:
- Lightweight
	- 2 bytes header per message
	- 7 bytes on successful authentication
	- 3 bytes header on rejection
- User-friendly & fast serialization
- Custom transport support
- Secure authentication
- Multiple client/server support

## Simple examples

`NetManager` requires an authenticator. Here is an example of its implementation:
```csharp
public class CustomNetAuthenticator : NetAuthenticator {

	public CustomNetAuthenticator(NetManager manager) : base(manager) { }
		
	// Custom request message
	private struct Request : INetSerializable {

		public string email;
		public string password;

		// For client
		public Request(string email, string password) {
			this.email = email;
			this.password = password;
		}
		
		// For client
		public void Serialize(Writer writer) {
			writer.PutString(email);
			/* [!] Sending unencrypted passwords is not the best idea.
			 * This is just an example
			 */
			writer.PutString(password);
		}

		// For server
		public void Deserialize(Reader reader) {
			/* Don't worry if client sends some random bytes here causing an exception.
			 * In that case, server will disconnect it automatically
			 */
			email = reader.GetString();
			password = reader.GetString();
		}
	}
	
	/* Don't worry if client will not send anything, by default
	 * 'NetAuthenticator' has a 'timeout' which you can set (4 seconds by default?)
	 * or remove altogether by overriding 'ServerOnAuth' method
	 */
	
	/* Don't worry if client will send several auth
	 * requests, server will accept only 1 in any case
	 */

	// Server
	protected override void OnRequestMessage(ConnToClientData conn, Reader reader) {
		Request request = reader.Get<Request>();
		
		// right pair?
		if (request.email == "correct@email.meow" && request.password == "correct_password") {
			Accept(conn);
			return;
		}
			
		/* [!] This is a bad example of sending response.
		 * It is better to send 'enum->byte' instead. This will turn 17 bytes into 1
		 */
		Reject(conn, writer => writer.PutString("wrong credentials"));
	}

	protected override void ClientOnAccepted() {
		// Server has accepted us~ <3
		
		// 'manager.client.connId' is now set, which can be used for identification
	}

	protected override void ClientOnRejected(Reader reader) {
		// Rejected ;(
	}

	public override void ClientOnAuth() {
		manager.client.SendMessage(REQUEST_MESSAGE_UNIQUE_ID, writer =>
			writer.Put(new Request("correct@email.meow", "correct_password")), DeliveryMethod.Reliable);
	}
}
```

`NetManager` (Godot):
```csharp
public enum Message : ushort {
	Hi = 0,
	Hi2 = 1,
}

public partial class CustomNetManager : NetManager {

	// Server
	private void OnHiMessage(ConnToClientData sender, Reader reader) {
		GD.Print(reader.GetString());
	}

	// Server
	private void OnHi2Message(ConnToClientData sender) {
		GD.Print("Hi2");
	}

	// Both
	protected override void OnRegisterMessageHandlers() {
		server.RegisterMessageHandler((ushort)Message.Hi, OnHiMessage);
		server.RegisterMessageHandler((ushort)Message.Hi2, OnHi2Message);
	}

	// Client
	public override void ClientOnConnected() {
		client.SendMessage((ushort)Message.Hi, writer => writer.PutString("Hi"), DeliveryMethod.Reliable);
		client.SendMessage((ushort)Message.Hi2, DeliveryMethod.Reliable);
	}
}
```

Setup (Godot):
```csharp
public partial class World : Node {

	public static CustomNetManager netManager;
	
	public override void _Ready() {
		netManager = new() {
			maxNumOfConnections = 1000
		};
		netManager.transport = new KCPTransport(netManager);
		netManager.authenticator = new CustomNetAuthenticator(netManager);
		
		netManager.Init();
		
		netManager.StartHost();
	}
	
	public override void _PhysicsProcess(double delta) {
		netManager.Tick((float)delta);
	}
	
	public override void _ExitTree() {
		if (netManager.isServer) netManager.StopServer();
		if (netManager.isClient) netManager.StopClient();
	}
}
```

## This repository would most likely not exist without
- [Mirror (MIT)](https://github.com/vis2k/Mirror) :heart:
- [LiteNetLib (MIT)](https://github.com/RevenantX/LiteNetLib) :heart:
