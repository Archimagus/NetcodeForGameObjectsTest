using TMPro;
using Unity.Netcode;

public class PlayerLabel : NetworkBehaviour
{
	private TextMeshProUGUI _text;

	public void SetPlayerName(string playerName)
	{
		_text.text = playerName;
	}
	private void Awake()
	{
		_text = GetComponent<TextMeshProUGUI>();
	}
}
