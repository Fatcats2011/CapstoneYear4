using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The Speed and Low Gravity mutators (MatchPhysics): applied from the match's rules, and back to normal on the way
    /// to the menus and on a rematch, so cans and civilians don't float in the next match
    /// </summary>
    public class MatchPhysicsTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            MatchPhysics.Reset();
            HouseRules.Current = HouseRules.Playing = HouseRules.Normal;
            objects.DestroyAll();
        }

        static HouseRules Mutated => new HouseRules(MatchLength.Normal, true, true, true, GameMode.Standard, 3, 150, true);

        [Test]
        public void Apply_LowGravity_HalvesGravity_AndSpeedScales()
        {
            MatchPhysics.Apply(Mutated);

            Assert.AreEqual(-6.5f, Physics.gravity.y, 0.001f);
            Assert.AreEqual(1.5f, MatchPhysics.SpeedScale);
        }

        [Test]
        public void Apply_Normal_IsTheProjectsGravity()
        {
            MatchPhysics.Apply(HouseRules.Normal);

            Assert.AreEqual(-13f, Physics.gravity.y, 0.001f);
            Assert.AreEqual(1f, MatchPhysics.SpeedScale);
        }

        [Test]
        public void PublicLobby_PlaysNormalPhysics()
        {
            MatchPhysics.Apply(Mutated.ForMatch(true));

            Assert.AreEqual(-13f, Physics.gravity.y, 0.001f);
            Assert.AreEqual(1f, MatchPhysics.SpeedScale);
        }

        [Test]
        public void ToTheMenu_ResetsPhysics()
        {
            GameManager game = objects.Add<GameManager>();
            MatchPhysics.Apply(Mutated);

            game.ApplyGameState(GameState.Menu);

            Assert.AreEqual(-13f, Physics.gravity.y, 0.001f);
            Assert.AreEqual(1f, MatchPhysics.SpeedScale);
        }

        [Test]
        public void ARematch_ResetsPhysics_UntilTheMatchAppliesItsRules()
        {
            GameManager game = objects.Add<GameManager>();
            MatchPhysics.Apply(Mutated);

            game.StartNewMatch();

            Assert.AreEqual(-13f, Physics.gravity.y, 0.001f);
        }
    }
}
