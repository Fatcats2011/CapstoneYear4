using NUnit.Framework;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The price under a beacon's package icon (PriceTag) and on a drop-off's compass mark (CompassIconUI.SetPrice).
    /// Edit mode: nothing starts, so these reach the members directly
    /// </summary>
    public class PriceTagTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
        }

        // An indicator with this many views, each an icon object with a sprite renderer
        BeconIndicator NewIndicator(int views)
        {
            BeconIndicator indicator = objects.Add<BeconIndicator>();
            GameObject[] icons = new GameObject[views];
            SpriteRenderer[] sprites = new SpriteRenderer[views];
            for (int i = 0; i < views; i++)
            {
                icons[i] = objects.NewGameObject("Icon " + i);
                icons[i].transform.SetParent(indicator.transform, false);
                icons[i].layer = 8 + i;
                sprites[i] = icons[i].AddComponent<SpriteRenderer>();
            }
            SetField(indicator, "beconRotationObjects", icons);
            SetField(indicator, "rotationSprites", sprites);
            return indicator;
        }

        Order NewOrder(Constants.OrderValue tier, BeconIndicator indicator)
        {
            Order order = objects.Add<Order>();
            order.value = tier;
            order.orderMeshObject = objects.NewGameObject("Mesh");
            order.meshScale = new float[] { 1f, 1f, 1f, 1f };
            SetField(order, "beconIndicator", indicator);
            return order;
        }

        static TextMeshPro LabelOn(GameObject icon)
        {
            return icon.GetComponentInChildren<TextMeshPro>(true);
        }

        [Test]
        public void AnIndicatorWithTwoIcons_GetsTwoLabelsReadingThePrice()
        {
            BeconIndicator indicator = NewIndicator(2);

            PriceTag.Refresh(indicator, "$55");

            PriceTag tag = indicator.GetComponent<PriceTag>();
            Assert.AreEqual(2, tag.Labels);
            Assert.AreEqual("$55", tag.Text);
            Assert.AreEqual("$55", LabelOn(indicator.Icons[0]).text);
            Assert.AreEqual("$55", LabelOn(indicator.Icons[1]).text);
        }

        [Test]
        public void AnIconSwitchedOff_StillGetsItsLabel()
        {
            // A view nobody is playing has its icon switched off: text made under it never wakes, and can't take a font
            BeconIndicator indicator = NewIndicator(2);
            indicator.Icons[1].SetActive(false);

            PriceTag.Refresh(indicator, "$55");

            Assert.AreEqual(2, indicator.GetComponent<PriceTag>().Labels);
            Assert.AreEqual("$55", LabelOn(indicator.Icons[1]).text);
            Assert.AreEqual(indicator.Icons[1].transform, LabelOn(indicator.Icons[1]).transform.parent);
        }

        [Test]
        public void EachLabel_SitsUnderItsOwnIconOnItsLayer()
        {
            BeconIndicator indicator = NewIndicator(2);

            PriceTag.Refresh(indicator, "$55");

            for (int i = 0; i < 2; i++)
            {
                TextMeshPro label = LabelOn(indicator.Icons[i]);
                Assert.AreSame(indicator.Icons[i].transform, label.transform.parent);
                Assert.AreEqual(indicator.Icons[i].layer, label.gameObject.layer);
                Assert.AreEqual(new Vector3(0f, PriceTag.BELOW_ICON, 0f), label.transform.localPosition);
            }
        }

        [Test]
        public void ASecondRefresh_ChangesTheTextWithoutAddingLabels()
        {
            BeconIndicator indicator = NewIndicator(2);
            PriceTag.Refresh(indicator, "$55");

            PriceTag.Refresh(indicator, "$110");

            Assert.AreEqual(1, indicator.GetComponents<PriceTag>().Length);
            Assert.AreEqual(2, indicator.GetComponent<PriceTag>().Labels);
            Assert.AreEqual(2, indicator.GetComponentsInChildren<TextMeshPro>(true).Length);
            Assert.AreEqual("$110", LabelOn(indicator.Icons[1]).text);
        }

        [Test]
        public void AnOrder_ShowsItsWorthOnItsBeacon()
        {
            BeconIndicator indicator = NewIndicator(2);
            Order order = NewOrder(Constants.OrderValue.Easy, indicator);

            order.CountSteal();

            Assert.AreEqual("$" + order.Worth, indicator.GetComponent<PriceTag>().Text);
        }

        [Test]
        public void TheGoldenOrder_LabelsReadNothing()
        {
            BeconIndicator indicator = NewIndicator(2);
            Order order = NewOrder(Constants.OrderValue.Golden, indicator);

            order.ResetSteals();

            Assert.AreEqual("", indicator.GetComponent<PriceTag>().Text);
            Assert.AreEqual("", LabelOn(indicator.Icons[0]).text);
        }

        [Test]
        public void AnOrderWithNoIndicator_IsFine()
        {
            Order order = NewOrder(Constants.OrderValue.Easy, null);

            order.ResetSteals();

            Assert.IsNull(order.GetComponentInChildren<PriceTag>(true));
        }

        [Test]
        public void MissingIndicatorsAndIcons_AreSkipped()
        {
            PriceTag.Refresh((BeconIndicator)null, "$55");

            BeconIndicator bare = objects.Add<BeconIndicator>();
            PriceTag.Refresh(bare, "$55");
            Assert.AreEqual(0, bare.GetComponent<PriceTag>().Labels);

            BeconIndicator indicator = NewIndicator(2);
            indicator.Icons[0] = null;
            PriceTag.Refresh(indicator, "$55");
            Assert.AreEqual(1, indicator.GetComponent<PriceTag>().Labels);
        }

        [Test]
        public void ALabelsAlpha_FollowsItsIconsSprite()
        {
            BeconIndicator indicator = NewIndicator(2);
            PriceTag.Refresh(indicator, "$55");
            indicator.IconSprites[1].color = new Color(1f, 1f, 1f, 0.25f);

            indicator.GetComponent<PriceTag>().FollowIcons();

            Assert.AreEqual(0.25f, LabelOn(indicator.Icons[1]).color.a, 0.001f);
            Assert.AreEqual(1f, LabelOn(indicator.Icons[0]).color.a, 0.001f);
        }

        [Test]
        public void TheCompassIcon_ShowsAndHidesItsPrice()
        {
            CompassIconUI icon = objects.Add<CompassIconUI>();

            icon.SetPrice("$80");
            Assert.AreEqual("$80", icon.ShownPrice);

            icon.SetPrice("");
            Assert.AreEqual("", icon.ShownPrice);
        }

        [Test]
        public void TheCompassIcon_ReusesItsPriceLines()
        {
            CompassIconUI icon = objects.Add<CompassIconUI>();

            icon.SetPrice("$80");
            icon.SetPrice("$130");

            Assert.AreEqual("$130", icon.ShownPrice);
            Assert.AreEqual(1, icon.GetComponentsInChildren<TMP_Text>(true).Length, "a bare icon has no images, so one line");
        }

        [Test]
        public void TheCompassIconsPrice_FadesWithItsIcon()
        {
            CompassIconUI icon = objects.Add<CompassIconUI>();
            GameObject image = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            image.transform.SetParent(icon.transform, false);
            icon.mainIcon = image.GetComponent<UnityEngine.UI.Image>();
            icon.SetPrice("$80");

            icon.mainIcon.color = new Color(1f, 1f, 1f, 0.25f); // the fade animation does this close up
            icon.FollowFade();

            Assert.AreEqual(0.25f, icon.GetComponentInChildren<TMP_Text>(true).alpha, 0.001f);
        }
    }
}
