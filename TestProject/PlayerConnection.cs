using System;
using MonoDevelop.Debugger.Soft.Unity;
using NUnit.Framework;

namespace PlayerConnection_spec
{
    [TestFixture]
    public class ParsingPlayerConnectionString
    {
        [Test]
        public void XboxOneTestCase()
        {
            var dictionary = PlayerConnection.PlayerInfo.ParsePlayerString(
                @"[IP] 192.168.1.9 [Port] 4600 [Flags] 3 [Guid] 1343323326 [EditorId] 2183797363 [Version] 1048832 [Id] XboxOnePlayer(192.168.1.9):4601 [Debug] 1");
            Assert.AreEqual("192.168.1.9", dictionary["ip"]);
            Assert.AreEqual("4600", dictionary["port"]);
            Assert.AreEqual("3", dictionary["flags"]);
            Assert.AreEqual("1343323326", dictionary["guid"]);
            Assert.AreEqual("2183797363", dictionary["editorid"]);
            Assert.AreEqual("1048832", dictionary["version"]);
            Assert.AreEqual("XboxOnePlayer(192.168.1.9):4601", dictionary["id"]);
            Assert.AreEqual("1", dictionary["debug"]);
        }

        [Test]
        public void PS4PlayerTestCase()
        {
            var dictionary = PlayerConnection.PlayerInfo.ParsePlayerString(
                @"[IP] 192.168.1.4 [Port] 35037 [Flags] 3 [Guid] 1906011430 [EditorId] 2225513615 [Version] 1048832 [Id] PS4Player(192.168.1.4):4601 [Debug] 1");
            Assert.AreEqual("192.168.1.4", dictionary["ip"]);
            Assert.AreEqual("35037", dictionary["port"]);
            Assert.AreEqual("3", dictionary["flags"]);
            Assert.AreEqual("1906011430", dictionary["guid"]);
            Assert.AreEqual("2225513615", dictionary["editorid"]);
            Assert.AreEqual("1048832", dictionary["version"]);
            Assert.AreEqual("PS4Player(192.168.1.4):4601", dictionary["id"]);
            Assert.AreEqual("1", dictionary["debug"]);
        }
    }

    [TestFixture]
    public class PlayerInfo
    {
        [Test]
        public void XboxOneTestCase()
        {
            var playerInfo = PlayerConnection.PlayerInfo.Parse(
                @"[IP] 192.168.1.9 [Port] 4600 [Flags] 3 [Guid] 1343323326 [EditorId] 2183797363 [Version] 1048832 [Id] XboxOnePlayer(192.168.1.9):4601 [Debug] 1");
            Assert.AreEqual("192.168.1.9", playerInfo.m_IPEndPoint.Address.ToString());
            Assert.AreEqual(4600, playerInfo.m_IPEndPoint.Port);
            Assert.AreEqual(3, playerInfo.m_Flags);
            Assert.AreEqual(1343323326, playerInfo.m_Guid);
            Assert.AreEqual(2183797363, playerInfo.m_EditorGuid);
            Assert.AreEqual(1048832, playerInfo.m_Version);
            Assert.AreEqual("XboxOnePlayer(192.168.1.9):4601", playerInfo.m_Id);
            Assert.AreEqual(true, playerInfo.m_AllowDebugging);
        }

        [Test]
        public void PS4PlayerTestCase()
        {
            var playerInfo = PlayerConnection.PlayerInfo.Parse(
                @"[IP] 192.168.1.4 [Port] 35037 [Flags] 3 [Guid] 1906011430 [EditorId] 2225513615 [Version] 1048832 [Id] PS4Player(192.168.1.4):4601 [Debug] 1");
            Assert.AreEqual("192.168.1.4", playerInfo.m_IPEndPoint.Address.ToString());
            Assert.AreEqual(35037, playerInfo.m_IPEndPoint.Port);
            Assert.AreEqual(3, playerInfo.m_Flags);
            Assert.AreEqual(1906011430, playerInfo.m_Guid);
            Assert.AreEqual(2225513615, playerInfo.m_EditorGuid);
            Assert.AreEqual(1048832, playerInfo.m_Version);
            Assert.AreEqual("PS4Player(192.168.1.4):4601", playerInfo.m_Id);
            Assert.AreEqual(true, playerInfo.m_AllowDebugging);
        }
    }
}
