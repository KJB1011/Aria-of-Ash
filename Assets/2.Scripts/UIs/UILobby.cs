// ============================================================================
// UILobby.cs
// ----------------------------------------------------------------------------
// LobbyScene ÀüÃ¼¸¦ ´ã´çÇÏ´Â ½ºÅ©¸³Æ®ÀÔ´Ï´Ù. ¾ÆÀÌµğ ÀÔ·ÂÃ¢ÀÇ °ªÀ» °ËÁõÇØ¼­ °ÔÀÓ ½ÃÀÛ ¹öÆ°ÀÇ È°¼ºÈ­
// »óÅÂ¸¦ °ü¸®ÇÏ°í, ¹öÆ°À» ´©¸£¸é ±× ¾ÆÀÌµğ¸¦ GameManager¿¡ ÀúÀåÇÑ µÚ È­¸éÀ» ÆäÀÌµå ¾Æ¿ôÇÏ°í
// IngameSceneÀ¸·Î ÀÌµ¿½ÃÅµ´Ï´Ù. ½ÇÁ¦ ÆäÀÌµå/¾À ÀüÈ¯Àº »õ·Î ¸¸µéÁö ¾Ê°í GameManager.LoadSceneWithFade()¸¦
// ±×´ë·Î Àç»ç¿ëÇÕ´Ï´Ù(GameManager.cs »ó´Ü [ÀÏ¹İÀûÀÎ ¾À ÀüÈ¯] Âü°í).
//
// [¾ÆÀÌµğ ÀÔ·Â - ½Ç½Ã°£ °ËÁõ]
//   Awake()¿¡¼­ idInputField.characterLimitÀ» maxIdLength·Î ¸ÂÃç¼­, 12±ÛÀÚ¸¦ ³Ñ¾î°¡´Â ¼ø°£ºÎÅÍ´Â
//   Å°º¸µå·Î ´õ ÀÔ·ÂÇØµµ(ºÙ¿©³Ö±â Æ÷ÇÔ) ¾Æ¿¹ ¾ÃÇô¼­ µé¾î°¡Áö ¾Ê½À´Ï´Ù - ±ÛÀÚ¼ö Á¶°ÇÀº ÀÌ·¸°Ô ÀÔ·Â
//   ´Ü°è¿¡¼­ºÎÅÍ ¸·½À´Ï´Ù. ±×¿Í º°°³·Î idInputField.onValueChanged¸¦ ±¸µ¶ÇØ¼­ ±ÛÀÚ°¡ ¹Ù²ğ ¶§¸¶´Ù
//   ¾Æ·¡ Á¶°ÇÀ» °Ë»çÇÕ´Ï´Ù(±ÛÀÚ¼ö ÃÊ°ú °Ë»çµµ ÄÚµå¿¡ ±×´ë·Î ³²°Üµ×½À´Ï´Ù - characterLimitÀ» ³ªÁß¿¡
//   ÀÎ½ºÆåÅÍ¿¡¼­ Áö¿ö¹ö¸®°Å³ª ÄÚµå·Î ÅØ½ºÆ®¸¦ Á÷Á¢ ´ëÀÔÇÏ´Â µî, UI ·¹º§ÀÇ Á¦ÇÑÀ» ¿ìÈ¸ÇÏ´Â °æ¿ì¿¡µµ
//   ¹æ¾îÇÒ ¼ö ÀÖ´Â ÀÌÁß ¾ÈÀüÀåÄ¡ÀÔ´Ï´Ù):
//     1) ºñ¾îÀÖÀ½ ¡æ °ÔÀÓ ½ÃÀÛ ¹öÆ° ºñÈ°¼ºÈ­, »óÅÂ ¹®±¸´Â ¼û±è(¾ÆÁ÷ ¾Æ¹« ÀÔ·Âµµ ¾È ÇÑ »óÅÂ¶ó ±»ÀÌ
//        ¿¡·¯Ã³·³ º¸ÀÏ ÇÊ¿ä°¡ ¾ø½À´Ï´Ù).
//     2) maxIdLength(±âº» 12)ÀÚ ÃÊ°ú ¡æ °ÔÀÓ ½ÃÀÛ ¹öÆ° ºñÈ°¼ºÈ­ + tooLongMessageFormat Ç¥½Ã(Æò¼Ò
//        Å¸ÀÌÇÎÀ¸·Î´Â characterLimit ¶§¹®¿¡ °ÅÀÇ ¹ß»ıÇÏÁö ¾Ê°í, À§¿¡¼­ ¸»ÇÑ ¿ìÈ¸ »óÈ²¿¡¼­¸¸ ¶å´Ï´Ù).
//     3) ¿µ¾î(A-Z, a-z)/ÇÑ±Û(¿Ï¼ºÇü À½Àı + ÀÚ¸ğ) ÀÌ¿ÜÀÇ ±ÛÀÚ Æ÷ÇÔ ¡æ °ÔÀÓ ½ÃÀÛ ¹öÆ° ºñÈ°¼ºÈ­ +
//        invalidCharsMessage Ç¥½Ã.
//     4) À§ Á¶°Ç¿¡ ¸ğµÎ ÇØ´çÇÏÁö ¾ÊÀ½(=À¯È¿) ¡æ °ÔÀÓ ½ÃÀÛ ¹öÆ° È°¼ºÈ­, »óÅÂ ¹®±¸ ¼û±è.
//
// [¾À ÁØºñ]
//   1) LobbyScene¿¡ ¾ÆÀÌµğ ÀÔ·ÂÃ¢(TMP_InputField), ±× ¾Æ·¡ »óÅÂ ¾È³» ÅØ½ºÆ®(TextMeshProUGUI, Æò¼Ò¿£
//      ºñÈ°¼ºÈ­ »óÅÂ·Î µÖµµ µË´Ï´Ù - ½ºÅ©¸³Æ®°¡ ¾Ë¾Æ¼­ ÄÑ°í ²ü´Ï´Ù), GameStart ¹öÆ°(Button)À» ¸¸µå¼¼¿ä.
//   2) ÀÌ ½ºÅ©¸³Æ®¸¦ ¾Æ¹« ¿ÀºêÁ§Æ®¿¡³ª ºÙÀÎ µÚ(ºó ¿ÀºêÁ§Æ®µµ ¹«¹æÇÕ´Ï´Ù) Id Input Field/Id Status
//      Text/Game Start Button ¼¼ ÇÊµå¿¡ °¢°¢ ¿¬°áÇÏ¼¼¿ä.
//   3) GameStart ¹öÆ°ÀÇ OnClick¿¡ ÀÌ ÄÄÆ÷³ÍÆ®ÀÇ ClickGameStartButton()À» ¿¬°áÇÏ¼¼¿ä - ¹öÆ°ÀÌ
//      ºñÈ°¼ºÈ­(Interactable = false)µÇ¾î ÀÖ´Â µ¿¾ÈÀº À¯´ÏÆ¼°¡ ÀÚµ¿À¸·Î Å¬¸¯ ÀÚÃ¼¸¦ ¸·¾ÆÁÖ¹Ç·Î
//      º°µµ ¹æ¾î ÄÚµå°¡ ¾ø¾îµµ ¾ÈÀüÇÕ´Ï´Ù.
//   4) [Áß¿ä] LobbyScene¿¡ GameManager°¡ ÀÖ¾î¾ß ÇÕ´Ï´Ù - LobbySceneÀÌ °ÔÀÓÀÌ °¡Àå ¸ÕÀú ½ÃÀÛÇÏ´Â
//      ¾ÀÀÌ µÇ¾úÀ¸´Ï, ´Ù¸¥ ¾À¿¡ ÀÖ´ø GameManager(Fade Canvas Group/UIExit/UIGameOver ÀÚ½Ä ±¸¼º
//      Æ÷ÇÔ)¸¦ ÅëÂ°·Î LobbySceneÀ¸·Î ¿Å±â¼¼¿ä.
//   5) Ingame Scene Name¿¡ ½ÇÁ¦ ÀÎ°ÔÀÓ ¾À ÀÌ¸§À» Á¤È®È÷ ÀÔ·ÂÇÏ¼¼¿ä - Build SettingsÀÇ Scenes In
//      Build¿¡ LobbyScene°ú ÇÔ²² µî·ÏµÇ¾î ÀÖ¾î¾ß ÇÕ´Ï´Ù.
//   6) IngameScene ÂÊ¿¡¼­ ÀÌ ¾ÆÀÌµğ¸¦ Ç¥½ÃÇÏ·Á¸é GameManager.Instance.PlayerId¸¦ ÀĞÀ¸¸é µË´Ï´Ù
//      (UICharacterInfo.cs°¡ ÀÌ¹Ì ÀÌ·¸°Ô ¿¬°áµÇ¾î ÀÖ½À´Ï´Ù).
// ============================================================================

