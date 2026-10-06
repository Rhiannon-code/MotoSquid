using System.Collections.Generic;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Common {

	public static class Yielders {

		public static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();
		public static readonly WaitForFixedUpdate FixedUpdate = new WaitForFixedUpdate();

		public static readonly WaitForSeconds ForMilliseconds10 = new WaitForSeconds(0.01f);
		public static readonly WaitForSeconds ForMilliseconds100 = new WaitForSeconds(0.1f);
		public static readonly WaitForSeconds ForMilliseconds200 = new WaitForSeconds(0.2f);
		public static readonly WaitForSecondsRealtime ForSecondsRealtime1 = new WaitForSecondsRealtime(1f);
		public static readonly WaitForSecondsRealtime ForSecondsRealtime2 = new WaitForSecondsRealtime(2f);
		public static readonly WaitForSeconds ForSeconds1 = new WaitForSeconds(1f);
		public static readonly WaitForSeconds ForSeconds2 = new WaitForSeconds(2f);
		public static readonly WaitForSeconds ForSeconds3 = new WaitForSeconds(3f);
		public static readonly WaitForSeconds ForSeconds4 = new WaitForSeconds(4f);
		public static readonly WaitForSeconds ForSeconds5 = new WaitForSeconds(5f);
		public static readonly WaitForSeconds ForSeconds10 = new WaitForSeconds(10f);
		public static readonly WaitForSeconds ForSeconds30 = new WaitForSeconds(30f);
		public static readonly WaitForSeconds ForMinutes1 = new WaitForSeconds(60f);

		private static readonly Dictionary<float, WaitForSeconds> _mTimeIntervals;
		private static readonly Dictionary<float, WaitForSecondsRealtime> _mTimeRealtimeIntervals;

		static Yielders() {
			_mTimeIntervals = new Dictionary<float, WaitForSeconds>(100) {
				{0.01f, ForMilliseconds10},
				{0.1f, ForMilliseconds100},
				{0.2f, ForMilliseconds200},
				{1f, ForSeconds1},
				{2f, ForSeconds2},
				{3f, ForSeconds3},
				{4f, ForSeconds4},
				{5f, ForSeconds5},
				{10f, ForSeconds10},
				{30f, ForSeconds30},
				{60f, ForMinutes1}
			};

			_mTimeRealtimeIntervals = new Dictionary<float, WaitForSecondsRealtime>();
		}

		public static WaitForSeconds ForSeconds(float seconds) {
			if (_mTimeIntervals.TryGetValue(seconds, out WaitForSeconds yielder)) {
				return yielder;
			}
			yielder = new WaitForSeconds(seconds);
			_mTimeIntervals.Add(seconds, yielder);
			return yielder;
		}

		public static WaitForSecondsRealtime ForSecondsRealtime(float seconds) {
			if (_mTimeRealtimeIntervals.TryGetValue(seconds, out WaitForSecondsRealtime yielder)) {
				return yielder;
			}
			yielder = new WaitForSecondsRealtime(seconds);
			_mTimeRealtimeIntervals.Add(seconds, yielder);
			return yielder;
		}

	}

}