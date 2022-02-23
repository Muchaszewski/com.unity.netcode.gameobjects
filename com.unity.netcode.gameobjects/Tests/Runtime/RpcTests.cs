using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using Unity.Netcode.TestHelpers.Runtime;
using Debug = UnityEngine.Debug;

namespace Unity.Netcode.RuntimeTests
{
    public class RpcTests : NetcodeIntegrationTest
    {
        public class RpcTestNB : NetworkBehaviour
        {
            public event Action<ulong, ServerRpcParams> OnServer_Rpc;
            public event Action OnClient_Rpc;

            [ServerRpc]
            public void MyServerRpc(ulong clientId, ServerRpcParams param = default)
            {
                OnServer_Rpc(clientId, param);
            }

            [ClientRpc]
            public void MyClientRpc()
            {
                OnClient_Rpc();
            }
        }

        protected override int NbClients => 1;

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<RpcTestNB>();
        }

        [UnityTest]
        public IEnumerator TestRpcs()
        {
            // This is the *SERVER VERSION* of the *CLIENT PLAYER*
            var serverClientPlayerResult = new NetcodeIntegrationTestHelpers.ResultWrapper<NetworkObject>();
            var clientId = m_ClientNetworkManagers[0].LocalClientId;
            yield return NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == clientId), m_ServerNetworkManager, serverClientPlayerResult);

            // This is the *CLIENT VERSION* of the *CLIENT PLAYER*
            var clientClientPlayerResult = new NetcodeIntegrationTestHelpers.ResultWrapper<NetworkObject>();
            yield return NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == clientId), m_ClientNetworkManagers[0], clientClientPlayerResult);

            // Setup state
            bool hasReceivedServerRpc = false;
            bool hasReceivedClientRpcRemotely = false;
            bool hasReceivedClientRpcLocally = false;

            clientClientPlayerResult.Result.GetComponent<RpcTestNB>().OnClient_Rpc += () =>
            {
                Debug.Log("ClientRpc received on client object");
                hasReceivedClientRpcRemotely = true;
            };

            clientClientPlayerResult.Result.GetComponent<RpcTestNB>().OnServer_Rpc += (clientId, param) =>
            {
                // The RPC invoked locally. (Weaver failure?)
                Assert.Fail("ServerRpc invoked locally. Weaver failure?");
            };

            serverClientPlayerResult.Result.GetComponent<RpcTestNB>().OnServer_Rpc += (clientId, param) =>
            {
                Debug.Log("ServerRpc received on server object");
                Assert.True(param.Receive.SenderClientId == clientId);
                hasReceivedServerRpc = true;
            };

            serverClientPlayerResult.Result.GetComponent<RpcTestNB>().OnClient_Rpc += () =>
            {
                // The RPC invoked locally. (Weaver failure?)
                Debug.Log("ClientRpc received on server object");
                hasReceivedClientRpcLocally = true;
            };

            // Send ServerRpc
            clientClientPlayerResult.Result.GetComponent<RpcTestNB>().MyServerRpc(clientId);

            // Send ClientRpc
            serverClientPlayerResult.Result.GetComponent<RpcTestNB>().MyClientRpc();

            // Wait for RPCs to be received
            yield return WaitForConditionOrTimeOut(() => hasReceivedServerRpc && hasReceivedClientRpcLocally && hasReceivedClientRpcRemotely);
            
            var clientMessageResult = new MultiInstanceHelpers.CoroutineResultWrapper<bool>();
            var serverMessageResult = new MultiInstanceHelpers.CoroutineResultWrapper<bool>();
            // Wait for RPCs to be received - client and server should each receive one.
            yield return MultiInstanceHelpers.RunMultiple(new[]
            {
                MultiInstanceHelpers.WaitForMessageOfType<ClientRpcMessage>(m_ClientNetworkManagers[0], clientMessageResult),
                MultiInstanceHelpers.WaitForMessageOfType<ServerRpcMessage>(m_ServerNetworkManager, serverMessageResult),
            });

            Assert.True(hasReceivedServerRpc, "ServerRpc was not received");
            Assert.True(hasReceivedClientRpcLocally, "ClientRpc was not locally received on the server");
            Assert.True(hasReceivedClientRpcRemotely, "ClientRpc was not remotely received on the client");
        }
    }
}
