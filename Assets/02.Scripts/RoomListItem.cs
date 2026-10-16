using System;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//로비 방 목록의 한 줄 (프리팹에 붙임)
public class RoomListItem : MonoBehaviour
{
    public TMP_Text roomNameText;
    public TMP_Text playerCountText;
    public Button joinButton;

    //LobbyManager가 방 정보와 클릭 시 실행할 함수를 넘겨줌
    public void Setup(RoomInfo info, Action<string> onClick)
    {
        roomNameText.text = info.Name;

        bool isFull = info.PlayerCount >= info.MaxPlayers;
        if (!info.IsOpen)
            playerCountText.text = "게임 중";
        else
            playerCountText.text = $"{info.PlayerCount} / {info.MaxPlayers}";

        //가득 찼거나 게임 중인 방은 클릭할 수 없게
        joinButton.interactable = info.IsOpen && !isFull;
        joinButton.onClick.AddListener(() => onClick(info.Name));
    }
}
