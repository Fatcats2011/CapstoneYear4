using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One seat's ticket on the Dispatch board: a held seat shows the player's company, name, colour and hat with a ready
/// stamp (or "getting ready"), a free seat shows "hiring". The scene wires the fields. See docs/online.md
/// </summary>
public class TicketView : MonoBehaviour
{
    public static string DISPATCHED => Loc.Get("dispatch.dispatched");
    public static string GETTING_READY => Loc.Get("dispatch.gettingready");
    public static string HIRING => Loc.Get("dispatch.hiring");

    static readonly int FlashTrigger = Animator.StringToHash("Flash");

    [Header("Held seat")]
    [SerializeField] Image header;
    [SerializeField] TMP_Text company;
    [SerializeField] TMP_Text playerName;
    [SerializeField] TMP_Text colourName;
    [SerializeField] TMP_Text hatName;
    [SerializeField] Image colourSwatch;
    [SerializeField] Image hatIcon;

    [Header("States")]
    [SerializeField] GameObject filled;
    [SerializeField] GameObject hiring;
    [SerializeField] GameObject stamp;
    [SerializeField] GameObject gettingReady;
    [SerializeField] GameObject mineMarker;

    [Header("Other")]
    [SerializeField] Animator highlight; // trigger "Flash"; may be missing

    /// <summary>How many times the ticket has flashed</summary>
    public int FlashCount { get; private set; }

    /// <summary>
    /// Shows a seat. The company, colour and hat can be null (nothing to show of them)
    /// </summary>
    public void Show(TicketState s, CompanyInformation companyInfo, PlayerColorInformationSO colour, PlayerHatInformationSO hat)
    {
        SetActive(filled, s.Taken);
        SetActive(hiring, !s.Taken);
        SetActive(stamp, s.Taken && s.Ready);
        SetActive(gettingReady, s.Taken && !s.Ready);
        SetActive(mineMarker, s.Taken && s.Mine);

        SetLabel(hiring, HIRING);
        SetLabel(stamp, DISPATCHED);
        SetLabel(gettingReady, GETTING_READY);

        if (!s.Taken)
            return;

        if (companyInfo != null)
        {
            SetText(company, companyInfo.companyName.ToString().ToUpperInvariant());
            if (header != null)
                header.color = companyInfo.companyColor;
        }
        if (playerName != null)
            playerName.richText = false; // a Steam name is not markup
        SetText(playerName, s.Name);

        SetText(colourName, colour == null ? "" : NameOr(colour.displayName, colour.name));
        if (colourSwatch != null)
            colourSwatch.sprite = colour == null ? null : colour.colorIcon;

        SetText(hatName, hat == null ? "" : NameOr(hat.displayName, hat.name));
        if (hatIcon != null)
            hatIcon.sprite = hat == null ? null : hat.hatIcon;
    }

    /// <summary>The ticket changed: it lights up for a moment</summary>
    public virtual void Flash()
    {
        FlashCount++;
        if (highlight != null)
            highlight.SetTrigger(FlashTrigger);
    }

    static string NameOr(string name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    static void SetActive(GameObject target, bool on)
    {
        if (target != null)
            target.SetActive(on);
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // The words on a state's stamp are the game's, not the scene's: its text takes them
    static void SetLabel(GameObject state, string value)
    {
        if (state == null)
            return;

        TMP_Text text = state.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = value;
    }
}
