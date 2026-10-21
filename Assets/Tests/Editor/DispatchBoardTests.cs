using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Dispatch's board and tickets: what a ticket shows for each state of a seat, when a ticket flashes, the ready
    /// counter and the banner over the countdown
    /// </summary>
    public class DispatchBoardTests
    {
        readonly TestObjects objects = new TestObjects();

        class Ticket
        {
            public TicketView View;
            public GameObject Filled, Hiring, Stamp, GettingReady, Mine;
            public TMP_Text ColourName, HatName, StampText;
        }

        DispatchBoard board;
        Ticket[] seats;
        GameObject banner;
        TMP_Text readyLine;

        [SetUp]
        public void SetUp()
        {
            board = objects.Add<DispatchBoard>();
            seats = new Ticket[Constants.MAX_PLAYERS];
            TicketView[] views = new TicketView[seats.Length];
            for (int i = 0; i < seats.Length; i++)
            {
                Ticket t = new Ticket { View = objects.Add<TicketView>() };
                t.Filled = objects.NewGameObject("Filled");
                t.Hiring = objects.NewGameObject("Hiring");
                t.Stamp = objects.NewGameObject("Stamp");
                t.GettingReady = objects.NewGameObject("Getting Ready");
                t.Mine = objects.NewGameObject("Mine");
                t.ColourName = objects.Add<TextMeshProUGUI>();
                t.HatName = objects.Add<TextMeshProUGUI>();
                t.StampText = t.Stamp.AddComponent<TextMeshProUGUI>();
                Set(t.View, "filled", t.Filled);
                Set(t.View, "hiring", t.Hiring);
                Set(t.View, "stamp", t.Stamp);
                Set(t.View, "gettingReady", t.GettingReady);
                Set(t.View, "mineMarker", t.Mine);
                Set(t.View, "colourName", t.ColourName);
                Set(t.View, "hatName", t.HatName);
                seats[i] = t;
                views[i] = t.View;
            }

            banner = objects.NewGameObject("Banner");
            banner.SetActive(false);
            readyLine = objects.Add<TextMeshProUGUI>();
            Set(board, "tickets", views);
            Set(board, "banner", banner);
            Set(board, "readyLine", readyLine);
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        // The scene wires the serialized fields; the tests wire them by name
        static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            info.SetValue(target, value);
        }

        static TicketState[] Seated(params (int seat, string name, bool ready, bool mine)[] players)
        {
            List<(int, string, int, int, bool, bool)> list = new List<(int, string, int, int, bool, bool)>();
            foreach (var p in players)
                list.Add((p.seat, p.name, 0, 0, p.ready, p.mine));
            return DispatchRules.Tickets(list);
        }

        [Test]
        public void Apply_FreeSeat_ShowsHiring()
        {
            board.Apply(Seated());

            Assert.IsTrue(seats[0].Hiring.activeSelf);
            Assert.IsFalse(seats[0].Filled.activeSelf);
        }

        [Test]
        public void Apply_ReadySeat_ShowsStamp_NotGettingReady()
        {
            board.Apply(Seated((0, "Ann", true, false), (1, "Bo", false, false)));

            Assert.IsTrue(seats[0].Stamp.activeSelf);
            Assert.IsFalse(seats[0].GettingReady.activeSelf);
            Assert.IsFalse(seats[1].Stamp.activeSelf);
            Assert.IsTrue(seats[1].GettingReady.activeSelf);
            Assert.AreEqual("DISPATCHED", seats[0].StampText.text);
        }

        [Test]
        public void Apply_Mine_ShowsMarker_OnlyOnThatTicket()
        {
            board.Apply(Seated((0, "Ann", false, false), (1, "Bo", false, true)));

            Assert.IsFalse(seats[0].Mine.activeSelf);
            Assert.IsTrue(seats[1].Mine.activeSelf);
            Assert.IsFalse(seats[2].Mine.activeSelf);
        }

        [Test]
        public void Apply_SetsReadyLine()
        {
            board.Apply(Seated((0, "Ann", true, false), (1, "Bo", false, false)));

            Assert.AreEqual("1 / 2 READY", readyLine.text);
        }

        [Test]
        public void Apply_SameStatesTwice_FlashesOnce()
        {
            board.Apply(Seated((0, "Ann", false, false)));
            board.Apply(Seated((0, "Ann", false, false)));

            Assert.AreEqual(1, seats[0].View.FlashCount);
        }

        [Test]
        public void Apply_ChangedSeat_FlashesThatTicketOnly()
        {
            board.Apply(Seated((0, "Ann", false, false), (1, "Bo", false, false)));
            board.Apply(Seated((0, "Ann", true, false), (1, "Bo", false, false)));

            Assert.AreEqual(2, seats[0].View.FlashCount);
            Assert.AreEqual(1, seats[1].View.FlashCount);
        }

        [Test]
        public void Apply_SeatEmpties_NoFlash_ShowsHiring()
        {
            board.Apply(Seated((0, "Ann", true, false), (1, "Bo", true, false)));
            board.Apply(Seated((0, "Ann", true, false)));

            Assert.AreEqual(1, seats[1].View.FlashCount);
            Assert.IsTrue(seats[1].Hiring.activeSelf);
            Assert.AreEqual("1 / 1 READY", readyLine.text);
        }

        [Test]
        public void Banner_FollowsCountdownEvents()
        {
            board.OnCountdownBegan();
            Assert.IsTrue(banner.activeSelf);

            board.OnCountdownStopped();
            Assert.IsFalse(banner.activeSelf);

            // A join during the banner raises Stopped, and Began can come twice
            Assert.DoesNotThrow(() =>
            {
                board.OnCountdownBegan();
                board.OnCountdownBegan();
            });
            Assert.IsTrue(banner.activeSelf);

            board.OnCountdownStopped();
            Assert.IsFalse(banner.activeSelf);
        }

        [Test]
        public void TicketView_EmptyDisplayName_UsesAssetName()
        {
            PlayerColorInformationSO colour = ScriptableObject.CreateInstance<PlayerColorInformationSO>();
            colour.name = "Ghost Green";
            PlayerHatInformationSO hat = ScriptableObject.CreateInstance<PlayerHatInformationSO>();
            hat.name = "Top Hat";
            hat.displayName = "Fancy Hat";
            try
            {
                seats[0].View.Show(new TicketState { Seat = 0, Taken = true, Name = "Ann" }, null, colour, hat);

                Assert.AreEqual("Ghost Green", seats[0].ColourName.text);
                Assert.AreEqual("Fancy Hat", seats[0].HatName.text);
            }
            finally
            {
                Object.DestroyImmediate(colour);
                Object.DestroyImmediate(hat);
            }
        }

        [Test]
        public void JoinPrompts_HiddenOnDispatch_ComeBackAsTheyWere()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            GameObject[] prompts = new GameObject[Constants.MAX_PLAYERS];
            for (int i = 0; i < prompts.Length; i++)
                prompts[i] = objects.NewGameObject("Join Prompt " + i);
            prompts[1].SetActive(false);
            canvas.pressButtonTexts = prompts;

            canvas.SetJoinPromptsHidden(true);
            Assert.IsFalse(prompts[0].activeSelf);

            // A seat freed while hidden: its prompt waits for the end of the hiding
            canvas.TogglePressButtonTexts(1, true);
            Assert.IsFalse(prompts[1].activeSelf);

            canvas.SetJoinPromptsHidden(false);
            Assert.IsTrue(prompts[0].activeSelf);
            Assert.IsTrue(prompts[1].activeSelf);
            Assert.IsTrue(prompts[3].activeSelf);
        }

        // I-2: the board stays up through a lobby switch's moment offline, so Local Play's player select doesn't show
        [Test]
        public void ShowsFor_OnlineOrSwitching_InPlayerSelectOnly()
        {
            Assert.IsTrue(DispatchBoard.ShowsFor(true, false, GameState.PlayerSelect), "online");
            Assert.IsTrue(DispatchBoard.ShowsFor(false, true, GameState.PlayerSelect), "between sessions in a switch");
            Assert.IsFalse(DispatchBoard.ShowsFor(false, false, GameState.PlayerSelect), "Local Play");
            Assert.IsFalse(DispatchBoard.ShowsFor(true, true, GameState.Menu), "not outside player select");
        }

        // M-10: the right shoulder's label says what pressing it does, on the host only
        [Test]
        public void FillPrompt_HostSeesWhatPressingDoes_ClientSeesNothing()
        {
            TMP_Text prompt = objects.Add<TextMeshProUGUI>();
            Set(board, "fillPrompt", prompt);

            board.ShowFillPrompt(true, false);
            Assert.IsTrue(prompt.gameObject.activeSelf);
            Assert.AreEqual(MatchRules.OPEN_TO_ANYONE, prompt.text, "friends only: pressing opens it");

            board.ShowFillPrompt(true, true);
            Assert.AreEqual(MatchRules.FRIENDS_ONLY, prompt.text, "open: pressing closes it");

            board.ShowFillPrompt(false, false);
            Assert.IsFalse(prompt.gameObject.activeSelf, "a client's press does nothing");
        }

        [Test]
        public void FillPrompt_NoneWired_Nothing()
        {
            Assert.DoesNotThrow(() => board.ShowFillPrompt(true, true));
        }

        // The Fill prompt goes with the board, even wired outside its root, and comes back with it
        [Test]
        public void FillPrompt_HiddenWithTheBoard()
        {
            TMP_Text prompt = objects.Add<TextMeshProUGUI>();
            Set(board, "fillPrompt", prompt);
            board.ShowFillPrompt(true, false);

            Reflect.Invoke(board, "SetShowing", false);
            Assert.IsFalse(prompt.gameObject.activeSelf, "hidden with the board");

            board.ShowFillPrompt(true, false);
            Assert.IsTrue(prompt.gameObject.activeSelf, "shown again once the board is up");
        }

        // The line is rebuilt only when filling, searching or the whole seconds shown change, so a countdown frame that
        // changes none of them allocates nothing
        [Test]
        public void Matchmaking_LineFollowsSecondFillingAndSearching_BuiltOnlyOnAChange()
        {
            TMP_Text line = objects.Add<TextMeshProUGUI>();
            Set(board, "matchmakingLine", line);

            board.ShowMatchmaking(true, false, 29.2f);
            Assert.AreEqual("OPEN TO ANYONE · STARTS IN 30", line.text);
            Assert.AreEqual(1, board.StatusBuilds);

            board.ShowMatchmaking(true, false, 29.5f); // 30 is the ceiling of all three
            board.ShowMatchmaking(true, false, 29.01f);
            Assert.AreEqual(1, board.StatusBuilds, "the same second builds nothing");

            board.ShowMatchmaking(true, false, 28.9f);
            Assert.AreEqual("OPEN TO ANYONE · STARTS IN 29", line.text, "the next second updates it");
            Assert.AreEqual(2, board.StatusBuilds);

            board.ShowMatchmaking(false, false, 28.9f);
            Assert.AreEqual(MatchRules.FRIENDS_ONLY, line.text, "filling changes it");

            board.ShowMatchmaking(false, true, 28.9f);
            Assert.AreEqual(MatchRules.StatusLine(false, true, 0), line.text, "searching changes it");

            Reflect.Invoke(board, "SetShowing", false);
            board.ShowMatchmaking(false, true, 28.9f);
            Assert.AreEqual(5, board.StatusBuilds, "a re-shown board sets it again");
        }

        // The room's camera films the podiums while the board is up, and is off otherwise (the menu's own camera again)
        [Test]
        public void RoomCamera_OnWithTheBoard_OffWithout()
        {
            GameObject room = objects.NewGameObject("Room Camera");
            room.SetActive(false);
            Set(board, "roomCamera", room);

            Reflect.Invoke(board, "SetShowing", true);
            Assert.IsTrue(room.activeSelf, "on with the board");

            Reflect.Invoke(board, "SetShowing", false);
            Assert.IsFalse(room.activeSelf, "off with it");
        }

        [Test]
        public void RoomCamera_NoneWired_Nothing()
        {
            Assert.DoesNotThrow(() => Reflect.Invoke(board, "SetShowing", true));
        }

        [Test]
        public void TicketView_FlashWithoutAnAnimator_JustCounts()
        {
            Assert.DoesNotThrow(() => seats[0].View.Flash());
            Assert.AreEqual(1, seats[0].View.FlashCount);
        }
    }
}
