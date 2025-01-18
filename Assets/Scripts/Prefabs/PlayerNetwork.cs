using Unity.Collections;
using Unity.Netcode;

public class PlayerNetwork : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> currentRoomID;
    public NetworkVariable<bool> isRoomHost;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // When the player spawns, they are in the main room list (no room)
            currentRoomID.Value = "";
        }
    }

    public void SetCurrentRoomID(string roomID)
    {
        if (IsServer)
        {
            currentRoomID.Value = roomID;
        }
        else
        {
            RequestSetCurrentRoomIDServerRpc(roomID);
        }
    }

    [ServerRpc]
    private void RequestSetCurrentRoomIDServerRpc(string roomID)
    {
        currentRoomID.Value = roomID;
    }

    public void SetIsRoomHost(bool isHost)
    {
        if (IsServer)
        {
            isRoomHost.Value = isHost;
        }
        else
        {
            RequestSetIsRoomHostServerRpc(isHost);
        }
    }

    [ServerRpc]
    private void RequestSetIsRoomHostServerRpc(bool isHost)
    {
        isRoomHost.Value = isHost;
    }

    public void LeaveRoom()
    {
        RoomManager.Instance.LeaveRoomServerRpc();
    }
}