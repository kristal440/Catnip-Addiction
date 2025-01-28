using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class Launcher : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private GameObject controlPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private GameObject roomListPanel;
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private Transform roomsContainer;
    public Slider slider;

    private bool _isConnecting;
    private List<string> _roomLst;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    private void Start()
    {
        roomListPanel.SetActive(false);
        controlPanel.SetActive(false);
        loadingPanel.SetActive