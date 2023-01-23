using System.Runtime.CompilerServices;

namespace Albedo {

	public static partial class Utils {

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetPersistentHashCode(this string str) {
			unchecked {
				const uint PRIME = 16777619;
				const uint OFFSET = 2166136261;

				uint hash = OFFSET;

				for (int i = 0; i < str.Length; i++) {
					hash = (hash ^ str[i]) * PRIME;
				}

				return (int)hash;
			}
		}
	}
}
