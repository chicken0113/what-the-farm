# 농사

Unity 6 기반 3D 게임 프로젝트입니다. 기본 3D 템플릿과 Universal Render Pipeline(URP)을 사용합니다.

## 시작하기

1. Unity Hub에서 이 폴더를 **Add project**로 등록합니다.
2. Unity **6000.3.25f1**로 엽니다. 첫 실행 시 패키지 가져오기에 시간이 걸릴 수 있습니다.
3. `Assets/Scenes/SampleScene.unity`를 열어 시작합니다.

## 저장소 규칙

- `Assets/`, `Packages/`, `ProjectSettings/`와 각 에셋의 `.meta` 파일을 함께 커밋합니다.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` 등 Unity가 다시 만드는 파일은 커밋하지 않습니다.
- 씬과 프리팹은 텍스트 직렬화, 메타 파일은 표시 모드로 설정되어 있습니다.
- 큰 바이너리 에셋이 많아지면 Git LFS 도입을 검토합니다.

현재 원격 저장소는 연결하지 않았습니다. GitHub 등 사용할 주소가 정해지면 `git remote add origin <주소>`로 연결할 수 있습니다.
