namespace GreatClock.Common.Camera3D {

	public struct EarthQuakeData {
		public bool hard;
		public float duration;
		public float frequency;
		public float direction;
		public float amplitude;

		public static EarthQuakeData EarthQuakeStrong(bool hard, float duration, float frequency) {
			EarthQuakeData data = new EarthQuakeData();
			data.hard = hard;
			data.duration = duration;
			data.frequency = frequency;
			data.direction = float.NaN;
			data.amplitude = 10f;
			return data;
		}

		public static EarthQuakeData EarthQuakeMedium(bool hard, float duration, float frequency) {
			EarthQuakeData data = new EarthQuakeData();
			data.hard = hard;
			data.duration = duration;
			data.frequency = frequency;
			data.direction = float.NaN;
			data.amplitude = 7f;
			return data;
		}

		public static EarthQuakeData EarthQuakeWeak(bool hard, float duration, float frequency) {
			EarthQuakeData data = new EarthQuakeData();
			data.hard = hard;
			data.duration = duration;
			data.frequency = frequency;
			data.direction = float.NaN;
			data.amplitude = 4f;
			return data;
		}
	}

}
