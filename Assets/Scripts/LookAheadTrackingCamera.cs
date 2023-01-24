using System;
using UnityEngine;

public class LookAheadTrackingCamera : MonoBehaviour
{
	[SerializeField] private Transform _target; // the object to follow
	[SerializeField] private float _lookAhead = 3f; // how far ahead to look
	[SerializeField] private float _damping = 0.3f; // how smoothly to move the camera

	[SerializeField] private Vector3 _offset = new Vector3(0,0,-10); // the start offset of the camera

	private Vector3 _currentVelocity;
	private Rigidbody2D _rb;

	public Transform Target
	{
		get => _target; 
		set
		{
			_target = value;
			if(_target != null )
			{
				_rb = _target.GetComponent<Rigidbody2D>();
				if(_rb == null )
				{
					Debug.LogError("Target must have a Rigidbody2D", this._target);
				}
			}
		}
	}

	void Start()
	{
		Target = _target;
	}

	void LateUpdate()
	{
		if (_target == null || _rb == null) { return; }
		// calculate target position based on velocity
		Vector3 targetCamPos = _target.position + _offset;
		Vector3 targetPos = targetCamPos +  _lookAhead * (Vector3)_rb.velocity;

		// smoothly move the camera
		transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _currentVelocity, _damping);
	}
}