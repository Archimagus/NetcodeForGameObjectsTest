using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Lobbies.Models;
using System.Linq;

public class MainMenu : MonoBehaviour
{
	[SerializeField] private VisualTreeAsset _lobbyListTemplate;

	private VisualElement _mainMenu;
	private VisualElement _startButtons;
	private Button _singlePlayerButton;
	private Button _hostButton;
	private Button _joinButton;
	private TextField _playerNameField;
	private Label _playerNamePlaceholder;
	private VisualElement _serverBrowswer;
	private VisualElement _lobbyList;

	async void Start()
	{
		var uiDocument = GetComponent<UIDocument>();

		_mainMenu = uiDocument.rootVisualElement.Q("mainMenu");
		_startButtons = uiDocument.rootVisualElement.Q("startButtons");
		_singlePlayerButton = uiDocument.rootVisualElement.Q<Button>("singlePlayer");
		_hostButton = uiDocument.rootVisualElement.Q<Button>("host");
		_joinButton = uiDocument.rootVisualElement.Q<Button>("join");
		_playerNameField = uiDocument.rootVisualElement.Q<TextField>("playerName");
		_playerNamePlaceholder = uiDocument.rootVisualElement.Q<Label>("playerNamePlaceholder");
		_serverBrowswer = uiDocument.rootVisualElement.Q("serverBrowser");
		_lobbyList = uiDocument.rootVisualElement.Q("lobbyList");

		_playerNameField.SetValueWithoutNotify(GameServices.Instance.PlayerName);
		_startButtons.style.visibility = GameServices.Instance.PlayerName.Length > 0 ? StyleKeyword.Null : Visibility.Hidden;
		_playerNameField.RegisterValueChangedCallback(playerNameChanged);
		_playerNamePlaceholder.visible = GameServices.Instance.PlayerName.Length == 0;
		_singlePlayerButton.clicked += async () =>
		{
			if (await GameServices.Instance.StartSinglePlayer())
			{
				uiDocument.rootVisualElement.visible = false;
			}
		};
		_hostButton.clicked += async () =>
		{
			if (await GameServices.Instance.StartHost())
			{
				uiDocument.rootVisualElement.visible = false;
			}
		};
		_joinButton.clicked += async () =>
		{
			_serverBrowswer.visible = true;
			_mainMenu.visible = false;
			await GameServices.Instance.FindLobbies(lobbyFound);
		};

		
		if (PlayerPrefs.GetInt("Shutdown Properly", 0) == 0 && await GameServices.Instance.Reconnect())
		{
			uiDocument.rootVisualElement.visible = false;
		}
		PlayerPrefs.SetInt("Shutdown Properly", 0);
	}
	private void OnApplicationQuit()
	{
		PlayerPrefs.SetInt("Shutdown Properly", 1);
		GameServices.Instance.Shutdown();
	}
	void lobbyFound(IEnumerable<Lobby> lobbies)
	{
		_lobbyList.Clear();
		Debug.Log("Lobbies: " + lobbies.Count());
		foreach (var l in lobbies)
		{
			Debug.Log("Lobby: " + l.Id);
			var newEntry = _lobbyListTemplate.Instantiate();
			var newController = new LobbyListEntry();
			newEntry.userData = newController;
			newController.SetVisualElement(newEntry);
			newController.SetLobby(l);
			newController.OnJoinClicked += async (joinLobby) =>
			{
				if(await GameServices.Instance.StartJoin(joinLobby))
				{
					GetComponent<UIDocument>().rootVisualElement.visible = false;
					_serverBrowswer.style.visibility = StyleKeyword.Null;
					_mainMenu.style.visibility = StyleKeyword.Null;
				}
				else
				{
					_serverBrowswer.visible = false;
					_mainMenu.visible = true;

				}
			};
			_lobbyList.Add(newEntry);
		}

	}
	void playerNameChanged(ChangeEvent<string> playerName)
	{
		GameServices.Instance.PlayerName = playerName.newValue;
		_startButtons.style.visibility = GameServices.Instance.PlayerName.Length > 0 ? StyleKeyword.Null: Visibility.Hidden;
		_playerNamePlaceholder.visible = GameServices.Instance.PlayerName.Length == 0;
	}
}