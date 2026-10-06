using System;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Editor {

	public class BrokenCollectionCreatorException : Exception {

		public string ErrorTitle { get; }
		public string Error { get; }

		public BrokenCollectionCreatorException(string errorTitle, string error) {
			ErrorTitle = errorTitle;
			Error = error;

		}

	}

}