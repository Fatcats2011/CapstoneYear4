using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    public class LogCollectorTests
    {
        [Test]
        public void RecordsErrorsAndExceptions_NotMessagesOrWarnings()
        {
            using (LogCollector collector = new LogCollector())
            {
                Debug.Log("just information");
                Debug.LogWarning("just a warning");
                LogAssert.Expect(LogType.Error, "something broke");
                Debug.LogError("something broke");
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: it threw"));
                Debug.LogException(new InvalidOperationException("it threw"));

                Assert.AreEqual(2, collector.Problems.Count);
                StringAssert.Contains("something broke", collector.Problems[0]);
                StringAssert.Contains("it threw", collector.Problems[1]);
            }
        }

        [Test]
        public void StopsListeningOnceDisposed()
        {
            LogCollector collector = new LogCollector();
            collector.Dispose();

            LogAssert.Expect(LogType.Error, "after dispose");
            Debug.LogError("after dispose");

            Assert.AreEqual(0, collector.Problems.Count);
        }
    }
}
