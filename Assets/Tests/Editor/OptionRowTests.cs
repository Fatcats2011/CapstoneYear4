using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The Options pages' code-built rows (OptionRow) and their header (OptionsPageHeader): a row shows its setting in
    /// the screen's own look, greyed while dimmed; the header names every page with the current one bright, between the
    /// shoulder buttons of the device used last
    /// </summary>
    public class OptionRowTests
    {
        readonly TestObjects objects = new TestObjects();
        RectTransform page;
        TextMeshProUGUI style;

        static bool vsyncOn = true;
        static bool VsyncOn() { return vsyncOn; }

        [SetUp]
        public void SetUp()
        {
            page = objects.NewGameObject("Page").AddComponent<RectTransform>();
            style = objects.NewGameObject("Off").AddComponent<TextMeshProUGUI>();
            style.color = Color.yellow;
            vsyncOn = true;
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        static string Cap(int value) { return value == 0 ? "OFF" : value.ToString(); }

        [Test]
        public void Create_ShowsTheSetting_InTheScreensLook_WhereItsPut()
        {
            OptionSetting vsync = OptionSetting.Toggle("VSYNC", "OFF", "ON", 1);

            OptionRow row = OptionRow.Create(page, style, vsync, new Vector2(-560f, 80f));

            Assert.AreSame(page, row.transform.parent);
            Assert.AreEqual(new Vector2(-560f, 80f), ((RectTransform)row.transform).anchoredPosition);
            Assert.AreEqual("VSYNC", row.LabelText);
            Assert.AreEqual("ON", row.Value.text);
            Assert.AreEqual(Color.yellow, row.Value.color);
            Assert.AreSame(style.font, row.Value.font);
        }

        [Test]
        public void Refresh_ShowsTheNewValue()
        {
            OptionSetting vsync = OptionSetting.Toggle("VSYNC", "OFF", "ON", 1);
            OptionRow row = OptionRow.Create(page, style, vsync, Vector2.zero);

            vsync.Step(false);
            row.Refresh();

            Assert.AreEqual("OFF", row.Value.text);
        }

        [Test]
        public void ADimmedRow_IsGreyed_UntilItsInUseAgain()
        {
            OptionSetting cap = new OptionSetting("FRAME CAP", new[] { 0, 60 }, Cap, 0);
            cap.Dimmed = VsyncOn;
            OptionRow row = OptionRow.Create(page, style, cap, Vector2.zero);

            Assert.Less(row.Value.color.a, 1f, "greyed");
            vsyncOn = false;
            row.Refresh();
            Assert.AreEqual(Color.yellow, row.Value.color);
        }

        [TestCase(GlyphStyle.Xbox, "LB", "RB")]
        [TestCase(GlyphStyle.PlayStation, "L1", "R1")]
        [TestCase(GlyphStyle.Nintendo, "L", "R")]
        [TestCase(GlyphStyle.Keyboard, "F", "R")]
        public void Header_NamesTheShoulders_OfTheDeviceUsedLast(GlyphStyle device, string left, string right)
        {
            string line = OptionsPageHeader.Line(new[] { "GENERAL", "DISPLAY" }, 0, device);

            StringAssert.StartsWith("<alpha=#FF>" + left + " ", line);
            StringAssert.EndsWith(" " + right, line);
        }

        [Test]
        public void Header_HangsFromTheTopEdge_SoAWideScreenStillShowsIt()
        {
            OptionsPageHeader header = OptionsPageHeader.Create(page, style, new[] { "GENERAL", "DISPLAY" }, 60f);

            RectTransform rect = (RectTransform)header.transform;
            Assert.AreEqual(1f, rect.anchorMin.y);
            Assert.AreEqual(1f, rect.anchorMax.y);
            Assert.AreEqual(-60f, rect.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void Header_TheCurrentPageIsBright_TheOthersDimmed()
        {
            string line = OptionsPageHeader.Line(new[] { "GENERAL", "DISPLAY", "AUDIO" }, 1, GlyphStyle.Xbox);

            StringAssert.Contains("<alpha=#FF>DISPLAY", line);
            StringAssert.Contains(OptionsPageHeader.DIMMED + "GENERAL", line);
            StringAssert.Contains(OptionsPageHeader.DIMMED + "AUDIO", line);
        }
    }
}
