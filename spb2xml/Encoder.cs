using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace spb2xml
{
    class Encoder
    {
        private SymbolBank bank;
        private BinaryWriter writer;
        private XmlDocument doc;
        private ModelBank models;
        private Dictionary<string, DefinitionElement> metadata;

        public Encoder(string xmlFileUrl, string metadataFileUrl)
        {
            bank = SymbolBank.Instance;
            doc = new XmlDocument();
            doc.Load(xmlFileUrl);
            LoadMetadata(metadataFileUrl);
        }

        public void SetModels(ModelBank mBank)
        {
            models = mBank;
        }

        public void Encode(string spbFileUrl)
        {
            using (FileStream fs = new FileStream(spbFileUrl, FileMode.Create))
            {
                writer = new BinaryWriter(fs);
                WriteHeaders();
                WriteTagData();
                WriteElements(doc.DocumentElement);
            }
        }

        private void LoadMetadata(string metadataFileUrl)
        {
            metadata = new Dictionary<string, DefinitionElement>();
            using (FileStream fs = new FileStream(metadataFileUrl, FileMode.Open))
            using (StreamReader reader = new StreamReader(fs))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length == 2)
                    {
                        Guid guid = Guid.Parse(parts[0]);
                        DefinitionElement element = bank.LookupElement(guid);
                        if (element != null)
                        {
                            metadata[element.Name] = element;
                        }
                    }
                }
            }
        }

        private void WriteHeaders()
        {
            writer.Write((ushort)0xEBAC); // File signature
            for (int i = 0; i < 12; i++)
            {
                writer.Write(0); // Placeholder for headers
            }
        }

        private void WriteTagData()
        {
            foreach (var entry in metadata)
            {
                writer.Write(entry.Value.ID.ToByteArray());
                writer.Write(0); // Placeholder for unknown flag
            }
        }

        private void WriteElements(XmlNode node)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child is XmlElement)
                {
                    WriteElement((XmlElement)child);
                }
            }
        }

        private void WriteElement(XmlElement element)
        {
            if (metadata.TryGetValue(element.Name, out DefinitionElement defElement))
            {
                if (defElement is SetDef)
                {
                    WriteSet((SetDef)defElement, element);
                }
                else if (defElement is PropertyDef)
                {
                    WriteProperty((PropertyDef)defElement, element);
                }
            }
            else
            {
                throw new SPBException("Unknown element: " + element.Name);
            }
        }

        private void WriteSet(SetDef set, XmlElement element)
        {
            long startPosition = writer.BaseStream.Position;
            writer.Write(0); // Placeholder for set size

            foreach (XmlNode child in element.ChildNodes)
            {
                if (child is XmlElement)
                {
                    WriteElement((XmlElement)child);
                }
            }

            long endPosition = writer.BaseStream.Position;
            writer.BaseStream.Position = startPosition;
            writer.Write((int)(endPosition - startPosition - 4)); // Set size
            writer.BaseStream.Position = endPosition;
        }

        private void WriteProperty(PropertyDef prop, XmlElement element)
        {
            TypeDef type = prop.Type;
            string value = element.InnerText;

            switch (type.Name)
            {
                case "TEXT":
                case "MLTEXT":
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(value);
                        writer.Write(bytes.Length);
                        writer.Write(bytes);
                    }
                    break;
                case "ULONG":
                    writer.Write(uint.Parse(value));
                    break;
                case "LONG":
                    writer.Write(int.Parse(value));
                    break;
                case "LONG2":
                    {
                        var parts = value.Split(',');
                        writer.Write(int.Parse(parts[0]));
                        writer.Write(int.Parse(parts[1]));
                    }
                    break;
                case "LONG4":
                    {
                        var parts = value.Split(',');
                        writer.Write(int.Parse(parts[0]));
                        writer.Write(int.Parse(parts[1]));
                        writer.Write(int.Parse(parts[2]));
                        writer.Write(int.Parse(parts[3]));
                    }
                    break;
                case "BOOL":
                    writer.Write(value == "true" ? 1 : 0);
                    break;
                case "FLOAT":
                    writer.Write(float.Parse(value));
                    break;
                case "FLOAT2":
                    {
                        var parts = value.Split(',');
                        writer.Write(float.Parse(parts[0]));
                        writer.Write(float.Parse(parts[1]));
                    }
                    break;
                case "FLOAT4":
                    {
                        var parts = value.Split(',');
                        writer.Write(float.Parse(parts[0]));
                        writer.Write(float.Parse(parts[1]));
                        writer.Write(float.Parse(parts[2]));
                        writer.Write(float.Parse(parts[3]));
                    }
                    break;
                case "DOUBLE":
                    writer.Write(double.Parse(value));
                    break;
                case "BYTE4":
                    {
                        var parts = value.Split(',');
                        writer.Write(byte.Parse(parts[0]));
                        writer.Write(byte.Parse(parts[1]));
                        writer.Write(byte.Parse(parts[2]));
                        writer.Write(byte.Parse(parts[3]));
                    }
                    break;
                case "GUID":
                    writer.Write(new Guid(value).ToByteArray());
                    break;
                case "PBH":
                case "PBH32":
                    {
                        var parts = value.Split(',');
                        writer.Write((uint)(double.Parse(parts[0]) * 65536 * 65536 / 360.0));
                        writer.Write((uint)(double.Parse(parts[1]) * 65536 * 65536 / 360.0));
                        writer.Write((uint)(double.Parse(parts[2]) * 65536 * 65536 / 360.0));
                        writer.Write(0); // Padding
                    }
                    break;
                case "ENUM":
                    {
                        EnumDef enumDef = prop.Enum;
                        int idx = enumDef.values.IndexOf(value);
                        writer.Write(idx);
                    }
                    break;
                case "LLA":
                    {
                        var parts = value.Split(',');
                        writer.Write(long.Parse(parts[0]));
                        writer.Write(long.Parse(parts[1]));
                        writer.Write(uint.Parse(parts[2]));
                        writer.Write(int.Parse(parts[3]));
                    }
                    break;
                case "FILETIME":
                    {
                        // TODO: Implement FILETIME encoding
                    }
                    break;
                default:
                    throw new SPBException("Don't know how to format type " + type.Type + " at " + writer.BaseStream.Position);
            }
        }
    }
}