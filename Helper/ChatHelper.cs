using RoR2;
using RoR2.Networking;
using Unity;
using UnityEngine;
using UnityEngine.Networking;

namespace Lunzir.Helper
{
    class ChatHelper
    {
        public static void Send(string message)
        {
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = message
            });
        }
        public static void Send(string message, NetworkConnection networkConnection, short msgType = 59)
        {
            Chat.SimpleChatMessage simpleChat = new Chat.SimpleChatMessage
            {
                baseToken = message,
            };
            NetworkWriter writer = new NetworkWriter();
            writer.StartMessage(msgType);
            writer.Write(simpleChat.GetTypeIndex());
            writer.Write(simpleChat);
            writer.FinishMessage();
            networkConnection?.SendWriter(writer, QosChannelIndex.chat.intVal);
        }

        public static void Send(CharacterMaster master, PickupIndex pickupIndex)
        {
            if (!NetworkServer.active)
            {
                return;
            }
            uint pickupQuantity = 1U;
            if (master.inventory)
            {
                PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                ItemIndex itemIndex = (pickupDef != null) ? pickupDef.itemIndex : ItemIndex.None;
                if (itemIndex != ItemIndex.None)
                {
                    pickupQuantity = (uint)master.inventory.GetItemCount(itemIndex);
                }
            }
            PickupMessage msg = new PickupMessage
            {
                masterGameObject = master.gameObject,
                pickupIndex = pickupIndex,
                pickupQuantity = pickupQuantity
            };
            NetworkServer.SendByChannelToAll(57, msg, QosChannelIndex.chat.intVal);
        }

        public static string ItemTierColor(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.Tier1:
                    return "cSub";
                case ItemTier.Tier2:
                    return "cIsHealing";
                case ItemTier.Tier3:
                    return "cDeath";
                case ItemTier.Lunar:
                    return "cIsUtility";
                case ItemTier.Boss:
                    return "cShrine";
                case ItemTier.VoidTier1:
                case ItemTier.VoidTier2:
                case ItemTier.VoidTier3:
                case ItemTier.VoidBoss:
                    return "cWorldEvent";
                default:
                    return "cKeywordName";
            }
        }
    }
    class PickupMessage : MessageBase
    {
        public void Reset()
        {
            this.masterGameObject = null;
            this.pickupIndex = PickupIndex.none;
            this.pickupQuantity = 0U;
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(this.masterGameObject);
            GeneratedNetworkCode._WritePickupIndex_None(writer, this.pickupIndex);
            writer.WritePackedUInt32(this.pickupQuantity);
        }

        public override void Deserialize(NetworkReader reader)
        {
            this.masterGameObject = reader.ReadGameObject();
            this.pickupIndex = GeneratedNetworkCode._ReadPickupIndex_None(reader);
            this.pickupQuantity = reader.ReadPackedUInt32();
        }

        public GameObject masterGameObject;
        public PickupIndex pickupIndex;
        public uint pickupQuantity;
    }
}
