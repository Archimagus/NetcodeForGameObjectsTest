using Unity.Netcode;
using UnityEngine;

public class NetworkShip : NetworkBehaviour
{
	[SerializeField] private float _thrusterForce = 3;
	[SerializeField] private float _reverseThrusterForce = 0.25f;
	[SerializeField] private float _throttleResponse = 3;
	[SerializeField] private float _maneuverForce = 0.1f;

	[SerializeField] private Transform _maneuverThrusterPoint;


	private NetworkPlayer _player;
	private Rigidbody2D _rb;
	private ShipThrottleIndicator _throttleIndicator;
	private bool _clampThrottle;
	private NetworkVariable<Vector3> _moveDir = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
	private NetworkVariable<float> _throttle = new NetworkVariable<float>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

	public NetworkPlayer Player
	{
		get => _player;
		set
		{
			_player = value;
		}
	}
	public void SetLabel(string name)
	{
		GetComponentInChildren<PlayerLabel>().SetPlayerName(name);

	}

	public void Stop()
	{
		_throttle.Value = 0;
		_rb.velocity = Vector2.zero;
	}
	public void Resume(Vector3? position, Quaternion? rotation)
	{
		if (position.HasValue)
		{
			_rb.position = position.Value;
		}
		if (rotation.HasValue)
		{
			_rb.SetRotation(rotation.Value);
		}
	}
	public void MoveInput(Vector2 moveDir)
	{
		_moveDir.Value = moveDir;
	}


	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		_throttleIndicator = GetComponentInChildren<ShipThrottleIndicator>();

		if (IsServer)
		{
			_rb = GetComponent<Rigidbody2D>();
		}
		Debug.Log($"Spawned Ship; Me {NetworkManager.Singleton.LocalClientId} Owner {OwnerClientId} Is Owner? {IsOwner}");
	}

	public override void OnGainedOwnership()
	{
		base.OnGainedOwnership();
		Camera.main.GetComponent<LookAheadTrackingCamera>().Target = transform;
		Debug.Log($"Gained Ship Ownership Me{NetworkManager.Singleton.LocalClientId} Owner{OwnerClientId}");
	}



	private void Update()
	{
		if (IsOwner)
		{
			var throttleInput = _moveDir.Value.y;
			if (_throttle.Value > 0 && throttleInput < 0)
			{
				_throttle.Value = Mathf.Clamp(_throttle.Value + throttleInput * _throttleResponse * Time.deltaTime, 0, _thrusterForce);
				_throttle.Value = _throttle.Value;
				_clampThrottle = true;
			}
			else if (_throttle.Value < 0 && throttleInput > 0)
			{
				_throttle.Value = Mathf.Clamp(_throttle.Value + throttleInput * _throttleResponse * Time.deltaTime, -_reverseThrusterForce, 0);
				_throttle.Value = _throttle.Value;
				_clampThrottle = true;
			}
			else if (throttleInput == 0)
			{
				_clampThrottle = false;
			}
			else if (!_clampThrottle)
			{
				_throttle.Value = Mathf.Clamp(_throttle.Value + throttleInput * _throttleResponse * Time.deltaTime, -_reverseThrusterForce, _thrusterForce);
			}
		}
		if (_throttleIndicator != null)
		{
			if (_throttle.Value >= 0)
			{
				_throttleIndicator.Throttle = _throttle.Value / _thrusterForce;
			}
			else
			{
				_throttleIndicator.Throttle = _throttle.Value / _reverseThrusterForce;
			}
		}
	}

	void FixedUpdate()
	{
		if (!IsServer) return;


		var pushForce = Vector2.up * _throttle.Value;
		var turnForce = -transform.right * _moveDir.Value.x * _maneuverForce * transform.InverseTransformVector(_rb.velocity).y;

		_rb.AddRelativeForce(pushForce, ForceMode2D.Force);
		_rb.AddForceAtPosition(turnForce, _maneuverThrusterPoint.position, ForceMode2D.Force);

	}
}
