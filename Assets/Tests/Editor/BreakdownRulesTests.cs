using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The money breakdown on a results card (BreakdownRules): the label keys, how many rows and how much height the
    /// sources that paid need, which sources show, and the four labels in every language
    /// </summary>
    public class BreakdownRulesTests
    {
        [TestCase(MoneySource.Deliveries, "results.deliveries")]
        [TestCase(MoneySource.Steals, "results.steals")]
        [TestCase(MoneySource.Golden, "results.golden")]
        [TestCase(MoneySource.Mode, "results.mode")]
        public void Key_IsTheSourcesLabel(MoneySource source, string key)
        {
            Assert.AreEqual(key, BreakdownRules.Key(source));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        public void Rows_AreTwoSourcesEach(int shown, int rows)
        {
            Assert.AreEqual(rows, BreakdownRules.Rows(shown));
            Assert.AreEqual(rows * BreakdownRules.ROW, BreakdownRules.ExtraHeight(shown));
        }

        [Test]
        public void Shown_SkipsZeros_AndKeepsTheOrder()
        {
            AwardBoard board = new AwardBoard();
            board.SetMoney(1, MoneySource.Mode, 30);
            board.SetMoney(1, MoneySource.Deliveries, 520);
            board.SetMoney(1, MoneySource.Golden, 80);
            board.SetMoney(0, MoneySource.Steals, 5);
            List<MoneySource> shown = new List<MoneySource> { MoneySource.Steals };

            BreakdownRules.Shown(board, 1, shown);

            CollectionAssert.AreEqual(new[] { MoneySource.Deliveries, MoneySource.Golden, MoneySource.Mode }, shown, "the list is refilled, not added to");
        }

        [Test]
        public void Shown_NothingPaid_IsEmpty()
        {
            List<MoneySource> shown = new List<MoneySource> { MoneySource.Mode };

            BreakdownRules.Shown(new AwardBoard(), 0, shown);

            Assert.IsEmpty(shown);
        }

        [TestCase("results.deliveries")]
        [TestCase("results.steals")]
        [TestCase("results.golden")]
        [TestCase("results.mode")]
        public void EveryLabel_IsInEveryLanguage(string key)
        {
            StringsCsv csv = StringsCsv.Load();
            foreach (string locale in LocaleCodes.All)
                Assert.IsNotNull(csv.Get(key, locale), key + " " + locale);
        }
    }
}
