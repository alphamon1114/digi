# 결정 기록

## D001 · 2026-09-16 · 사용자 확정

- 추가: 사용자가 지정한 비대칭 PvP 규칙과 지속 문서 관리 규칙.
- 변경 전: URP 기본 템플릿 두 개, 게임 규칙/구현 없음.
- 변경 후: GAME_DESIGN의 사용자 확정 항목을 구현 기준으로 사용.
- 이유: 이 작업의 명시적 사용자 요구.
- 관련: `AGENTS.md`, `docs/GAME_DESIGN.md`, `docs/HANDOFF.md`.
- 영향: 1대3 시험, 서버 권한, 악당 전용 정보 격리가 핵심 제약. 기존 게임 저장 데이터 없음.
- 검증: 프로젝트/패키지/씬/코드 목록 확인. 런타임 검증은 구현 후 기록.

## D002 · 2026-09-16 · 개발상 임시

- 추가: `digi/`를 작업 프로젝트로 선택. Unity 6000.6.0f1과 기존 URP/Input System 유지. NGO 2.7.0을 도입하고 입력 요청/수신자별 상태 스냅샷은 NGO CustomMessagingManager로 전송한다.
- 변경 전: 네트워크, 플레이 씬, 게임 로직 없음. `.gitignore`는 루트 Unity 프로젝트만 가정.
- 변경 후: 독립 Prototype 씬과 서버 시뮬레이션, Inspector 설정, 기본 도형 표현. 중첩 프로젝트 캐시도 Git에서 제외.
- 이유: 기존 커스텀 코드/에셋이 없고, 최소 프로토타입에서 프리팹 동기화보다 작은 명시적 서버 상태를 검증하기 쉽다. Unity 공식 문서에서 NGO 2.7.0의 Unity 6 지원을 확인했으며 실제 패키지 해결/컴파일로 확인할 예정.
- 임시 규칙: GAME_DESIGN의 D002 기본값 전체. 한 판 종료, 사냥으로 잠기지 않는 구조 경로, 혼자 시험 가능한 선택형 봇을 위한 개발 판단이며 사용자 의도로 해석하지 않는다.
- 관련: `digi/Assets/Prototype/`, `digi/Packages/manifest.json`, `.gitignore`.
- 영향: 기존 SampleScene/다른 프로젝트 보존. 전용 서버/인터넷 매칭/저장 호환성/클라이언트 예측은 범위 밖. LAN 또는 직접 IP 접속.
- 검증: 구현 후 HANDOFF에 실제 결과 기록. 공식 참고: https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.netcode.gameobjects.html

## D003 · 2026-09-16 · 개발상 임시 (D002의 NGO 버전 선택을 대체)

- 변경 전: NGO 2.7.0 (Unity 6.0 공식 호환 문서 기준).
- 변경 후: NGO 2.13.2. Unity 엔진은 6000.6.0f1 그대로 유지.
- 이유: 실제 Unity 6.6 컴파일에서 2.7.0 패키지의 EntityId → int 변환이 CS0619 오류. 설치된 6000.6.0f1 Editor의 PackageManager/Editor/manifest.json이 명시한 최소/번들 버전 2.13.2를 선택했다. 일반적인 Unity 6 지원과 현재 세부 버전 지원은 다름을 확인.
- 관련: `digi/Packages/manifest.json`, `digi/Packages/packages-lock.json`.
- 영향: NGO API/전송 구현만 교체, 게임 규칙/기존 데이터 변경 없음. D002의 나머지 선택은 유지.
- 검증: 2.7.0 컴파일 실패를 실제 확인. 2.13.2 재컴파일 결과는 HANDOFF에 기록.

## D004 · 2026-09-16 · 개발상 임시

