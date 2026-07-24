// ============================================================================
// TalkScript.cs
// ----------------------------------------------------------------------------
// 대화 하나를 정의하는 ScriptableObject입니다. Talks(대사 한 줄)를 순서대로 나열한 배열이며,
// TalkManager가 이 배열을 순서대로(또는 선택지를 통해 index를 지정해서) 재생합니다.
//
// [Talks.index]
//   배열 안에서의 실제 위치(순번)와는 별개로, 각 Talks마다 직접 지정하는 고유 번호입니다.
//   선택지(Choice)는 이 index 값으로 "다음에 재생할 Talks"를 지정합니다 - 배열 순번이 아니라
//   이 값을 참조하기 때문에, 나중에 인스펙터에서 Talks 순서를 옮기거나 중간에 새 Talks를
//   끼워넣어도 이미 만들어둔 선택지의 목적지가 깨지지 않습니다. 같은 TalkScript 안에서 index
//   값은 서로 겹치면 안 됩니다(TalkManager가 재생을 시작할 때 중복을 검사해서 경고를 남깁니다).
//
// [기본 진행 순서 - 선택지가 없는 줄]
//   Choice가 없는 Talks는 재생이 끝나면 배열상 바로 다음 Talks로 자동 진행합니다(더 이상 다음
//   Talks가 없으면 대화 종료). 즉 "다음 줄로 넘어가기"는 배열 순서를, "선택지로 점프하기"는
//   index 값을 쓰는 방식입니다.
//
// [선택지 없이 여기서 바로 대화 끝내기 - endsConversation]
//   위 규칙 때문에, 배열 "맨 끝"이 아닌 중간의 Talks는 선택지 없이는 대화를 끝낼 방법이 없습니다 -
//   Advance()가 무조건 배열상 다음 Talks로 진행해버리기 때문입니다. 그런데 NPCTalker처럼 퀘스트
//   상태별(안 받음/진행 중/완료 보고 대기/완료) 대화 묶음을 전부 이 하나의 talks[] 배열 안에
//   이어붙여두고 index로 각 묶음의 시작 위치만 다르게 잡는 구조에서는, 각 묶음이 배열 끝이 아니라
//   중간에서 끝나야 하는 경우가 흔합니다(다음 묶음의 대사로 잘못 이어지면 안 되니까요). 이럴 때
//   선택지를 억지로 추가하지 않고도 이 Talks에서 대화를 바로 끝내고 싶다면 endsConversation을
//   켜두세요 - Advance()가 다음 Talks로 넘어가는 대신 곧바로 대화를 종료합니다. 선택지가 있는
//   Talks(HasChoices)에서는 이 값이 무시됩니다(선택지의 targetIndex = -1로 끝내세요).
//
// [Choice.targetIndex]
//   선택지를 고르면 그 값과 같은 index를 가진 Talks로 바로 이동합니다. -1이면 대화를 그대로
//   종료합니다. 자기 자신보다 앞선(또는 같은) index를 가리키면 원신 잡담 NPC처럼 같은 선택지
//   목록으로 되돌아가는 "루프형" 대화도 그대로 만들 수 있습니다. 선택지는 최대 3개를 권장합니다
//   (OnValidate가 그보다 많으면 경고를 남깁니다 - 강제로 자르지는 않습니다).
//
// [Talks.questToGrant / questToTurnIn - 선택지 없이 대사만으로 퀘스트 지급/보고]
//   Choice.questToGrant/questToTurnIn은 "그 선택지를 골라야만" 적용되지만, 이 둘은 Talks 자신에
//   달려 있어서 이 Talks가 시작되는 순간(플레이어가 아무 선택도 하지 않고 그냥 대사만 봐도)
//   TalkManager.GoToPosition()이 자동으로 적용합니다 - "말을 걸기만 해도 퀘스트를 준다" 같은 연출을
//   만들 때 씁니다. Choice 쪽과 완전히 같은 안전장치가 그대로 적용됩니다(QuestData가 null이면 무시,
//   이미 진행 중/완료된 퀘스트를 questToGrant에 넣어도 QuestManager가 경고만 남기고 조용히 무시,
//   requiresTurnIn 조건을 못 채운 채 questToTurnIn을 넣어도 마찬가지). 같은 Talks에 둘 다(그리고
//   Choice의 questToGrant/questToTurnIn까지) 동시에 넣어도 서로 방해하지 않습니다.
//
// [카메라 좌표 - anchor 기준 상대값]
//   cameraLocalPosition/cameraLocalEulerAngles는 월드 절대 좌표가 아니라, TalkManager.StartTalk()에
//   넘겨준 anchor Transform을 기준으로 한 상대 위치/회전입니다. 그래서 같은 TalkScript를 여러
//   NPC/여러 씬에 재사용해도 카메라 앵글이 항상 그 자리에서 올바르게 재현됩니다.
//
// [onTalkStart - 중요한 제약: 씬(Hierarchy) 오브젝트는 연결할 수 없습니다!]
// 이 줄이 재생되기 시작할 때 같이 실행할 UnityEvent입니다. 버튼 OnClick과 겉보기엔 똑같아 보이지만,
// 이 TalkScript는 **애셋**(ScriptableObject, Project 창에 있는 파일)이고, Unity는 애셋이 씬(Hierarchy)
// 안의 특정 오브젝트를 직접 참조하는 것을 허용하지 않습니다 - 그래서 Hierarchy에서 오브젝트를
// 드래그해도 슬롯에 안 들어가거나(또는 대신 프리팹 애셋 자체가 들어가 버리고), 실행해보면 그
// 프리팹 원본에다 대고 호출하려다 아무 효과가 없거나 에러가 납니다. 이 제약 때문에 "이 NPC의
// Animator에 Talk 트리거를 걸어줘" 같은, 특정 씬 인스턴스를 대상으로 하는 연출은 여기 onTalkStart로는
// 만들 수 없습니다 - 대신 각 Talks의 playTalkAnimationOnStart(bool, 순수 데이터라 애셋에 안전하게
// 저장됩니다)를 체크하면, NPCTalker가 TalkManager.OnTalkChanged를 구독해서 "자기 자신"의 Animator에
// 알아서 Talk 트리거를 걸어줍니다(NPCTalker.cs 참고) - 씬 오브젝트를 여기서 직접 참조할 필요가
// 없어서 이 제약을 피해갑니다.
// onTalkStart 자체는 씬 오브젝트가 아니라 "다른 애셋"이나 정적인 무언가를 다루는 용도로 남겨뒀지만,
// 실전에서 거의 쓸 일이 없다면 지워도 무방합니다. 비워두면 아무 것도 실행하지 않습니다.
//
// [애셋 만들기]
//   Project 창에서 우클릭 → Create → Dialogue > Talk Script 로 새 대화 애셋을 만드세요.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "Talk_New", menuName = "Dialogue/Talk Script")]
public class TalkScript : ScriptableObject
{
    [Serializable]
    public class Choice
    {
        [Tooltip("선택지로 보여줄 텍스트입니다.")]
        public string choiceText;
        [Tooltip("이 선택지를 고르면 이동할 Talks의 index입니다. -1이면 대화를 종료합니다.")]
        public int targetIndex = -1;

