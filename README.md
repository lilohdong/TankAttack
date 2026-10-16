# Tank Attack

턴제 + 지형 파괴 탱크 대전 게임 (Unity + Photon PUN 2, 최대 4인 개인전)

- 로비 : 닉네임, 방 목록, 방 만들기, 랜덤 참가
- 방 : 준비, RGB 탱크 색상, 방장이 게임 시작
- 배틀 : 턴마다 이동(거리 제한) + 행동 1번 (기본탄 / 레이저 / 대공탄 / 회복 / 정찰)
  - 포탄이 떨어진 곳의 지형이 파임 (포트리스 스타일)
  - 모두가 포탄을 3인칭으로 따라가는 카메라
  - 미니맵에는 내 탱크만, 정찰하면 다음 턴까지 적 위치 표시
  - 마지막까지 살아남은 1명 승리

## 실행 환경
- Unity 6000.5.10f1
- Photon PUN 2 (2.56)

## 처음 받았을 때 : Photon AppId 설정

이 저장소에는 Photon AppId가 들어 있지 않습니다. 직접 만든 AppId를 넣어야 접속됩니다.

1. https://dashboard.photonengine.com 에서 **Photon PUN** 타입 앱을 만들고 AppId 복사
2. Unity 메뉴 **Window → Photon Unity Networking → Highlight Server Settings**
3. **App Id PUN** 에 붙여넣기 (Fixed Region 은 `kr` 로 되어 있음)
4. AppId가 실수로 커밋되지 않도록, 터미널에서 한 번 실행
   ```
   git update-index --skip-worktree Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset
   ```
   (서버 설정을 정말로 바꿔서 커밋해야 할 때는 `--no-skip-worktree` 로 풀고, AppId를 지운 뒤 커밋)

## 혼자 테스트
`Assets/01.Scenes/scBattleField` 를 열고 Play → 서버 접속 없이 오프라인 모드로 플레이

## 문서
- [Docs/01_Lobby_Setup.md](Docs/01_Lobby_Setup.md) : 로비
- [Docs/02_Room_Setup.md](Docs/02_Room_Setup.md) : 방 화면
- [Docs/03_Battle_Setup.md](Docs/03_Battle_Setup.md) : 배틀필드 (규칙, 조작, 네트워크 구조)
