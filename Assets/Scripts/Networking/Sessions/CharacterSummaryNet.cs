using Unity.Collections;
using Unity.Netcode;

namespace ROC.Networking.Sessions
{
    public struct CharacterSummaryNet : INetworkSerializable
    {
        public FixedString64Bytes CharacterId;
        public FixedString64Bytes DisplayName;

        public bool HasCompletedIntro;

        public FixedString64Bytes SceneId;
        public FixedString128Bytes InstanceId;

        public CharacterSummaryNet(
            string characterId,
            string displayName,
            bool hasCompletedIntro,
            string sceneId,
            string instanceId)
        {
            CharacterId = new FixedString64Bytes(characterId);
            DisplayName = new FixedString64Bytes(displayName);
            HasCompletedIntro = hasCompletedIntro;
            SceneId = new FixedString64Bytes(sceneId);
            InstanceId = new FixedString128Bytes(instanceId);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CharacterId);
            serializer.SerializeValue(ref DisplayName);
            serializer.SerializeValue(ref HasCompletedIntro);
            serializer.SerializeValue(ref SceneId);
            serializer.SerializeValue(ref InstanceId);
        }
    }
}