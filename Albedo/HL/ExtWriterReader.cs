using UnityEngine;

namespace Albedo {

	public static class ExtWriterReader {

		public static void PutVector2(this Writer writer, Vector2 value) {
			writer.PutFloat(value.x);
			writer.PutFloat(value.y);
		}

		public static Vector2 GetVector2(this Reader reader) {
			return new(reader.GetFloat(), reader.GetFloat());
		}

		/// <summary>
		/// Z only
		/// </summary>
		public static void PutEulerAngles2D(this Writer writer, Vector3 value) {
			writer.PutFloat(value.z);
		}

		/// <summary>
		/// Z only
		/// </summary>
		public static Vector3 GetEulerAngles2D(this Reader reader) {
			return new(0f, 0f, reader.GetFloat());
		}
	}
}
