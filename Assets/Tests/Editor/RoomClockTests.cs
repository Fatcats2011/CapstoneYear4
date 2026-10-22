using System;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The Dispatch room's wall clock (RoomClock) tells the computer's time. Its hands were modelled at ten to two, so
    /// the clock learns from them where twelve and three are, whichever way the model came in
    /// </summary>
    public class RoomClockTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void Angles_ClockwiseFromTwelve()
        {
            Assert.AreEqual(90f, RoomClock.HourAngle(new DateTime(2026, 10, 21, 3, 0, 0)), 1e-3f);
            Assert.AreEqual(0f, RoomClock.MinuteAngle(new DateTime(2026, 10, 21, 3, 0, 0)), 1e-3f);
            Assert.AreEqual(315f, RoomClock.HourAngle(new DateTime(2026, 10, 21, 22, 30, 0)), 1e-3f, "10:30 pm");
            Assert.AreEqual(180f, RoomClock.MinuteAngle(new DateTime(2026, 10, 21, 22, 30, 0)), 1e-3f);
            Assert.AreEqual(93f, RoomClock.MinuteAngle(new DateTime(2026, 10, 21, 1, 15, 30)), 1e-3f, "the seconds move the minute hand");
        }

        // A hand: its pivot at the clock's centre, its mesh out along the direction it points
        Transform Hand(string name, Vector3 pointing)
        {
            GameObject hand = objects.NewGameObject(name);
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(hand.transform, false);
            mesh.transform.localScale = Vector3.one * 0.1f;
            mesh.transform.position = pointing.normalized;
            return hand.transform;
        }

        static Vector3 Pointing(Transform hand)
        {
            return (hand.GetComponentInChildren<Renderer>().bounds.center - hand.position).normalized;
        }

        [TestCase(false)]
        [TestCase(true)] // the model came in mirrored
        public void Show_PointsTheHandsAtTheTime(bool mirrored)
        {
            // Ten to two: the hour hand at two, the minute hand at ten. Twelve is up, three is right (or left, mirrored)
            float side = mirrored ? -1 : 1;
            Vector3 up = Vector3.up, right = Vector3.right * side;
            Transform hour = Hand("Clock_Hour", up * Mathf.Cos(60 * Mathf.Deg2Rad) + right * Mathf.Sin(60 * Mathf.Deg2Rad));
            Transform minute = Hand("Clock_Minute", up * Mathf.Cos(60 * Mathf.Deg2Rad) - right * Mathf.Sin(60 * Mathf.Deg2Rad));
            RoomClock clock = objects.Add<RoomClock>();
            clock.hourHand = hour;
            clock.minuteHand = minute;
            clock.Learn();

            clock.Show(new DateTime(2026, 10, 21, 3, 0, 0));
            Assert.That(Vector3.Dot(Pointing(hour), right), Is.GreaterThan(0.999f), "3:00: the hour hand at three");
            Assert.That(Vector3.Dot(Pointing(minute), up), Is.GreaterThan(0.999f), "3:00: the minute hand at twelve");

            clock.Show(new DateTime(2026, 10, 21, 7, 30, 0));
            Vector3 half7 = (up * Mathf.Cos(225 * Mathf.Deg2Rad) + right * Mathf.Sin(225 * Mathf.Deg2Rad)).normalized;
            Assert.That(Vector3.Dot(Pointing(hour), half7), Is.GreaterThan(0.999f), "7:30: the hour hand halfway past seven");
            Assert.That(Vector3.Dot(Pointing(minute), -up), Is.GreaterThan(0.999f), "7:30: the minute hand at six");
        }

        [Test]
        public void Show_WithoutHands_DoesNothing()
        {
            RoomClock clock = objects.Add<RoomClock>();

            Assert.DoesNotThrow(() => { clock.Learn(); clock.Show(DateTime.Now); });
        }
    }
}
