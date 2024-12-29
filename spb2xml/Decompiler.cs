using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;

namespace spb2xml
{
    class Decompiler
    {
        private SymbolBank bank;
        private BinaryReader reader;
        private DefinitionElement[] tags;
        private int ntags;
        private XmlDocument doc;
        private ModelBank models;
        private Dictionary<Guid, DefinitionElement> metadata;
        private int[] headers; // Add a field to store header values
        private List<int> unknownFlags; // Add a field to store unknown flag values

        private static UTF8Encoding encoder = new UTF8Encoding();
        private const string DEC_FORMAT = "0.000";

        public Decompiler(string spbFileUrl)
        {
            bank = SymbolBank.Instance;
            FileStream fs = new FileStream(spbFileUrl, FileMode.Open);
            reader = new BinaryReader(fs);
            metadata = new Dictionary<Guid, DefinitionElement>();
            headers = new int[12]; // Initialize the headers array
            unknownFlags = new List<int>(); // Initialize the unknown flags list
        }

        public void SetModels(ModelBank mBank)
        {
            models = mBank;
        }

        public void Decompile(Stream outStream, string metadataFileUrl)
        {
            ReadHeaders();
            ReadTagData();

            doc = new XmlDocument();
            XmlDeclaration xmlDeclNode = (XmlDeclaration)doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");
            doc.AppendChild(xmlDeclNode);
            ParseElement(null, doc);

            XmlTextWriter writer = new XmlTextWriter(outStream, Encoding.Default);
            writer.Formatting = Formatting.Indented;
            doc.Save(writer);

            SaveMetadata(metadataFileUrl);
        }

        private void ReadHeaders()
        {
            ushort fType = reader.ReadUInt16();
            if (fType != 0xEBAC)
            {
                throw new SPBException("Invalid file ID");
            }
            ntags = 0;
            for (int i = 0; i < 12; i++)
            {
                int v = reader.ReadInt32();
                headers[i] = v; // Store the header value
                if (i == 6) ntags = v;
            }

            tags = new DefinitionElement[ntags];
        }

        private void ReadTagData()
        {
            for (int i = 0; i < (ntags - 1); i++)
            {
                byte[] guidData = reader.ReadBytes(16);
                Guid g = new Guid(guidData);
                DefinitionElement de = bank.LookupElement(g);

                if (de == null)
                {
                    throw new SPBException("Unbound property GUID : " + g.ToString());
                }

                tags[i] = de;
                metadata[g] = de; // Preserve metadata
                int unknownFlag = reader.ReadInt32(); // Read and store the unknown flag
                unknownFlags.Add(unknownFlag);
            }
        }

        private void ParseElement(SymbolDef current, XmlNode node)
        {
            int tagNumber = reader.ReadInt32() - 1;

            if (tagNumber == -1) return;

            if (tagNumber < 0 || tagNumber > ntags)
            {
                throw new SPBException("Invalid tag index " + tagNumber);
            }

            DefinitionElement tagType = tags[tagNumber];
            if (tagType is SetDef)
            {
                SetDef set = (SetDef)tagType;
                ParseSet(current, set, node);
            }
            else if (tagType is PropertyDef)
            {
                PropertyDef pd = (PropertyDef)tagType;
                ParseProperty(current, pd, node);
            }
            else
            {
                throw new SPBException("Unexpected tag type : " + tagType.GetType().Name);
            }
        }

        private void ParseSet(SymbolDef current, SetDef set, XmlNode node)
        {
            int setSize = reader.ReadInt32();
            long endPosition = reader.BaseStream.Position + setSize;

            XmlNode setNode;

            if (current == null || set.Parent != current)
            {
                setNode = doc.CreateElement("", set.Parent.Name + "." + set.Name, "");
                current = set.Parent;
            }
            else
            {
                setNode = doc.CreateElement("", set.Name, "");
            }

            node.AppendChild(setNode);
            while (reader.BaseStream.Position < endPosition)
            {
                ParseElement(current, setNode);
            }
        }

