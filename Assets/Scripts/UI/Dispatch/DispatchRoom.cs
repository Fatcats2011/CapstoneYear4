using System;
using UnityEngine;

/// <summary>
/// The Dispatch room, in its own scene (Multiplayerlobby) loaded alongside the menu while the board is up
/// (DispatchRoomLoader). It tells the game where its seats are (the scooters stand there online), where each seat's
/// ticket goes on the board, and which camera shows it, and colours each podium's rim in its seat's company colour.
/// Tools → Dead on Arrival → Dispatch → Set Up Room Scene wires it. See docs/online.md
/// </summary>
public class DispatchRoom : MonoBehaviour
{
    /// <summary>How much brighter than the company colour a rim glows</summary>
    public const float RIM_GLOW = 2f;

    /// <summary>How bright the backdrop (walls, floor, board, props) is against its own colour: it sits back and the
    /// spotlit podiums stand out, as in the mockup</summary>
    public const float BACKDROP = 0.65f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    [SerializeField] internal GameObject[] seats = new GameObject[Constants.MAX_PLAYERS];      // SeatN_Spawn, by seat
    [SerializeField] internal Transform[] ticketAnchors = new Transform[Constants.MAX_PLAYERS]; // SeatN_Ticket, by seat
    [SerializeField] internal Camera roomCamera;
    [SerializeField] internal Renderer[] rims = new Renderer[Constants.MAX_PLAYERS];          // PodiumN_Rim, by seat
    [SerializeField] internal Renderer[] dimmed;                                               // the backdrop: all but the podiums
    [SerializeField] internal Transform[] avatarAnchors = new Transform[Constants.MAX_PLAYERS]; // SeatN_Avatar: under each podium

    /// <summary>The room that's loaded, if one is</summary>
    public static DispatchRoom Current { get; private set; }

    public GameObject[] Seats { get { return seats; } }
    public Transform[] TicketAnchors { get { return ticketAnchors; } }
    public Camera RoomCamera { get { return roomCamera; } }
    /// <summary>Under each podium, by seat: where its player's profile picture hangs</summary>
    public Transform[] AvatarAnchors { get { return avatarAnchors; } }

    void OnEnable()
    {
        Register();
        Dim();
        // Open from the start in the editor, the room mustn't cover the title screen: its camera follows the board
        ShowCamera(DispatchBoard.Showing);

        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return;

        Tint(players.CompanyForSlot);
        // The room arrives after player select began: the scooters move from the booths onto its podiums
        if (GameManager.Instance != null && GameManager.Instance.MainState == GameState.PlayerSelect)
            players.SetAllPlayerSpawn();
    }

    void OnDisable()
    {
        Unregister();
    }

    /// <summary>The room's camera on (the board is up) or off</summary>
    public void ShowCamera(bool on)
    {
        if (roomCamera != null && roomCamera.enabled != on)
            roomCamera.enabled = on;
    }

    /// <summary>The backdrop at BACKDROP of each material's own colour (a missing renderer is skipped)</summary>
    public void Dim()
    {
        if (dimmed == null)
            return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in dimmed)
        {
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColor))
                continue;

            Color own = r.sharedMaterial.GetColor(BaseColor);
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, new Color(own.r * BACKDROP, own.g * BACKDROP, own.b * BACKDROP, own.a));
            r.SetPropertyBlock(block);
        }
    }

    internal void Register()
    {
        Current = this;
    }

    internal void Unregister()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>Each podium's rim in its seat's company colour (a missing rim or company is skipped)</summary>
    public void Tint(Func<int, CompanyInformation> companyForSeat)
    {
        if (rims == null)
            return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < rims.Length && i < Constants.MAX_PLAYERS; i++)
        {
            CompanyInformation company = rims[i] == null ? null : companyForSeat(i);
            if (company == null)
                continue;

            rims[i].GetPropertyBlock(block);
            block.SetColor(BaseColor, company.companyColor);
            block.SetColor(EmissionColor, company.companyColor * RIM_GLOW);
            rims[i].SetPropertyBlock(block);
        }
    }
}
