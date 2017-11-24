using System;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;

namespace test
{
    public class SerializeObjectTest
    {
        public static void Test()
        {
            // with SerializeObject 
            {
                var a1 = new A();
                a1.init();
                SerializeObject<A>(a1, "d:\\1.xml");
                var a2 = DeSerializeObject<A>("d:\\1.xml");
                a1.b1.x = 5; // this will change also the value of a1.b2.x
                a2.b1.x = 5; // this will not!!!!! change also the value of a2.b2.x
                MessageBox.Show(
                    "a1.b1.x==a1.b2.x : " + a1.b1.x + "?=" + a1.b2.x + "\r\n" +
                    "a2.b1.1==a2.b2.x : " + a2.b1.x + "?=" + a2.b2.x + "  !!\r\n", "Save.SaveAble"
                    );
            }
            // with saver
            {
                var a1 = new A();
                a1.init();
                Saver.SaveAble.Save("d:\\2.xml", a1);
                var a2 = Saver.SaveAble.Load<A>("d:\\2.xml");
                a1.b1.x = 5; // this will change also the value of a1.b2.x
                a2.b1.x = 5; // this will change also the value of a2.b2.x
                MessageBox.Show(
                    "a1.b1.x==a1.b2.x : " + a1.b1.x + "?=" + a1.b2.x + "\r\n" +
                    "a2.b1.1==a2.b2.x : " + a2.b1.x + "?=" + a2.b2.x + "  ok\r\n", "Save.SaveAble"
                    );
            }
        }

        public class A
        {
            public void init()
            {
                b1 = new B() { x = 100 };
                b2 = b1;
            }
            public B b1;
            public B b2;
        }
        public class B
        {
            public double x;
        }

        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializableObject"></param>
        /// <param name="fileName"></param>
        public static void SerializeObject<T>(T serializableObject, string fileName)
        {
            if (serializableObject == null) { return; }

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlSerializer serializer = new XmlSerializer(serializableObject.GetType());
                using (MemoryStream stream = new MemoryStream())
                {
                    serializer.Serialize(stream, serializableObject);
                    stream.Position = 0;
                    xmlDocument.Load(stream);
                    xmlDocument.Save(fileName);
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }
        }


        /// <summary>
        /// Deserializes an xml file into an object list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T DeSerializeObject<T>(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) { return default(T); }

            T objectOut = default(T);
            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(fileName);
                string xmlString = xmlDocument.OuterXml;

                using (StringReader read = new StringReader(xmlString))
                {
                    Type outType = typeof(T);

                    XmlSerializer serializer = new XmlSerializer(outType);
                    using (XmlReader reader = new XmlTextReader(read))
                    {
                        objectOut = (T)serializer.Deserialize(reader);
                        reader.Close();
                    }

                    read.Close();
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }

            return objectOut;
        }
    }
}