- 추가/수정: 결정적 서버 회귀 검증, 실제 4프로세스 네트워크 스모크, 셰이더의 직렬화 참조, 무그래픽 실행 시 표현 생략, 종료 후 접속 종료가 결과 상태를 바꾸지 않도록 수정.
- 변경 전: 동적 Shader.Find만 사용하여 실제 실행 파일의 무그래픽 실행에서 null material 초기화 오류. 전송 크기에 맞춘 분할 스냅샷과 실제 연결 검증이 필요했음.
- 변경 후: Rules.asset이 URP Lit 셰이더를 직접 참조하여 빌드 포함을 보장. 무그래픽 실행에서도 동일한 서버/입력/네트워크 로직은 실행하고 카메라·재질·음향만 생략. 큰 상태 스냅샷은 ReliableFragmentedSequenced 전송. 초기 카메라는 맵 안쪽 방향.
- 이유: 실제 빌드/실행에서 발견한 문제 해결과 재현 가능한 검증. 게임 규칙 변경은 아님.
- 관련: `digi/Assets/Prototype/PrototypeSession.cs`, `digi/Assets/Prototype/PrototypeConfig.cs`, `digi/Assets/Prototype/Rules.asset`, `digi/Assets/Prototype/Editor/`, `scripts/`.
- 영향: 에셋 참조 및 검증 진입점 추가. 기존 씬/세이브 손상 없음. 개발용 실행 인수는 자동 입력과 로그/스크린샷 출력에만 사용되며 게임 상태를 직접 주입하지 않는다.
- 검증: 서버 통합 단언 33개와 Windows 빌드 성공. 다중 접속/화면 확인 최종 결과는 HANDOFF에 기록.

## D005 · 2026-09-16 · 개발상 임시 (D002의 초기 맵·초반 악당 수치를 대체)

- 계기: 사용자가 좁은 전장과 상호 노출로 진화 전 악당이 불리하다고 플레이 피드백을 제공했다. 아래 수치는 그 피드백에 대응한 개발상 시험값이며 사용자 확정 수치가 아니다.
- 변경 전: 60×60, 소형 기둥5개, 진영 시작점 약33 간격, 멀리 있는 액터도 렌더링. 초기 악당 속도5.3, 첫 진화120 (야생3마리 필요, 한 거점은2마리).
- 변경 후: 120×120, 직경10/높이9 차폐물9개, 시작점 약103 간격, 북서/북동/남쪽 거점. 시야32+차폐 검사 및 안개, 이름표18. 초기 악당 속도6.2, 첫 진화80. 상세 현재 값은 GAME_DESIGN D005 및 Rules.asset.
- 이유: 초반 사냥 시간·추격 단절·우회 접근 기회를 제공하고 성장기3명이 처음부터 한 화면에서 악당을 압박하는 상황을 줄이려는 개발 판단. 체력·공격력까지 동시에 바꾸지 않아 변화 원인을 비교하기 쉽게 한다.
- 관련: `digi/Assets/Prototype/PrototypeConfig.cs`, `MatchSimulation.cs`, `PrototypeSession.cs`, `Rules.asset`, `Prototype.unity`, `Editor/PrototypeVerification.cs`, `scripts/NetworkSmoke.ps1`.
- 영향: 기존 룰/진화 영속성/팀XP/3거점/대피 보고의 비위치성은 유지. 서버 장애물 판정과 봇 이동도 확대 지형에 맞춤. 새 필드는 기존 에셋에 명시적으로 추가하고 .meta GUID는 유지. 로컬 빌드를 갱신해 전 참가자가 동일 버전을 실행해야 한다. 이동시간 증가로 네트워크 스모크 대기 상한150초로 조정.
- 검증: 확대맵 경계·차폐·초기 분리·한 거점 진화·봇 완주를 포함한 단언42개, Unity 컴파일/Windows 빌드, 실제 호스트+3클라이언트 구조 승리(게임 시간65.4초)와 최종 상태 일치, Direct3D 11 맵 렌더 확인. 상세 HANDOFF 참조. 실제 1대3 밸런스 개선 정도는 사용자 재플레이로 확인 필요.
