namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	public class BreakByClick : BreakAbstractTrigger {

		private void OnMouseDown() {
			_breakableObject.Break();
		}

	}

}