using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The Options screen's code-built rows (OptionSetting) and its pages (OptionsBook): left and right step a row's
    /// choices and stop at the ends, a dimmed row doesn't change, the shoulders turn the pages round, and up and down
    /// wrap within a page
    /// </summary>
    public class OptionsBookTests
    {
        static readonly int[] CAPS = { 0, 30, 60, 120 };
        static int changes;
        static int lastValue;

        [SetUp]
        public void SetUp()
        {
            changes = 0;
            lastValue = -1;
        }

        static string Cap(int value) { return value == 0 ? "OFF" : value.ToString(); }
        static void Changed(int value) { changes++; lastValue = value; }
        static bool AlwaysDimmed() { return true; }

        static OptionsBook Book()
        {
            return new OptionsBook(
                new OptionsPage("GENERAL"),
                new OptionsPage("DISPLAY", OptionSetting.Toggle("VSYNC", "OFF", "ON", 1), new OptionSetting("FRAME CAP", CAPS, Cap, 0)),
                new OptionsPage("AUDIO", OptionSetting.Toggle("REDUCE MOTION", "OFF", "ON", 0)));
        }

        [Test]
        public void Setting_StartsAtItsValue_AndShowsItsLabel()
        {
            OptionSetting cap = new OptionSetting("FRAME CAP", CAPS, Cap, 60);

            Assert.AreEqual(60, cap.Value);
            Assert.AreEqual("60", cap.ValueLabel);
            Assert.AreEqual("FRAME CAP", cap.Label);
        }

        [Test]
        public void Setting_Step_StopsAtTheEnds()
        {
            OptionSetting cap = new OptionSetting("FRAME CAP", CAPS, Cap, 0);

            Assert.IsFalse(cap.Step(false), "already the first");
            Assert.IsTrue(cap.Step(true));
            Assert.AreEqual(30, cap.Value);
            cap.Step(true);
            cap.Step(true);
            Assert.IsFalse(cap.Step(true), "already the last");
            Assert.AreEqual(120, cap.Value);
        }

        [Test]
        public void Setting_Step_SaysWhatItChangedTo()
        {
            OptionSetting cap = new OptionSetting("FRAME CAP", CAPS, Cap, 0);
            cap.Changed += Changed;

            cap.Step(true);
            cap.Step(false);
            cap.Step(false);

            Assert.AreEqual(2, changes, "the stop at the end isn't a change");
            Assert.AreEqual(0, lastValue);
        }

        [Test]
        public void Setting_AValueNotListed_IsAddedInOrder()
        {
            OptionSetting cap = new OptionSetting("FRAME CAP", CAPS, Cap, 75);

            Assert.AreEqual(75, cap.Value);
            Assert.AreEqual("75", cap.ValueLabel);
            cap.Step(false);
            Assert.AreEqual(60, cap.Value);
            cap.Step(true);
            cap.Step(true);
            Assert.AreEqual(120, cap.Value);
        }

        [Test]
        public void Setting_Dimmed_DoesntStep()
        {
            OptionSetting cap = new OptionSetting("FRAME CAP", CAPS, Cap, 0);
            cap.Dimmed = AlwaysDimmed;

            Assert.IsTrue(cap.IsDimmed);
            Assert.IsFalse(cap.Step(true));
            Assert.AreEqual(0, cap.Value);
        }

        [Test]
        public void Toggle_IsOffAndOn()
        {
            OptionSetting vsync = OptionSetting.Toggle("VSYNC", "OFF", "ON", 1);

            Assert.AreEqual("ON", vsync.ValueLabel);
            vsync.Step(false);
            Assert.AreEqual(0, vsync.Value);
            Assert.AreEqual("OFF", vsync.ValueLabel);
        }

        [Test]
        public void Book_TurnPage_WrapsRound_AndStartsAtTheTopRow()
        {
            OptionsBook book = Book();
            Assert.AreEqual(0, book.Page);

            book.TurnPage(true);
            book.Move(true);
            Assert.AreEqual(1, book.Row);
            book.TurnPage(true);
            Assert.AreEqual(2, book.Page);
            Assert.AreEqual(0, book.Row, "a new page starts at its top");
            book.TurnPage(true);
            Assert.AreEqual(0, book.Page, "round to GENERAL");
            book.TurnPage(false);
            Assert.AreEqual(2, book.Page, "and back round");
            Assert.AreEqual("AUDIO", book.Current.Name);
        }

        [Test]
        public void Book_Move_WrapsWithinThePage()
        {
            OptionsBook book = Book();
            book.TurnPage(true);

            book.Move(false);
            Assert.AreEqual(1, book.Row, "up from the top: the bottom");
            Assert.AreEqual("FRAME CAP", book.Selected.Label);
            book.Move(true);
            Assert.AreEqual(0, book.Row);
        }

        [Test]
        public void Book_Step_ChangesTheSelectedRow()
        {
            OptionsBook book = Book();
            book.TurnPage(true);
            book.Move(true);

            Assert.IsTrue(book.Step(true));
            Assert.AreEqual(30, book.Selected.Value);
        }

        [Test]
        public void Book_ThePageWithNoRows_HasNoSelection()
        {
            OptionsBook book = Book();

            Assert.IsNull(book.Selected, "GENERAL is the drawn page");
            Assert.IsFalse(book.Step(true));
            book.Move(true);
            Assert.AreEqual(0, book.Row);
        }

        [Test]
        public void Book_Reset_BackToTheFirstPage()
        {
            OptionsBook book = Book();
            book.TurnPage(true);
            book.Move(true);

            book.Reset();

            Assert.AreEqual(0, book.Page);
            Assert.AreEqual(0, book.Row);
        }
    }
}
