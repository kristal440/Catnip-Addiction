using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Leaderboard : MonoBehaviour
{
    [Header("UI References")]
    public Transform leaderboardListContainer;
    public GameObject leaderboardEntryPrefab;

    private bool _dataLoaded; // Flag to track if the leaderboard data has been loaded.
    private const float RetryInterval = 1f; // Time interval between retries in seconds
    private const float MaxWaitTime = 10f; // Maximum time to wait for the data to load in seconds
    private float _startTime; // Time when we started trying to load the data.

    private void Start()
    {
        _startTime = Time.time;
        StartCoroutine(LoadLeaderboardWithRetry());
    }

    private IEnumerator LoadLeaderboardWithRetry()
    {
        while (!_dataLoaded && (Time.time - _startTime) < MaxWaitTime)
        {
            if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties != null) // Ensure Room and Custom Properties are not null
            {
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("LeaderboardData", out var leaderboardDataObj))
                {
                    if (leaderboardDataObj != null) // Check if the value returned is not null.
                    {
                        //Debug.Log("Leaderboard data FOUND in Custom Room Properties.");
                        //Debug.Log($"Data Type: {leaderboardDataObj.GetType()}");

                        if (leaderboardDataObj is Hashtable leaderboardHashtable)
                        {
                            //Debug.Log("Leaderboard data is of type Hashtable.");
                            var leaderboardData = new Dictionary<int, PlayerResultData>();

                            foreach (var entry in leaderboardHashtable)
                            {
                                var playerID = (int)entry.Key;
                                var playerDataHashObj = entry.Value;

                                //Debug.Log($"Entry Value Type for Player {playerID}: {playerDataHashObj.GetType()}");

                                if (playerDataHashObj is Hashtable playerDataHash)
                                {
                                    //Debug.Log($"Entry Value for Player {playerID} is also a Hashtable.");

                                    var playerName = "";
                                    var finishTime = 0f;

                                    if (playerDataHash.TryGetValue("playerName", out var playerNameObj))
                                    {
                                        if (playerNameObj is string s)
                                        {
                                            playerName = s;
                                        }
                                        else
                                        {
                                            Debug.LogError($"playerName is not a string! Type: {playerNameObj.GetType()}");
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogError($"playerName key not found in playerDataHash for Player {playerID}");
                                    }

                                    if (playerDataHash.TryGetValue("finishTime", out var finishTimeObj))
                                    {
                                        switch (finishTimeObj)
                                        {
                                            // Check if it's a float (System.Single)
                                            case float timeFloat:
                                                finishTime = timeFloat;
                                                //Debug.Log($"finishTime is a float: {finishTime}");
                                                break;
                                            // Also handle double just in case
                                            case double timeDouble:
                                                finishTime = (float)timeDouble;
                                                //Debug.Log($"finishTime is a double (converted to float): {finishTime}");
                                                break;
                                            default:
                                                Debug.LogError($"finishTime is not a float or double! Type: {finishTimeObj.GetType()}");
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogError($"finishTime key not found in playerDataHash for Player {playerID}");
                                    }

                                    leaderboardData.Add(playerID, new PlayerResultData(playerName, finishTime));
                                }
                                else
                                {
                                    Debug.LogError($"Entry Value for Player {playerID} is NOT a Hashtable! Type: {playerDataHashObj.GetType()}");
                                }
                            }

                            var sortedLeaderboard = new List<KeyValuePair<int, PlayerResultData>>(leaderboardData);
                            sortedLeaderboard.Sort((pair1, pair2) => pair1.Value.finishTime.CompareTo(pair2.Value.finishTime));

                            var position = 1;
                            foreach (var entry in sortedLeaderboard)
                            {
                                var playerData = entry.Value;
                                GameObject entryInstance;

                                if (leaderboardEntryPrefab != null)
                                {
                                    entryInstance = Instantiate(leaderboardEntryPrefab, leaderboardListContainer);
                                }
                                else
                                {
                                    if (leaderboardListContainer.childCount > 0)
                                    {
                                        entryInstance = Instantiate(leaderboardListContainer.GetChild(0).gameObject, leaderboardListContainer);
                                    }
                                    else
                                    {
                                        Debug.LogError("No Leaderboard Entry Prefab assigned and no entry in scene to duplicate!");
                                        yield break; // Exit coroutine if no prefab and no existing entries.
                                    }
                                }

                                var texts = entryInstance.GetComponentsInChildren<TextMeshProUGUI>();
                                if (texts.Length == 3)
                                {
                                    texts[0].text = position + ".";
                                    texts[1].text = ShortenName(playerData.playerName);
                                    texts[2].text = playerData.finishTime.ToString("F2") + "s";
                                }
                                else
                                {
                                    Debug.LogError("Leaderboard Entry prefab/structure does not have 3 TextMeshPro Text objects!");
                                }
                                entryInstance.SetActive(true);
                                position++;
                            }
                            _dataLoaded = true; // Set the flag to true to exit the loop.
                            yield break; // Exit the coroutine, no need to retry.

                        }

                        Debug.LogError("Leaderboard data is not of the expected type (ExitGames.Client.Photon.Hashtable)");
                    }
                    else
                    {
                        Debug.Log("LeaderboardData is null. Retrying...");
                    }
                }
                else
                {
                    Debug.Log("Leaderboard data not found in Custom Room Properties. Retrying...");
                }
            }
            else
            {
                Debug.Log("PhotonNetwork.CurrentRoom or CustomProperties is null. Retrying...");
            }

            yield return new WaitForSeconds(RetryInterval);
        }

        if (!_dataLoaded)
        {
            Debug.LogError("Failed to load leaderboard data after multiple retries.");
        }
    }

    private static string ShortenName(string playerName)
    {
        return playerName.Length switch
        {
            <= 13 => playerName,
            > 13 => playerName[..12] + ".."
        };
    }
}