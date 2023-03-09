using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using XmlSerDe.Common;
using XmlSerDe.Components.Exhauster;
using XmlSerDe.Components.Injector;
using XmlSerDe.Tests.Complex.Subject;
using Xunit;

namespace XmlSerDe.Tests.Complex
{
    public class ComplexFixture
    {
        public static readonly XmlSerializer SystemXmlSerializer = new XmlSerializer(
            typeof(InfoContainer),
            new[]
            {
                typeof(Derived3Info),
                typeof(Derived1Info),
                typeof(Derived2Info),
                typeof(SerializeKeyValue),
                typeof(PerformanceTime)
            }
            );

        public static readonly InfoContainer DefaultObject = new InfoContainer
        {
            InfoCollection = new List<BaseInfo>
            {
                new Derived3Info
                {
                    Email = "example@example.com"
                },
                new Derived1Info
                {
                    BasePersonificationInfo = RawString
                },
                new Derived2Info
                {
                    HotKeyUsed = false,
                    StepsCounter = 1,
                    EventsTime = new List<SerializeKeyValue>
                    {
                        new SerializeKeyValue
                        {
                            Key = KeyValueKindEnum.Three,
                            Value = new PerformanceTime { SecondsSpan = 3, StartTime = DateTime.Parse("2022-09-28T14:51:39.2438815+03:00") }
                        },
                        new SerializeKeyValue
                        {
                            Key = KeyValueKindEnum.One,
                            Value = new PerformanceTime { SecondsSpan = 0, StartTime = DateTime.Parse("2022-09-28T14:28:00.5009069+03:00") }
                        },
                        new SerializeKeyValue
                        {
                            Key = KeyValueKindEnum.Two,
                            Value = new PerformanceTime { SecondsSpan = 1, StartTime = DateTime.Parse("2022-09-28T14:28:02.3089553+03:00") }
                        },
                    }
                }
            }
        };

        public const string RawString = "my string !@#$%^&*()_+|-=\\';[]{},./<>?";
        //https://coderstoolbox.net/string/#!encoding=xml&action=encode&charset=us_ascii
        public const string XmlEncodedString = "my string !@#$%^&amp;*()_+|-=\\&#39;;[]{},./&lt;&gt;?";

        public const string AuxXml = @"
<InfoContainer>
    <InfoCollection>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived3Info"">
            <Email>example@example.com</Email>
        </BaseInfo>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived1Info"">
            <BasePersonificationInfo>my string !@#$%^&amp;*()_+|-=\&#39;;[]{},./&lt;&gt;?</BasePersonificationInfo>
        </BaseInfo>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived2Info"">
            <HotKeyUsed>false</HotKeyUsed>
            <StepsCounter>1</StepsCounter>
            <EventsTime>
                <SerializeKeyValue>
                    <Key>Three</Key>
                    <Value>
                        <StartTime>2022-09-28T14:51:39.2438815+03:00</StartTime>
                        <SecondsSpan>3</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
                <SerializeKeyValue>
                    <Key>One</Key>
                    <Value>
                        <StartTime>2022-09-28T14:28:00.5009069+03:00</StartTime>
                        <SecondsSpan>0</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
                <SerializeKeyValue>
                    <Key>Two</Key>
                    <Value>
                        <StartTime>2022-09-28T14:28:02.3089553+03:00</StartTime>
                        <SecondsSpan>1</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
            </EventsTime>
        </BaseInfo>
    </InfoCollection>
</InfoContainer>
";

        public const string PartXml = @"          <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived3Info"">
<Email>example@example.com</Email>
</BaseInfo>";

        public const string InternalsXml = @"
    <InfoCollection>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived3Info"">
            <Email>example@example.com</Email>
        </BaseInfo>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived1Info"">
            <BasePersonificationInfo>my string !@#$%^&amp;*()_+|-=\&#39;;[]{},./&lt;&gt;?</BasePersonificationInfo>
        </BaseInfo>
        <BaseInfo xmlns:p3=""http://www.w3.org/2001/XMLSchema-instance"" p3:type=""Derived2Info"">
            <HotKeyUsed>false</HotKeyUsed>
            <StepsCounter>1</StepsCounter>
            <EventsTime>
                <SerializeKeyValue>
                    <Key>Three</Key>
                    <Value>
                        <StartTime>2022-09-28T14:51:39.2438815+03:00</StartTime>
                        <SecondsSpan>3</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
                <SerializeKeyValue>
                    <Key>One</Key>
                    <Value>
                        <StartTime>2022-09-28T14:28:00.5009069+03:00</StartTime>
                        <SecondsSpan>0</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
                <SerializeKeyValue>
                    <Key>Two</Key>
                    <Value>
                        <StartTime>2022-09-28T14:28:02.3089553+03:00</StartTime>
                        <SecondsSpan>1</SecondsSpan>
                    </Value>
                </SerializeKeyValue>
            </EventsTime>
        </BaseInfo>
    </InfoCollection>
";

