# 배운 점

한 줄에 하나. 겹치면 합치고, 틀린 건 지워요.

- 시트를 고치면 `data_balance.csv`와 `Data/Generated/BalanceConfig.asset`을 같이 커밋해요. 새 키만 보지 말고 바뀐 값도 asset에서 확인해요 (7a210d8에서 빠져 오크 사거리가 옛 값 8로 남았어요).
- 시트 가져오기는 두 번 돌리고, 바꾼 키 몇 개의 값을 asset에서 grep으로 확인해요.
- 에이전트가 넘긴 CSV 행 파일은 삭제 목록 형식(주석인지 키인지)을 확인하고, 반영 뒤 지운 키가 정말 없어졌는지 봐요.
- `unity test --filter`는 정규식이라 `(` `)`를 `\(` `\)`로 써요. 결과의 `tests="N"`이 0이면 아무것도 안 돈 거예요.
- 테스트가 실패하면 "원래 있던 실패"로 넘기기 전에 기획 변경으로 기대값이 낡은 건지 먼저 봐요 (보상 방 전투 없음, 기물 102종).
- 엄폐물 · 거리 테스트는 한 가지 배치만 쓰지 말고 거리를 바꿔 봐요 (담 뒤 3칸은 통과, 3.5칸은 근접 적이 멈춤).
- 병합 전에 아트 브랜치가 더하는 파일이 main의 미추적 파일과 겹치는지 봐요. 겹치면 내용을 비교하고 백업한 뒤 치워요 (씬 빌드가 만든 재질은 GUID가 달라요).
- 아트 세션이 로컬 main에 직접 병합할 수 있어요. 푸시 전에 `git log origin/main..main`으로 무엇이 같이 올라가는지 봐요.
- 씬 빌드 결과물(재질 · 컨트롤러 · 프리팹)은 이제 같은 입력이면 같은 파일이 나와서 커밋해 둬요 (95c3137). 씬 · ProjectSettings · Assets/Settings · Telegraph 재질은 커밋하지 않아요.
- 빌드 코드에서 에셋을 지우고 새로 만들면 GUID · fileID가 매번 바뀌어요. 내용을 견줘 같으면 그대로 두는 BeginController/FinishController · SaveStablePrefab을 써요.
- 적 공격 클립은 기본 방식에서 예고 동안 첫 프레임에 멈췄다가 활성에 재생돼요. 첫 프레임 = 예고 자세, 판정 자세는 몇 프레임 안에. 예고 동안 움직이려면 MoveMode.Play.
- Unity MCP는 `unity mcp`가 아니라 AI Assistant 패키지의 릴레이 `%USERPROFILE%\.unity\relay\relay_win.exe --mcp --project-path <프로젝트>`로 붙어요 (`unity mcp`는 도구 0개). `~/.claude.json`의 `unity-editor-mcp`를 쓰고, 프로젝트 경로가 없는 데스크톱 확장 `unity-mcp`는 쓰지 않아요. 세션 도구 목록은 시작할 때만 받아서 설정을 바꾸면 새 대화에서 보이고, 안 보이면 에디터의 Project Settings > AI > Unity MCP Server가 Running인지 봐요.
- 아트 시안을 맡기기 전에 그 오브젝트가 Play 화면에서 실제로 보이는지 확인해요. 편집 화면과 게임 화면이 달라요: 바닥은 InkRegionLook이 지역 텍스처로 바꾸고 (죽림 = Floor_BambooGrove), 죽림 방은 ShowBushes가 풀숲을 꺼요 (덤불 시안 3~5차가 헛걸음).
- Unity MCP `Unity_RunCommand` 안에서 Undo.RevertAllDownToGroup은 되돌리지 못해요. 열린 씬을 잠깐 바꿔 캡처할 땐 미리 씬이 저장된 상태인지 보고, 끝나면 `EditorSceneManager.OpenScene`으로 다시 불러와요 (delayCall은 에디터가 뒤에 있으면 안 돌아요).
- 주인공 동작 클립을 새로 만들면 PlayerRig.cs의 반복(loopTime) 목록에도 넣어요. 빠지면 한 번 재생하고 멈춰요 (IdleBare·RunBare: 걷다가 1~2초 뒤 다리가 멈췄어요).
- PowerShell `Get-Content`는 `-Encoding UTF8` 없이 쓰면 한글이 깨져요. 중괄호가 든 치환은 perl 대신 Edit 도구로 해요.
- 런타임 스크립트가 공유 재질 에셋(바닥 Ink3D_Floor 등)을 바꿀 때, 원래 값은 Start에서 재질을 읽지 말고 씬 빌드 때 적어 둬요. 이전 실행이 되돌리지 못한 값이 원래 값이 되어 곱이 쌓여요 (타일 4 → 62.5). 바꾼 뒤엔 EditMode 테스트를 두 번 돌려 재질이 그대로인지 봐요.
