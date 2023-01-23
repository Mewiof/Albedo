namespace Albedo {

	internal static class Messages {

		internal static class ForClient {

			internal const ushort
				SPAWN_IDENTITY = ushort.MaxValue - 1,
				DESPAWN_IDENTITY = ushort.MaxValue - 2;

			internal readonly struct SpawnIdentity : INetSerializable {

				private readonly NetIdentity _identity;
				private readonly bool _isLocalPlayer;

				public SpawnIdentity(NetIdentity identity, bool isLocalPlayer) {
					_identity = identity;
					_isLocalPlayer = isLocalPlayer;
				}

				public void Serialize(Writer writer) {
					writer.PutUInt(_identity.NetId);
					writer.PutBool(_isLocalPlayer);
					writer.PutVector2(_identity.transform.localPosition);
					writer.PutEulerAngles2D(_identity.transform.localEulerAngles);
				}

				public void Deserialize(Reader reader) {
					_identity.NetId = reader.GetUInt();
					_identity.IsLocalPlayer = reader.GetBool();
					_identity.transform.localPosition = reader.GetVector2();
					_identity.transform.localEulerAngles = reader.GetEulerAngles2D();
				}
			}
		}
	}
}