        private void ParseProperty(SymbolDef current, PropertyDef prop, XmlNode node)
        {
            TypeDef type = prop.Type;
            Console.WriteLine(type.Name);
            switch (type.Name)
            {
                case "TEXT":
                case "MLTEXT":
                    {
                        int stringLen = reader.ReadInt32();
                        string s;
                        if (stringLen <= 0)
                        {
                            s = "";
                        }
                        else
                        {
                            s = TextDecode.Decode(reader.ReadBytes(stringLen));
                        }
                        AddProp(current, prop, s, node);
                    }
                    break;
                case "ULONG":
                    {
                        uint uv = reader.ReadUInt32();
                        AddProp(current, prop, Convert.ToString(uv), node);
                    }
                    break;
                case "LONG":
                    {
                        int v = reader.ReadInt32();
                        AddProp(current, prop, Convert.ToString(v), node);
                    }
                    break;

                case "LONG2":
                    {
                        int v1 = reader.ReadInt32();
                        int v2 = reader.ReadInt32();
                        AddProp(current, prop, v1 + "," + v2, node);
                    }
                    break;
                case "LONG4":
                    {
                        int v1 = reader.ReadInt32();
                        int v2 = reader.ReadInt32();
                        int v3 = reader.ReadInt32();
                        int v4 = reader.ReadInt32();
                        AddProp(current, prop, v1 + "," + v2 + "," + v3 + "," + v4, node);
                    }
                    break;

                case "BOOL":
                    {
                        int v = reader.ReadInt32();
                        AddProp(current, prop, (v == 1) ? "true" : "false", node);
                    }
                    break;
                case "FLOAT":
                    {
                        float f = reader.ReadSingle();
                        AddProp(current, prop, f.ToString(DEC_FORMAT), node);
                    }
                    break;
                case "FLOAT2":
                    {
                        float f1 = reader.ReadSingle();
                        float f2 = reader.ReadSingle();
                        AddProp(current, prop, f1.ToString(DEC_FORMAT) + "," + f2.ToString(DEC_FORMAT), node);
                    }
                    break;
                case "FLOAT4":
                    {
                        float f1 = reader.ReadSingle();
                        float f2 = reader.ReadSingle();
                        float f3 = reader.ReadSingle();
                        float f4 = reader.ReadSingle();
                        AddProp(current, prop, f1.ToString(DEC_FORMAT) + "," +
                            f2.ToString(DEC_FORMAT) + "," +
                            f3.ToString(DEC_FORMAT) + "," +
                            f4.ToString(DEC_FORMAT), node);
                    }
                    break;
                case "DOUBLE":
                    {
                        double v = reader.ReadDouble();
                        AddProp(current, prop, v.ToString(DEC_FORMAT), node);
                    }
                    break;
                case "BYTE4":
                    {
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();
                        byte b4 = reader.ReadByte();
                        AddProp(current, prop, b1 + "," + b2 + "," + b3 + b4, node);
                    }
                    break;
                case "GUID":
                    {
                        byte[] gd = reader.ReadBytes(16);
                        Guid g = new Guid(gd);
                        if (models != null)
                        {
                            string modelName = models.Lookup(g);
                            if (modelName != null)
                            {
                                XmlComment com = doc.CreateComment("Model: " + modelName);
                                node.AppendChild(com);
                            }
                        }
                        AddProp(current, prop, "{" + new Guid(gd).ToString().ToUpper() + "}", node);
                    }
                    break;
                case "PBH":
                case "PBH32":
                    {
                        double p = reader.ReadUInt32() / ((double)65536 * 65536) * 360.0;
                        double b = reader.ReadUInt32() / ((double)65536 * 65536) * 360.0;
                        double h = reader.ReadUInt32() / ((double)65536 * 65536) * 360.0;
                        reader.ReadInt32();
                        AddProp(current, prop, p.ToString(DEC_FORMAT) + "," + b.ToString(DEC_FORMAT) + "," + h.ToString(DEC_FORMAT), node);
                    }
                    break;
                case "ENUM":
                    {
                        EnumDef enumDef = prop.Enum;
                        if (enumDef == null)
                        {
                            throw new SPBException("Enum type without values");
                        }
                        int idx = reader.ReadInt32();
                        string val = enumDef[idx];
                        AddProp(current, prop, val, node);
                    }
                    break;
                case "LLA":
                    {
                        long lat = reader.ReadInt64();
                        long lon = reader.ReadInt64();
                        uint alt1 = reader.ReadUInt32();
                        int alt2 = reader.ReadInt32();
                        LLA l = new LLA(lat, lon, alt1, alt2);
                        AddProp(current, prop, l.ToString("D2"), node);
                    }
                    break;
                case "FILETIME":
                    {
                        // TODO: filetime
                    }
                    break;
                default:
                    throw new SPBException("Don't know how to format type " + type.Type + " at " + reader.BaseStream.Position);
            }
        }

        private void AddProp(SymbolDef current, PropertyDef pd, String text, XmlNode node)
        {
            if (pd.IsAttribute())
            {
                XmlAttribute aNode = doc.CreateAttribute(pd.Name);
                aNode.Value = text;
                node.Attributes.Append(aNode);
            }
            else
            {
                String propName = pd.Name;
                if (pd.SymbolContext != null &&
                    pd.SymbolContext != current)
                    propName = pd.SymbolContext.Name + "." + propName;

                XmlNode pNode = doc.CreateElement("", propName.TrimEnd(), "");
                XmlNode tNode = doc.CreateTextNode(text);
                pNode.AppendChild(tNode);
                node.AppendChild(pNode);
            }
        }

        private void SaveMetadata(string metadataFileUrl)
        {
            using (FileStream fs = new FileStream(metadataFileUrl, FileMode.Create))
            using (StreamWriter writer = new StreamWriter(fs))
            {
                foreach (var entry in metadata)
                {
                    string metadataEntry = entry.Value.Name;
                    if (entry.Value is SetDef setDef && setDef.Parent != null)
                    {
                        metadataEntry = $"{setDef.Parent.Name}.{setDef.Name}";
                    }
                    writer.WriteLine($"{entry.Key}:{metadataEntry}");
                }
                // Save headers
                writer.WriteLine("Headers:" + string.Join(",", headers));
                // Save unknown flags
                writer.WriteLine("UnknownFlags:" + string.Join(",", unknownFlags));
            }
        }
    }
}