        [Tooltip("이 선택지를 고르면 지급할 퀘스트입니다(비워두면 아무 퀘스트도 지급하지 않습니다). " +
                  "QuestData는 TalkScript와 마찬가지로 애셋이라, 다른 애셋(QuestData)을 참조하는 건 " +
                  "안전합니다(onTalkStart처럼 씬 오브젝트를 직접 참조하는 게 아니라서 이 필드는 " +
                  "onTalkStart의 제약과 무관합니다) - 실제 지급은 TalkManager.SelectChoice()가 코드에서 " +
                  "QuestManager.Instance.AddQuest()를 호출하는 방식으로 처리합니다.")]
        public QuestData questToGrant;

        [Tooltip("이 선택지를 고르면 \"완료 보고\"할 퀘스트입니다(비워두면 아무 것도 보고하지 " +
                  "않습니다). QuestData.requiresTurnIn이 켜진 퀘스트를 여기 연결해두면, 목표를 다 채운 " +
                  "뒤 이 선택지를 골랐을 때 비로소 완료 처리/보상 지급이 이뤄집니다(아직 목표를 " +
                  "못 채웠으면 QuestManager가 경고만 남기고 아무 일도 일어나지 않습니다) - " +
                  "questToGrant와 마찬가지로 애셋-애셋 참조라 안전하며, 실제 처리는 " +
                  "TalkManager.SelectChoice()가 코드에서 QuestManager.Instance.TurnInQuest()를 호출하는 " +
                  "방식입니다.")]
        public QuestData questToTurnIn;
    }

    [Serializable]
    public class Talks
    {
        [Tooltip("이 Talks의 고유 번호입니다. 배열 순번과 달리 직접 지정하며, Choice.targetIndex가 " +
                  "이 값을 참조해서 점프할 대상을 찾습니다. 같은 TalkScript 안에서 겹치면 안 됩니다.")]
        public int index;

