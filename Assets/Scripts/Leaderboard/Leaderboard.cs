using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Leaderboard : MonoBehaviour
{
    [Header("UI References")]
    public Transform leaderboardListContainer;
    public GameObject leaderboardEntryPrefab;

    private bool _dataLoaded;
    private const float RetryInterval = 1f;
    private const float MaxWaitTime = 10f;
    private float _startTime;

    private void Start()
    {
        _startTime = Time.time;
        StartCoroutine(LoadLeaderboardWithRetry());
    }

    #region Leaderboard Loading
    private IEnumerator LoadLeaderboardWithRetry()
    {
        while (!_dataLoaded && (Time.time - _startTime) < MaxWaitTime)
        {
            if (PhotonNetwork.CurrentRoom?.CustomProperties == null)
            {
                yield return new WaitForSeconds(RetryInterval);
                continue;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("LeaderboardData", out var leaderboardDataObj)
                || leaderboardDataObj is not Hashtable leaderboardHashtable)
            {
                yield return new WaitForSeconds(RetryInterval);
                continue;
            }

            var leaderboardData = new Dictionary<int, PlayerResultData>();
            foreach (var entry in leaderboardHashtable)
            {
                var playerID = (int)entry.Key;
                var playerDataHashObj = entry.Value;

                if (playerDataHashObj is not Hashtable playerDataHash)
                {
                    Debug.LogError($"Entry Value for Player {playerID} is NOT a Hashtable! Type: {playerDataHashObj.GetType()}");
                    continue;
                }

                var playerName = GetPlayerNameFromHash(playerDataHash, playerID);
                var finishTime = GetFinishTimeFromHash(playerDataHash, playerID);
                leaderboardData.Add(playerID, new PlayerResultData(playerName, finishTime));
            }

            PopulateLeaderboard(leaderboardData);
            _dataLoaded = true;
            yield break;
        }

        if (!_dataLoaded)
            Debug.LogError("Failed to load leaderboard data after multiple retries.");
    }

    private static string GetPlayerNameFromHash(Hashtable playerDataHash, int playerID)
    {
        if (!playerDataHash.TryGetValue("playerName", out var playerNameObj))
        {
            Debug.LogError($"playerName key not found in playerDataHash for Player {playerID}");
            return string.Empty;
        }

        if (playerNameObj is string s)
            return s;

        Debug.LogError($"playerName is not a string! Type: {playerNameObj.GetType()}");
        return string.Empty;
    }

    private static float GetFinishTimeFromHash(Hashtable playerDataHash, int playerID)
    {
        if (!playerDataHash.TryGetValue("finishTime", out var finishTimeObj))
        {
            Debug.LogError($"finishTime key not found in playerDataHash for Player {playerID}");
            return 0f;
        }

        switch (finishTimeObj)
        {
            case float timeFloat:
                return timeFloat;
            case double timeDouble:
                return (float)timeDouble;
            default:
                Debug.LogError($"finishTime is not a float or double! Type: {finishTimeObj.GetType()}");
                return 0f;
        }
    }

    private void PopulateLeaderboard(Dictionary<int, PlayerResultData> leaderboardData)
    {
        var sortedLeaderboard = new List<KeyValuePair<int, PlayerResultData>>(leaderboardData);
        sortedLeaderboard.Sort((pair1, pair2) => pair1.Value.finishTime.CompareTo(pair2.Value.finishTime));

        var position = 1;
        foreach (var playerData in sortedLeaderboard.Select(entry => entry.Value))
        {
            var entryInstance = CreateLeaderboardEntry();
            if (!entryInstance)
                return;

            var texts = entryInstance.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length == 3)
            {
                texts[0].text = position + ".";
                texts[1].text = ShortenName(playerData.playerName);
                texts[2].text = playerData.finishTime.ToString("F2") + "s";
            }
            else
                Debug.LogError("Leaderboard Entry prefab/structure does not have 3 TextMeshPro Text objects!");

            entryInstance.SetActive(true);
            position++;
        }
    }

    private GameObject CreateLeaderboardEntry()
    {
        if (leaderboardEntryPrefab)
            return Instantiate(leaderboardEntryPrefab, leaderboardListContainer);

        if (leaderboardListContainer.childCount > 0)
            return Instantiate(leaderboardListContainer.GetChild(0).gameObject, leaderboardListContainer);

        Debug.LogError("No Leaderboard Entry Prefab assigned and no entry in scene to duplicate!");
        return null;
    }
    #endregion

    #region Helpers
    private static string ShortenName(string playerName)
    {
        if (playerName.Length <= 13)
            return playerName;

        return playerName[..12] + "..";
    }
    #endregion
}
