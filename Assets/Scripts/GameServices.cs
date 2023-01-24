using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;
using System.Timers;
using System.Text;
using static Unity.Netcode.Transports.UTP.UnityTransport;

#if UNITY_EDITOR
using ParrelSync;
#endif


public class GameServices
{
	private static GameServices _instance;
	public static GameServices Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new GameServices();
			}
			return _instance;
		}
	}

	public string PlayerName { get; set; }
	public Lobby Lobby { get; set; }

	private Timer _lobbyHeartbeatTimer;
	private Timer _lobbyUpdateTimer;


	private GameServices()
	{
		PlayerName = PlayerPrefs.GetString("PlayerName");
#if UNITY_EDITOR
		if (ClonesManager.IsClone())
			PlayerName = ClonesManager.GetArgument();
#endif
	}

	private void init()
	{
		_lobbyHeartbeatTimer = new Timer(25000);
		_lobbyUpdateTimer = new Timer(1100);
		_lobbyHeartbeatTimer.AutoReset = true;
		_lobbyUpdateTimer.AutoReset = true;

		_lobbyHeartbeatTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
		{
			if (Lobby != null)
			{
				LobbyService.Instance.SendHeartbeatPingAsync(Lobby.Id);
			}
		};
		_lobbyUpdateTimer.Elapsed += async (object sender, ElapsedEventArgs e) =>
		{
			if (Lobby != null)
			{
				Lobby = await LobbyService.Instance.GetLobbyAsync(Lobby.Id);
				//printLobbyPlayers(Lobby);
			}
		};
		AuthenticationService.Instance.SignedIn += () =>
		{
			Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");
			Debug.Log($"Access Token: {AuthenticationService.Instance.AccessToken}");
		};

		AuthenticationService.Instance.SignInFailed += (err) =>
		{
			Debug.LogError(err);
		};

		AuthenticationService.Instance.SignedOut += () =>
		{
			Debug.Log("Player signed out.");
		};

		AuthenticationService.Instance.Expired += () =>
		{
			Debug.Log("Player session could not be refreshed and expired.");
		};
	}

	public void Shutdown()
	{

		if (UnityServices.State == ServicesInitializationState.Initialized && Lobby?.HostId == AuthenticationService.Instance.PlayerId)
		{
			LobbyService.Instance.DeleteLobbyAsync(Lobby.Id);
		}
		_lobbyHeartbeatTimer.Stop();
		_lobbyUpdateTimer.Stop();
		_lobbyHeartbeatTimer.Dispose();
		_lobbyUpdateTimer.Dispose();
	}

	async Task<bool> signInAnonymouslyAsync()
	{
#if UNITY_EDITOR
		if (!ClonesManager.IsClone())
			PlayerPrefs.SetString("PlayerName", PlayerName);
#else
		PlayerPrefs.SetString("PlayerName", PlayerName);
#endif

		try
		{
			var options = new InitializationOptions();
			options.SetProfile(PlayerName);
			await UnityServices.InitializeAsync(options);

			if (!AuthenticationService.Instance.IsSignedIn)
			{
				init();
				await AuthenticationService.Instance.SignInAnonymouslyAsync();
				Debug.Log("Sign in anonymously succeeded!");
			}

			Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");
			return true;

		}
		catch (AuthenticationException ex)
		{
			Debug.LogException(ex);
		}
		catch (RequestFailedException ex)
		{
			Debug.LogException(ex);
		}
		return false;
	}

	public async Task<bool> StartSinglePlayer()
	{
		try
		{
			if (await signInAnonymouslyAsync())
			{
				setTransportProtocol(ProtocolType.UnityTransport);
				NetworkManager.Singleton.StartHost();
				return true;
			}
		}
		catch (Exception exc)
		{
			Debug.Log(exc);
		}
		return false;
	}


	public async Task<bool> StartHost()
	{
		try
		{
			if (await signInAnonymouslyAsync())
			{
				setTransportProtocol(ProtocolType.RelayUnityTransport);
				var transport = NetworkManager.Singleton.gameObject.GetComponent<UnityTransport>();
				var allocation = await RelayService.Instance.CreateAllocationAsync(2);
				var relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
				Debug.Log("Relay Join Code: " + relayJoinCode);


				Lobby = await LobbyService.Instance.CreateLobbyAsync(PlayerName, 2, new CreateLobbyOptions
				{
					Player = getPlayer(),
					Data = new Dictionary<string, DataObject>
					{
						{ "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
					}
				});
				Debug.Log("Started Lobby:" + Lobby.Name + " ID:" + Lobby.Id);
				_lobbyHeartbeatTimer.Start();
				_lobbyUpdateTimer.Start();

				RelayServerData data = new(allocation, "dtls");
				transport.SetRelayServerData(data);
				if (NetworkManager.Singleton.StartHost())
				{
					return true;
				}
				Debug.Log("Failed to start host");
				_lobbyHeartbeatTimer.Stop();
				_lobbyUpdateTimer.Stop();
				await LobbyService.Instance.DeleteLobbyAsync(Lobby.Id);
				Debug.Log("Closed Lobby");
				Lobby = null;
			}
		}
		catch (RelayServiceException exc)
		{
			Debug.Log(exc);
		}
		catch (LobbyServiceException exc)
		{
			Debug.Log(exc);
		}
		catch(Exception exc)
		{
			Debug.Log(exc);
		}
		return false;

	}

	public async Task FindLobbies(Action<IEnumerable<Lobby>> lobbyFound)
	{
		if (UnityServices.State == ServicesInitializationState.Initialized || await signInAnonymouslyAsync())
		{
			try
			{
				var response = await Lobbies.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
				{
					Count = 25,
					Filters = new List<QueryFilter> {
					new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
				}
				});

				lobbyFound(response.Results);
			}
			catch (LobbyServiceException exc)
			{
				Debug.Log(exc);
			}
		}

	}
	public async Task<bool> Reconnect()
	{
		try
		{
			if (await signInAnonymouslyAsync())
			{
				var joined = await Lobbies.Instance.GetJoinedLobbiesAsync();
				if (joined.Count > 0)
				{
					Lobby = await Lobbies.Instance.ReconnectToLobbyAsync(joined[0]);
					if (await joinServer(Lobby.Data["RelayJoinCode"].Value))
					{
						_lobbyUpdateTimer.Start();
						return true;
					}
				}
			}

		}
		catch (LobbyServiceException exc)
		{
			Debug.Log(exc);
		}
		return false;
	}
	public async Task<bool> StartJoin(Lobby lobby)
	{
		try
		{
			Debug.Log("Joining Lobby ! " + lobby.Name + " ID:" + lobby.Id);
			Lobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, new JoinLobbyByIdOptions { Player = getPlayer() });

			Debug.Log("Joined Lobby ! " + Lobby.Name + " ID:" + Lobby.Id);
			printLobbyPlayers(Lobby);
			_lobbyUpdateTimer.Start();

			return await joinServer(Lobby.Data["RelayJoinCode"].Value);
		}
		catch (LobbyServiceException exc)
		{
			Debug.Log(exc);
		}
		return false;
	}

	private async Task<bool> joinServer(string relayJoinCode)
	{
		try
		{
			Debug.Log($"Joining Relay Join Code: {relayJoinCode}");
			setTransportProtocol(ProtocolType.RelayUnityTransport);
			var transport = NetworkManager.Singleton.gameObject.GetComponent<UnityTransport>();
			var allocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
			RelayServerData data = new(allocation, "dtls");
			transport.SetRelayServerData(data);
			return NetworkManager.Singleton.StartClient();
		}
		catch (RelayServiceException exc)
		{
			Debug.Log(exc);
		}
		catch(Exception exc)
		{
			Debug.Log(exc);
		}
		return false;
	}

	private Player getPlayer()
	{
		return new Player
		{
			Data = new Dictionary<string, PlayerDataObject>
				{
					{"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, PlayerName) }
				}
		};
	}
	StringBuilder logString = new StringBuilder();
	private void printLobbyPlayers(Lobby lobby)
	{
		logString.Clear();
		logString.AppendLine($"Players in Lobby {lobby.Name}: {lobby.Players.Count}");
		foreach (var p in lobby.Players)
		{
			logString.AppendLine($"\t{p.Id}: {p.Data["PlayerName"].Value}");
		}
		Debug.Log(logString.ToString());
	}
	private static void setTransportProtocol(ProtocolType protocol)
	{
		var transport = NetworkManager.Singleton.gameObject.GetComponent<UnityTransport>();
		var type = transport.GetType();
		var method = type.GetMethod("SetProtocol", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		method.Invoke(transport, new object[] { protocol });
	}
}
