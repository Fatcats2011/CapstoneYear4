using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The mouse driving a menu (MouseMenus.Drive) through a fake one: moving over a row selects it, a click selects and
    /// presses it, a right click goes back, a still pointer leaves the pad's choice alone, and a menu whose rows span the
    /// screen takes a click anywhere along a row
    /// </summary>
    public class MouseMenusTests
    {
        readonly TestObjects objects = new TestObjects();
        MouseMenus mouse;

        class FakeMenu : IMouseMenu
        {
            public List<RectTransform> Rows = new List<RectTransform>();
            public int Selected;
            public bool Span;
            public readonly List<int> Clicks = new List<int>();
            public int Backs;

            public IReadOnlyList<RectTransform> MouseRows => Rows;
            public int MouseSelected => Selected;
            public bool MouseRowsSpanTheScreen => Span;
            public Camera MouseCamera => null;
            public void MouseSelect(int row) { Selected = row; }
            public void MouseClick(int row, Vector2 point, Camera camera) { Clicks.Add(row); }
            public void MouseBack() { Backs++; }
        }

        FakeMenu menu;

        [SetUp]
        public void SetUp()
        {
            mouse = objects.Add<MouseMenus>();
            menu = new FakeMenu();
            for (int i = 0; i < 3; i++)
            {
                RectTransform row = objects.NewGameObject("Row " + i).AddComponent<RectTransform>();
                row.sizeDelta = new Vector2(200, 80);
                row.position = new Vector3(300, 500 - i * 100, 0);
                menu.Rows.Add(row);
            }
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void MovingOverARow_SelectsIt()
        {
            mouse.Drive(menu, new Vector2(300, 400), true, false, false);

            Assert.AreEqual(1, menu.Selected);
            Assert.IsEmpty(menu.Clicks);
        }

        [Test]
        public void AStillPointer_LeavesThePadsChoice()
        {
            menu.Selected = 2;

            mouse.Drive(menu, new Vector2(300, 400), false, false, false);

            Assert.AreEqual(2, menu.Selected);
        }

        [Test]
        public void AClick_SelectsAndPresses()
        {
            mouse.Drive(menu, new Vector2(300, 300), false, true, false);

            Assert.AreEqual(2, menu.Selected);
            CollectionAssert.AreEqual(new[] { 2 }, menu.Clicks);
        }

        [Test]
        public void AClickWithTheCursorHidden_DoesNothing()
        {
            mouse.Drive(menu, new Vector2(300, 300), false, true, false, 3f);

            Assert.IsEmpty(menu.Clicks, "the click that focused the window");
            Assert.AreEqual(0, menu.Selected);
        }

        [Test]
        public void AClickOffTheRows_DoesNothing()
        {
            mouse.Drive(menu, new Vector2(900, 300), true, true, false);

            Assert.AreEqual(0, menu.Selected);
            Assert.IsEmpty(menu.Clicks);
        }

        [Test]
        public void ARightClick_GoesBack()
        {
            mouse.Drive(menu, new Vector2(900, 300), false, false, true);

            Assert.AreEqual(1, menu.Backs);
        }

        [Test]
        public void RowsSpanningTheScreen_TakeAClickFarAlongTheRow()
        {
            menu.Span = true;

            mouse.Drive(menu, new Vector2(Screen.width - 1, 400), false, true, false);

            CollectionAssert.AreEqual(new[] { 1 }, menu.Clicks);
        }

        [Test]
        public void AHiddenRow_CantBePicked()
        {
            menu.Rows[1].gameObject.SetActive(false);

            mouse.Drive(menu, new Vector2(300, 400), true, true, false);

            Assert.IsEmpty(menu.Clicks);
        }
    }
}
