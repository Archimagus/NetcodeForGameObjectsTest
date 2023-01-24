using Unity.Services.Lobbies.Models;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class LobbyListEntry
{
	Label _nameLabel;
	Label _playersLabel;
	Label _passwordLabel;
	Button _joinButton;

	public Lobby Lobby { get; private set; }

	public event System.Action<Lobby> OnJoinClicked;

	public void SetVisualElement(VisualElement visualElement)
	{
		_nameLabel = visualElement.Q<Label>("serverName");
		_playersLabel = visualElement.Q<Label>("playerCount");
		_passwordLabel = visualElement.Q<Label>("password");
		_joinButton = visualElement.Q<Button>("joinButton");
		_joinButton.clicked += () => OnJoinClicked?.Invoke(Lobby);
	}

	public void SetLobby(Lobby lobby)
	{
		Lobby = lobby;
		_nameLabel.text = lobby.Name;
		_playersLabel.text = $"{lobby.MaxPlayers-lobby.AvailableSlots}/{lobby.MaxPlayers}";
		_passwordLabel.text = "✓";
	}
}
