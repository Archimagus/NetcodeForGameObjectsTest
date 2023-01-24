using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayer : NetworkBehaviour
{
	[SerializeField] private NetworkShip _myShipPrefab;
	private NetworkShip _myShip;

	struct PlayerData : INetworkSerializable
	{
		public string Name;
		public int Money;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref Name);
			serializer.SerializeValue(ref Money);
		}
	}
	private NetworkVariable<PlayerData> _playerData = new NetworkVariable<PlayerData>(new PlayerData
	{
		Name = "Joe",
		Money = 0,
	}, writePerm: NetworkVariableWritePermission.Owner);

	public string PlayerName { get; private set; }

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		_playerData.OnValueChanged += (previous, newValue) =>
		{
			Debug.Log($"{OwnerClientId} Name:{newValue.Name} Money:{newValue.Money}");
			PlayerName = newValue.Name;
			_myShip?.SetLabel(PlayerName);
		};

		if (IsOwner)
		{
			_playerData.Value = new PlayerData
			{
				Money = 0,
				Name = GameServices.Instance.PlayerName
			};
		}
		else
		{
			GetComponent<PlayerInput>().enabled = false;
		}

		if (IsServer)
		{

			var _myShip = Instantiate(_myShipPrefab, Vector3.zero, Quaternion.identity);
			_myShip.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
			GetShipClientRpc(_myShip.NetworkObjectId);
		}
	}
	[ClientRpc]
	private void GetShipClientRpc(ulong networkId)
	{
		_myShip = GetNetworkObject(networkId).GetComponent<NetworkShip>();
		_myShip.Player = this;
		_myShip.SetLabel(PlayerName);
	}

	public void OnMoveAction(InputAction.CallbackContext context)
	{
		if (!IsOwner) return;
		_myShip?.MoveInput(context.ReadValue<Vector2>());
	}
}
