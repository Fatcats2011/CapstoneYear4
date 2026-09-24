using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace DoA.Tests
{
    /// <summary>
    /// The online packages are installed (docs/online.md): Netcode for GameObjects on the 1.x line (2.x needs Unity 6),
    /// the community Steam transport and ParrelSync. Types are looked up by name so these tests compile without them
    /// </summary>
    public class NetworkPackagesTests
    {
        [Test]
        public void Netcode_IsInstalledOnThe1xLine()
        {
            Type manager = FindType("Unity.Netcode.NetworkManager");
            Assert.IsNotNull(manager, "Netcode for GameObjects");

            StringAssert.StartsWith("1.", PackageInfo.FindForAssembly(manager.Assembly).version);
        }

        [Test]
        public void SteamTransport_IsInstalled()
        {
            // Its class only compiles when both Netcode and Steamworks.NET are present
            Assert.IsNotNull(FindType("Netcode.Transports.SteamNetworkingSocketsTransport"));
        }

        [Test]
        public void ParrelSync_IsInstalled()
        {
            Assert.IsNotNull(FindType("ParrelSync.ClonesManager"));
        }

        static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);
        }
    }
}