        [Header("카메라 (anchor 기준 상대 좌표)")]
        public Vector3 cameraLocalPosition;
        public Vector3 cameraLocalEulerAngles;

        [Header("대사")]
        public string speakerName;
        [TextArea(2, 5)]
        public string dialogueText;

        [Header("이 줄이 시작될 때 같이 실행 (애니메이션 재생 등, 비워둬도 됩니다)")]
        [Tooltip("[주의] 이 TalkScript는 애셋이라 Hierarchy의 씬 오브젝트를 직접 연결할 수 없습니다. " +
                  "NPC의 Talk 애니메이션을 틀고 싶다면 이 이벤트 대신 바로 아래 Play Talk Animation " +
                  "On Start 체크박스를 쓰세요.")]
        public UnityEvent onTalkStart;

        [Tooltip("켜두면 이 줄이 시작될 때 NPCTalker가 자기 Animator에 \"Talk\" 트리거를 자동으로 " +
                  "겁니다(NPCTalker가 TalkManager.OnTalkChanged를 구독해서 처리 - 위 onTalkStart와 " +
                  "달리 씬 오브젝트를 애셋에서 직접 참조하지 않아도 되는 방식입니다). NPC 대화가 " +
                  "아니라 다른 방식(예: 컷신 전용 스크립트)으로 재생되는 TalkScript라면 꺼두세요.")]
        public bool playTalkAnimationOnStart = false;

        [Tooltip("켜두면 이 줄이 끝났을 때(선택지가 없는 경우에 한해) 배열상 다음 Talks로 넘어가지 " +
                  "않고 곧바로 대화를 종료합니다. 여러 퀘스트 상태별 대화 묶음을 하나의 talks[] 배열에 " +
                  "이어붙여둔 경우, 각 묶음의 마지막 줄에 이 옵션을 켜서 다음 묶음의 대사로 잘못 이어지는 " +
                  "것을 막으세요. 선택지가 있는 Talks에서는 무시됩니다.")]
        public bool endsConversation = false;

        [Header("퀘스트 - 선택지 없이 이 줄이 시작되기만 해도 자동 지급/보고 (파일 상단 참고)")]
        [Tooltip("이 Talks가 시작되는 순간 자동으로 지급할 퀘스트입니다(비워두면 아무 것도 지급하지 " +
                  "않습니다) - Choice.questToGrant와 달리 선택지를 고를 필요 없이 이 대사가 나오기만 " +
                  "하면 QuestManager.Instance.AddQuest()가 호출됩니다.")]
        public QuestData questToGrant;
        [Tooltip("이 Talks가 시작되는 순간 자동으로 \"완료 보고\"할 퀘스트입니다(비워두면 아무 것도 " +
                  "보고하지 않습니다) - Choice.questToTurnIn과 달리 선택지 없이 이 대사가 나오기만 하면 " +
                  "QuestManager.Instance.TurnInQuest()가 호출됩니다. requiresTurnIn 조건(목표를 다 " +
                  "채운 상태)을 아직 못 채웠다면 QuestManager가 경고만 남기고 조용히 무시합니다.")]
        public QuestData questToTurnIn;

        [Header("선택지 (최대 3개 권장, 비어있으면 다음 Talks로 자동 진행)")]
        public Choice[] choices = new Choice[0];

        /// <summary>이 Talks에 선택지가 있는지 여부입니다. 있으면 자동 진행 대신 선택지를 골라야
        /// 다음으로 넘어갑니다(TalkManager.SelectChoice() 참고).</summary>
        public bool HasChoices => choices != null && choices.Length > 0;
    }

    [Tooltip("이 대화를 구성하는 Talks들입니다. 순서대로 배열해두면, 선택지가 없는 한 이 순서대로 " +
              "자동 진행됩니다.")]
    public Talks[] talks = new Talks[0];

    /// <summary>인스펙터에서 값을 바꿀 때마다 에디터가 호출합니다. 선택지가 3개를 넘는 Talks가
    /// 있으면(강제로 자르지는 않고) 경고만 남겨서 실수로 너무 많이 넣었는지 바로 알 수 있게 합니다.</summary>
    private void OnValidate()
    {
        if (talks == null) return;

        foreach (Talks talk in talks)
        {
            if (talk != null && talk.choices != null && talk.choices.Length > 3)
            {
                Debug.LogWarning($"[TalkScript] '{name}'의 Talks(index={talk.index})에 선택지가 " +
                                  $"{talk.choices.Length}개 있습니다 - 3개 이하를 권장합니다.", this);
            }
        }
    }
}