        public ComplexFixture()
        {
            //heat up
            _ = Deserialize_SystemXml(AuxXml);
            _ = Deserialize_XmlSerDe(AuxXml.AsSpan());

            Deserialize_CheckForEquality();
            Serialize_CheckForEquality();
        }

        [Fact]
        public void Serialize_CheckForEquality()
        {
            //serialize
            var ser_system = Serialize_SystemXml(DefaultObject);
            var ser_xmlserde = Serialize_XmlSerDe(DefaultObject);

            //deserialize with different deserializer
            var first = Deserialize_SystemXml(ser_xmlserde);
            var second = Deserialize_XmlSerDe(
                global::XmlSerDe.Generator.Producer.BuiltinCodeHelper.CutXmlHead(
                    ser_system.AsSpan()
                    )
                );

            CheckForEquality(first, second);
        }

        [Fact]
        public void Deserialize_CheckForEquality()
        {
            var deser_systemxml = Deserialize_SystemXml(AuxXml);
            var deser_xmlserde = Deserialize_XmlSerDe(AuxXml.AsSpan());

            CheckForEquality(deser_systemxml, deser_xmlserde);
        }

        private void CheckForEquality(InfoContainer first, InfoContainer second)
        {
            if (first.InfoCollection.Count != second.InfoCollection.Count)
            {
                throw new Exception("InfoCollection.Count");
            }

            {
                var firstic = (Derived3Info)first.InfoCollection[0];
                var secondic = (Derived3Info)second.InfoCollection[0];
                if (firstic.InfoType != secondic.InfoType)
                {
                    throw new Exception("InfoType");
                }
                if (firstic.PhoneNumber != secondic.PhoneNumber)
                {
                    throw new Exception("PhoneNumber");
                }
                if (firstic.Email != secondic.Email)
                {
                    throw new Exception("Email");
                }
            }
            {
                var firstic = (Derived1Info)first.InfoCollection[1];
                var secondic = (Derived1Info)second.InfoCollection[1];
                if (firstic.InfoType != secondic.InfoType)
                {
                    throw new Exception("InfoType");
                }
                if (firstic.BasePersonificationInfo != secondic.BasePersonificationInfo)
                {
                    throw new Exception("BasePersonificationInfo");
                }
            }
            {
                var firstic = (Derived2Info)first.InfoCollection[2];
                var secondic = (Derived2Info)second.InfoCollection[2];
                if (firstic.InfoType != secondic.InfoType)
                {
                    throw new Exception("InfoType");
                }
                if (firstic.StepsCounter != secondic.StepsCounter)
                {
                    throw new Exception("StepsCounter");
                }
                if (firstic.HotKeyUsed != secondic.HotKeyUsed)
                {
                    throw new Exception("HotKeyUsed");
                }
                if (firstic.EventsTime.Count != secondic.EventsTime.Count)
                {
                    throw new Exception("EventsTime.Count");
                }
                for (var eti = 0; eti < firstic.EventsTime.Count; eti++)
                {
                    var firstet = firstic.EventsTime[eti];
                    var secondet = secondic.EventsTime[eti];
                    if (firstet.Key != secondet.Key)
                    {
                        throw new Exception("Key");
                    }
                    if (firstet.Value.StartTime != secondet.Value.StartTime)
                    {
                        throw new Exception("Value.StartTime");
                    }
                    if (firstet.Value.SecondsSpan != secondet.Value.SecondsSpan)
                    {
                        throw new Exception("Value.SecondsSpan");
                    }
                }
            }
        }




        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Serialize_XmlSerDe(
            InfoContainer infoContainer
            )
        {
            var sb = new DefaultStringBuilderExhauster();
            XmlSerializerDeserializer.Serialize(sb, infoContainer, false);
            var xml = sb.ToString();
            return xml;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static string Serialize_SystemXml(
            InfoContainer infoContainer
            )
        {
            using var ms = new MemoryStream();
            SystemXmlSerializer.Serialize(ms, infoContainer);
            var xml = Encoding.UTF8.GetString(ms.GetBuffer().AsSpan(0, (int)ms.Length));
            return xml;
        }


        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static InfoContainer Deserialize_SystemXml(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                var r = (InfoContainer)SystemXmlSerializer.Deserialize(reader);
                return r;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static InfoContainer Deserialize_XmlSerDe(ReadOnlySpan<char> xml)
        {
            XmlSerializerDeserializer.Deserialize(DefaultInjector.Instance, xml, out InfoContainer r);
            return r;
        }


    }
}
