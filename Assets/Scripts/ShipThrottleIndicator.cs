using UnityEngine;

public class ShipThrottleIndicator : MonoBehaviour
{
	[SerializeField] ProgressBar _forwardProgressBar;
	[SerializeField] ProgressBar _backProgressBar;
	private float _throttle;

	public float Throttle
	{
		get => _throttle; 
		set
		{
			_throttle = value;
			_forwardProgressBar.Progress = Mathf.Clamp01(_throttle);
			_backProgressBar.Progress = Mathf.Clamp01(-_throttle);
		}
	}
}
