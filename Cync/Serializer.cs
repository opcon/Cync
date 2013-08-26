using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Cync
{
    class Serializer
    {
        public Serializer()
        {
        }

        public void SerializeObject(string filename, DeviceCollection objectToSerialize)
        {
            Stream stream = File.Open(filename, FileMode.Create);
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, objectToSerialize);
            stream.Close();
        }

        public DeviceCollection DeSerializeObject(string filename)
        {
            DeviceCollection objectToSerialize;
            Stream stream = File.Open(filename, FileMode.Open);
            IFormatter formatter = new BinaryFormatter();
            objectToSerialize = (DeviceCollection) formatter.Deserialize(stream);
            stream.Close();
            return objectToSerialize;
        }
    }
}