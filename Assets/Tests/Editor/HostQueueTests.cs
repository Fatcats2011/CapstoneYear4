using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// A client's messages from the host (its states and order changes) wait while this machine's scene changes, then
    /// go in the host's order
    /// </summary>
    public class HostQueueTests
    {
        bool holding;
        readonly List<int> ran = new List<int>();
        HostQueue queue;

        bool IsHolding()
        {
            return holding;
        }

        [SetUp]
        public void SetUp()
        {
            holding = false;
            ran.Clear();
            queue = new HostQueue(IsHolding);
        }

        [Test]
        public void NotHolding_AMessageRunsAtOnce()
        {
            queue.Add(() => ran.Add(1));
            CollectionAssert.AreEqual(new[] { 1 }, ran);
        }

        [Test]
        public void Holding_MessagesWait_ThenRunInOrderOnRelease()
        {
            holding = true;
            queue.Add(() => ran.Add(1));
            queue.Add(() => ran.Add(2));
            Assert.AreEqual(2, queue.Waiting);
            CollectionAssert.IsEmpty(ran);

            holding = false;
            queue.Release();
            CollectionAssert.AreEqual(new[] { 1, 2 }, ran);
        }

        [Test]
        public void AfterTheHold_ANewMessageWaitsBehindTheOnesStillWaiting()
        {
            // A state and an order change keep the host's order even when the scene comes up in between
            holding = true;
            queue.Add(() => ran.Add(1));
            holding = false;
            queue.Add(() => ran.Add(2));
            CollectionAssert.IsEmpty(ran, "1 hasn't gone yet");

            queue.Release();
            CollectionAssert.AreEqual(new[] { 1, 2 }, ran);
        }

        [Test]
        public void Release_WhileStillHolding_RunsNothing()
        {
            holding = true;
            queue.Add(() => ran.Add(1));
            queue.Release();
            CollectionAssert.IsEmpty(ran);
        }

        [Test]
        public void Clear_ForgetsWhatsWaiting()
        {
            holding = true;
            queue.Add(() => ran.Add(1));
            queue.Clear();
            holding = false;
            queue.Release();
            CollectionAssert.IsEmpty(ran);
        }

        [Test]
        public void AMessageThatThrows_IsLogged_AndTheOnesBehindItStillGo()
        {
            // One bad message mustn't hold up every later state and order change
            holding = true;
            queue.Add(Throw);
            queue.Add(() => ran.Add(2));
            holding = false;
            LogAssert.Expect(LogType.Exception, new Regex("a bad message"));

            queue.Release();

            CollectionAssert.AreEqual(new[] { 2 }, ran);
            Assert.AreEqual(0, queue.Waiting);
        }

        static void Throw()
        {
            throw new InvalidOperationException("a bad message");
        }
    }
}
