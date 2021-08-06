using LiteNetLib.Utils;
using System.Collections.Generic;

[System.Serializable]
public struct AttributeAmounts : INetSerializable
{
    private Dictionary<int, short> attributeAmounts;
    public Dictionary<int, short> Dict { get { return attributeAmounts; } }

    public AttributeAmounts(int capacity)
    {
        attributeAmounts = new Dictionary<int, short>(capacity);
    }

    public AttributeAmounts Increase(int id, short value)
    {
        if (attributeAmounts.ContainsKey(id))
            attributeAmounts[id] = (short)(attributeAmounts[id] + 1);
        else
            attributeAmounts.Add(id, 1);
        return this;
    }

    public void Serialize(NetDataWriter writer)
    {
        short length = (short)(attributeAmounts == null ? 0 : attributeAmounts.Count);
        writer.Put(length);
        if (length > 0)
        {
            foreach (var attributeAmount in attributeAmounts)
            {
                writer.Put(attributeAmount.Key);
                writer.Put(attributeAmount.Value);
            }
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        AttributeAmounts data = new AttributeAmounts(0);
        Dictionary<int, short> attributeAmounts = new Dictionary<int, short>();
        short length = reader.GetShort();
        if (length > 0)
        {
            attributeAmounts.Add(reader.GetInt(), reader.GetShort());
            data.attributeAmounts = attributeAmounts;
        }
    }
}