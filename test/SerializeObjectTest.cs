using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;

namespace test
{
    public  class SerializeObjectTest
    {
        public SerializeObjectTest()
        {
            // with SerializeObject 
            {
                var a1 = new A();
                a1.init();
                SerializeObject<A>(a1, "d:\\1.xml");
                var a2 = DeSerializeObject<A>("d:\\1.xml");
                a1.b[0].a = 4;
                a2.b[0].a = 4;
                MessageBox.Show(
                    "a1.b[0].a==a1.b[2].a : " + a1.b[0].a + "?=" + a1.b[2].a + "\r\n" +
                    "a2.b[0].a==a2.b[2].a : " + a2.b[0].a + "?=" + a2.b[2].a + " !!!!!\r\n", "SerializeObject"
                    );
            }
            // with saver
            {
                var a1 = new A();
                a1.init();
                Saver.SaveAble.Save("d:\\1.xml", a1);
                var a2 = Saver.SaveAble.Load<A>("d:\\1.xml");
                a1.b[0].a = 4;
                a2.b[0].a = 4;
                MessageBox.Show(
                    "a1.b[0].a==a1.b[2].a : " + a1.b[0].a + "?=" + a1.b[2].a + "\r\n" +
                    "a2.b[0].a==a2.b[2].a : " + a2.b[0].a + "?=" + a2.b[2].a + "  ok\r\n", "Save.SaveAble"
                    );
            }
        }

        [Serializable]
        public class A
        {
            public void init()
            {
                var b1 = new B() { a = 1, b = 2, c = 3 };
                var b2 = new B() { a = 10, b = 20, c = 30 };
                b = new List<B>();
                b.Add(b1);
                b.Add(b2);
                b.Add(b1);
                b.Add(b2);
            }
            public List<B> b;
        }
        [Serializable]
        public class B
        {
            public double a, b, c;
        }

        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializableObject"></param>
        /// <param name="fileName"></param>
        public void SerializeObject<T>(T serializableObject, string fileName)
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
        public T DeSerializeObject<T>(string fileName)
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
