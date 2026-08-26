using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneNavigation : MonoBehaviour
{
    private const string LobbySceneName = "LobbyScene";
    private const string BattleSceneName = "SampleScene";

    public void OpenLobby()
    {
        SceneManager.LoadScene(LobbySceneName);
    }

    public void StartBattle()
    {
        SceneManager.LoadScene(BattleSceneName);
    }
}