using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UILobby : MonoBehaviour
{
    private enum IdState { Empty, TooLong, InvalidChars, Valid }

    [Header("¾ÆÀÌµğ ÀÔ·Â")]
    [Tooltip("ÇÃ·¹ÀÌ¾î°¡ ¾ÆÀÌµğ¸¦ ÀÔ·ÂÇÏ´Â ÀÔ·ÂÃ¢ÀÔ´Ï´Ù.")]
    public TMP_InputField idInputField;
    [Tooltip("ÀÔ·Â°ªÀÌ Á¶°Ç¿¡ ¾î±ß³¯ ¶§(±ÛÀÚ¼ö ÃÊ°ú, Çã¿ëµÇÁö ¾Ê´Â ±ÛÀÚ) ±× ÀÌÀ¯¸¦ º¸¿©ÁÖ´Â ÅØ½ºÆ®ÀÔ´Ï´Ù. " +
              "ºñ¾îÀÖ°Å³ª À¯È¿ÇÑ ÀÔ·ÂÀÏ ¶§´Â ÀÚµ¿À¸·Î ¼û°ÜÁı´Ï´Ù.")]
    public TextMeshProUGUI idStatusText;
    [Tooltip("¾ÆÀÌµğ°¡ ºñ¾îÀÖ°Å³ª ¾Æ·¡ Á¶°Ç¿¡ ¾î±ß³ª¸é ÀÚµ¿À¸·Î ºñÈ°¼ºÈ­µÇ´Â °ÔÀÓ ½ÃÀÛ ¹öÆ°ÀÔ´Ï´Ù.")]
    public Button gameStartButton;

    [Header("¾ÆÀÌµğ ÀÔ·Â - Á¶°Ç")]
    [Tooltip("¾ÆÀÌµğ·Î Çã¿ëÇÒ ÃÖ´ë ±ÛÀÚ¼öÀÔ´Ï´Ù.")]
    public int maxIdLength = 12;
    [Tooltip("¿µ¾î/ÇÑ±Û ÀÌ¿ÜÀÇ ±ÛÀÚ(¼ıÀÚ, °ø¹é, Æ¯¼ö¹®ÀÚ µî)¸¦ ÀÔ·ÂÇßÀ» ¶§ idStatusText¿¡ º¸¿©ÁÙ ¹®±¸ÀÔ´Ï´Ù.")]
    public string invalidCharsMessage = "¿µ¾î¿Í ÇÑ±Û¸¸ »ç¿ëÇÒ ¼ö ÀÖ½À´Ï´Ù.";
    [Tooltip("ÃÖ´ë ±ÛÀÚ¼ö(maxIdLength)¸¦ ÃÊ°úÇßÀ» ¶§ idStatusText¿¡ º¸¿©ÁÙ ¹®±¸ÀÔ´Ï´Ù. {0} ÀÚ¸®¿¡ " +
              "maxIdLength°¡ ±×´ë·Î µé¾î°©´Ï´Ù.")]
    public string tooLongMessageFormat = "ÃÖ´ë {0}±ÛÀÚ±îÁö ÀÔ·ÂÇÒ ¼ö ÀÖ½À´Ï´Ù.";

    [Header("¾À ÀüÈ¯")]
    [Tooltip("°ÔÀÓ ½ÃÀÛ ¹öÆ°À» ´©¸£¸é ºÒ·¯¿Ã ÀÎ°ÔÀÓ ¾À ÀÌ¸§ÀÔ´Ï´Ù. Build SettingsÀÇ Scenes In Build¿¡ " +
              "µî·ÏµÇ¾î ÀÖ¾î¾ß ÇÕ´Ï´Ù.")]
    public string ingameSceneName = "IngameScene";
    [Tooltip("°ÔÀÓ ½ÃÀÛ ¹öÆ°À» ´©¸¥ µÚ È­¸éÀÌ ¿ÏÀüÈ÷ ±î¸ÅÁú ¶§±îÁö °É¸®´Â ½Ã°£(ÃÊ)ÀÔ´Ï´Ù.")]
    public float fadeOutDuration = 1f;
    [Tooltip("ÀÎ°ÔÀÓ ¾ÀÀÌ ´Ù ÁØºñµÈ µÚ, È­¸éÀÌ ´Ù½Ã º¸ÀÌ±â±îÁö(ÆäÀÌµå ÀÎ) °É¸®´Â ½Ã°£(ÃÊ)ÀÔ´Ï´Ù.")]
    public float fadeInDuration = 1f;

    // ¿µ¾î(A-Z, a-z) + ÇÑ±Û ¿Ï¼ºÇü À½Àı(°¡-ÆR, U+AC00~U+D7A3) + ÇÑ±Û ÀÚ¸ğ(¤¡-¤Ó, U+3131~U+318E - ÇÑ±Û
    // ÀÔ·Â µµÁß ¾ÆÁ÷ ¿Ï¼ºµÇÁö ¾ÊÀº ³¹ÀÚµµ ¸·Áö ¾Ê±â À§ÇØ ÇÔ²² Çã¿ëÇÕ´Ï´Ù)¸¸ Çã¿ëÇÕ´Ï´Ù. ºó ¹®ÀÚ¿­µµ
    // ÀÌ Á¤±Ô½Ä ÀÚÃ¼´Â Åë°ú½ÃÅ°¹Ç·Î(±æÀÌ 0), ºñ¾îÀÖÀ½ ¿©ºÎ´Â ValidateId()¿¡¼­ º°µµ·Î ¸ÕÀú °Ë»çÇÕ´Ï´Ù.
    private static readonly Regex AllowedIdCharsRegex = new Regex(@"^[a-zA-Z°¡-ÆR¤¡-¤ş]*$");

    // ÆäÀÌµå/¾À ÀüÈ¯ÀÌ ½ÃÀÛµÈ µÚ ¹öÆ°À» ¶Ç ´­·¯µµ Áßº¹À¸·Î ½ÃÀÛµÇÁö ¾Êµµ·Ï ¸·½À´Ï´Ù. ¾ÀÀÌ ½ÇÁ¦·Î
    // ¹Ù²î¸é ÀÌ ¿ÀºêÁ§Æ®µµ ÇÔ²² ÆÄ±«µÇ¹Ç·Î º°µµ·Î false·Î µÇµ¹¸± ÇÊ¿ä°¡ ¾ø½À´Ï´Ù.
    private bool isTransitioning;

    private void Awake()
    {
        if (idInputField == null)
        {
            Debug.LogWarning("[UILobby] Id Input Field°¡ ¿¬°áµÇ¾î ÀÖÁö ¾Ê½À´Ï´Ù.", this);
            return;
        }

        // Å¸ÀÌÇÎ/ºÙ¿©³Ö±â ´Ü°è¿¡¼­ºÎÅÍ maxIdLength¸¦ ³Ñ´Â ÀÔ·ÂÀÌ ¾Æ¿¹ µé¾î°¡Áö ¾Êµµ·Ï ¸·½À´Ï´Ù.
        idInputField.characterLimit = maxIdLength;

        idInputField.onValueChanged.AddListener(HandleIdValueChanged);
        HandleIdValueChanged(idInputField.text); // ½ÃÀÛ »óÅÂ(º¸Åë ºó ¹®ÀÚ¿­)¿¡ ¸ÂÃç ¹öÆ°/»óÅÂ ¹®±¸¸¦ ¹Ì¸® ¸ÂÃçµÓ´Ï´Ù.
    }

    private void OnDestroy()
    {
        if (idInputField != null) idInputField.onValueChanged.RemoveListener(HandleIdValueChanged);
    }

    /// <summary>¾ÆÀÌµğ ÀÔ·ÂÃ¢ÀÇ ±ÛÀÚ°¡ ¹Ù²ğ ¶§¸¶´Ù È£ÃâµË´Ï´Ù. »óÅÂ ¹®±¸ Ç¥½Ã¿Í °ÔÀÓ ½ÃÀÛ ¹öÆ°ÀÇ
    /// È°¼ºÈ­ ¿©ºÎ¸¦ ÇÔ²² °»½ÅÇÕ´Ï´Ù.</summary>
    private void HandleIdValueChanged(string text)
    {
        IdState state = ValidateId(text);

        switch (state)
        {
            case IdState.TooLong:
                ShowIdStatus(string.Format(tooLongMessageFormat, maxIdLength));
                break;
            case IdState.InvalidChars:
                ShowIdStatus(invalidCharsMessage);
                break;
            default: // Empty, Valid - Á¶°Ç À§¹İÀÌ ¾Æ´Ï¹Ç·Î »óÅÂ ¹®±¸¸¦ ¼û±é´Ï´Ù.
                ShowIdStatus(null);
                break;
        }

        if (gameStartButton != null) gameStartButton.interactable = state == IdState.Valid;
    }

    private void ShowIdStatus(string message)
    {
        if (idStatusText == null) return;

        bool show = !string.IsNullOrEmpty(message);
        idStatusText.text = show ? message : string.Empty;
        idStatusText.gameObject.SetActive(show);
    }

    private IdState ValidateId(string text)
    {
        if (string.IsNullOrEmpty(text)) return IdState.Empty;
        if (text.Length > maxIdLength) return IdState.TooLong;
        if (!AllowedIdCharsRegex.IsMatch(text)) return IdState.InvalidChars;
        return IdState.Valid;
    }

    /// <summary>GameStart ¹öÆ°ÀÇ OnClick¿¡ ¿¬°áÇÏ¼¼¿ä. ¾ÆÀÌµğ¸¦ GameManager¿¡ ÀúÀåÇÑ µÚ, È­¸éÀ»
    /// fadeOutDurationÃÊ¿¡ °ÉÃÄ ±î¸Ä°Ô ¸¸µé°í ingameSceneNameÀ» ºÒ·¯¿É´Ï´Ù - »õ ¾ÀÀÌ ÁØºñµÇ¸é ÀÚµ¿À¸·Î
    /// ´Ù½Ã ÆäÀÌµå ÀÎµË´Ï´Ù(GameManager.LoadSceneWithFade() Âü°í). ¹öÆ°ÀÌ ºñÈ°¼ºÈ­µÇ¾î ÀÖÀ¸¸é À¯´ÏÆ¼°¡
    /// Å¬¸¯ ÀÚÃ¼¸¦ ¸·¾ÆÁÖÁö¸¸, È¤½Ã ¸ğ¸¦ »óÈ²(ÄÚµå¿¡¼­ Á÷Á¢ È£Ãâ µî)¿¡ ´ëºñÇØ Á¶°ÇÀ» ÇÑ ¹ø ´õ
    /// È®ÀÎÇÕ´Ï´Ù.</summary>
    public void ClickGameStartButton()
    {
        if (isTransitioning) return;

        string id = idInputField != null ? idInputField.text : string.Empty;
        if (ValidateId(id) != IdState.Valid)
        {
            Debug.LogWarning("[UILobby] ¾ÆÀÌµğ Á¶°ÇÀÌ ÃæÁ·µÇÁö ¾Ê¾Æ °ÔÀÓÀ» ½ÃÀÛÇÒ ¼ö ¾ø½À´Ï´Ù.", this);
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[UILobby] GameManager.Instance°¡ ¾ø¾î ¾ÆÀÌµğ Àü´Ş/ÆäÀÌµå ¿¬Ãâ ¾øÀÌ ¾ÀÀ» ¹Ù·Î " +
                              "ºÒ·¯¿É´Ï´Ù. LobbyScene¿¡ GameManager°¡ ¹èÄ¡µÇ¾î ÀÖ´ÂÁö È®ÀÎÇÏ¼¼¿ä.", this);
            SceneManager.LoadScene(ingameSceneName);
            return;
        }

        GameManager.Instance.SetPlayerId(id);

        isTransitioning = true;
        GameManager.Instance.LoadSceneWithFade(ingameSceneName, fadeOutDuration, fadeInDuration);
    }
}