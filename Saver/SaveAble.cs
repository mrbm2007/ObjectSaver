//#define DEBUG
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.ComponentModel;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

//[assembly: Obfuscation(Feature = "Apply to member * when field and private: renaming", Exclude = true)]


namespace Saver
{
    /// <summary>
    /// Save / Load ability 
    /// </summary>
    [Obfuscation(Feature = "Apply to member *k__BackingField when field: renaming", Exclude = true)]
    public class SaveAble
    {
        #region Static settings
        public class Settings
        {
            #region static settings
            /// <summary>
            /// مساوی بودن آبجکت های این کلاس ها با استفاده از آی دی چک نخواهد شد
            /// </summary>
            public static List<Type> IgnorUIDs = new List<Type>();
            /// <summary>
            /// این نوع ها ذخیره نمی شوند
            /// </summary>
            public static List<Type> IgnorTypes = new List<Type>();
            /// <summary>
            /// این آبجک ها ذخیره نمی شوند
            /// </summary>
            public static List<Object> IgnorSaveObjects = new List<Object>();
            /// <summary>
            /// save xml file formatted?
            /// </summary>
            public static bool FormatedXML = true;
            [ThreadStatic]
            public static double LoadedVersion;
            /// <summary>
            /// saver version (Assembly)
            /// </summary>
            public static double Version
            {
                get
                {
                    var v = typeof(SaveAble).Assembly.GetName().Version;
                    return v.Major + Convert.ToDouble("0." + v.Minor);
                }
            }
            #endregion

            static object lock_obj = new object();
            /// <summary>
            /// path to the log file, for debug purpose 
            /// </summary>
            public static string debug_file
            {
                get { return Application.ExecutablePath + "." + System.Threading.Thread.CurrentThread.ManagedThreadId + ".saver"; }
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="type"></param>
            /// <param name="Loader">simple for string: (str) => Convert.ToInt32(str)</param>
            /// <param name="Saver">simple for string: (v) => v + ""</param>
            /// <param name="in_first_place"></param>
            /// <returns></returns>
            public static SaveAbleType AddSaveAbleType(Type type, LoadFuncSimple Loader, SaveFuncSimple Saver = null, bool in_first_place = true)
            {
                return AddSaveAbleType(type, (t, n, r, v) => Loader(Settings.GetNodeVal(n, r)), (n, v, r) => Settings.AddNode(n, Saver(v), r), in_first_place);
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="type"></param>
            /// <param name="Loader">simple for string: (t, n, r, v) => Convert.ToInt32(Settings.GetNodeVal(n, r))</param>
            /// <param name="Saver">simple for string: (n, v, r) => Settings.AddNode(n, v + "", r)</param>
            /// <param name="in_first_place"></param>
            /// <returns></returns>
            public static SaveAbleType AddSaveAbleType(Type type, LoadFunc Loader, SaveFunc Saver = null, bool in_first_place = true)
            {
                RegisterTypesIfNot();
                var st = new SaveAbleType() { type = type, Loader = Loader };
                if (Saver != null)
                    st.Saver = Saver;
                if (in_first_place)
                    Types.Insert(0, st);
                else
                    Types.Add(st);
                return st;
            }

            /// <summary>
            /// defined types for custom save/load
            /// </summary>
            public static List<SaveAbleType> Types = new List<SaveAbleType>();
            /// <summary>
            /// Save/Load for object
            /// </summary>
            public static SaveAbleType SaveAbleObjectType;

            /// <summary>
            /// custom save/load for specific type
            /// </summary>
            public class SaveAbleType
            {
                public Type type;
                /// <summary>
                /// simple for string: (t, n, r, v) => Convert.ToInt32(Settings.GetNodeVal(n, r))
                /// </summary>
                public LoadFunc Loader;
                /// <summary>
                /// simple for string: (n, v, r) => Settings.AddNode(n, v + "", r);
                /// </summary>
                public SaveFunc Saver = (n, v, r) => AddNode(n, v + "", r);

                public bool CanBeUsedForDerivativedTypes = true;
                public static SaveAbleType Get(Type type)
                {
                    RegisterTypesIfNot();
                    foreach (var st in Types)
                        if (st.type == type)
                            return st;
                    if (type.IsSubclassOf(typeof(SaveAble)))
                        return SaveAbleObjectType;
                    foreach (var st in Types)
                        if (st.Check(type))
                            return st;
                    Types.Add(new SaveAbleType() { type = type, Saver = SaveAbleObjectType.Saver, Loader = SaveAbleObjectType.Loader, CanBeUsedForDerivativedTypes = false });
                    return Types[Types.Count - 1];
                }
                internal bool Check(Type T)
                {
                    if (CanBeUsedForDerivativedTypes) return T == type || T.IsSubclassOf(type);
                    else return T == type;
                }
            }
            internal static List<FieldInfo> GetFields(Type type)
            {
                var bindingFlags = BindingFlags.NonPublic |
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.FlattenHierarchy;
                if (Settings.saveStaticFields != Settings.StaticFieldsBehavior.DontSaveAtAll)
                    bindingFlags |= BindingFlags.Static;

                var fieldInfos = type.GetFields(bindingFlags);

                // If this class doesn't have a base, don't waste any time
                if (type == typeof(object) || type.BaseType == typeof(object))
                    return Union(fieldInfos);
                else
                {   // Otherwise, collect all types up to the furthest base class
                    var currentType = type.BaseType;
                    var fieldInfoList = new List<FieldInfo>(fieldInfos);
                    fieldInfoList = Union(fieldInfoList);
                    do
                    {
                        fieldInfos = currentType.GetFields(bindingFlags);
                        Union(ref fieldInfoList, fieldInfos);
                        currentType = currentType.BaseType;
                    }
                    while (currentType != typeof(object));
                    return fieldInfoList;
                }
            }

            static bool FieldInfoEquals(FieldInfo x, FieldInfo y)
            {
                return x.DeclaringType == y.DeclaringType && x.Name == y.Name;
            }
            static List<FieldInfo> Union(ICollection<FieldInfo> list)
            {
                var list1 = new List<FieldInfo>() { Capacity = list.Count };
                foreach (var item in list)
                    if (!item.IsLiteral)
                    {
                        /*var found = false;
                        foreach (var item1 in list1)
                            if (FieldInfoEquals(item1, item))
                            {
                                found = true;
                                break;
                            }
                        if (!found)*/
                        list1.Add(item);
                    }
                return list1;
            }

            static void Union(ref List<FieldInfo> list1, FieldInfo[] list2)
            {
                foreach (var item2 in list2)
                    if (!item2.IsLiteral)
                    {
                        var found = false;
                        foreach (var item1 in list1)
                            if (FieldInfoEquals(item1, item2))
                            {
                                found = true;
                                break;
                            }
                        if (!found)
                            list1.Add(item2);
                    }
            }

            #region XML Tools
            public static XmlNode AddNode(string name, string value, XmlNode root, string atribName = "val")
            {
                name = ValidName(name);
                try
                {
                    XmlNode node = root.AppendChild(xml.CreateElement(name));
                    node.Attributes.Append(node.OwnerDocument.CreateAttribute(atribName)).Value = Tools.Encrypt(value);
                    return node;
                }
                catch (Exception ex) { Settings.Error(ex); return null; }
            }
            public static void SetAttrib(string name, string value, XmlNode node)
            {
                node.Attributes.Append(node.OwnerDocument.CreateAttribute(name)).Value = Tools.Encrypt(value);
            }
            public static XmlNode GetNode(string name, XmlNode root)
            {
                name = ValidName(name);
                foreach (XmlNode n in root.ChildNodes)
                    if (n.Name == name)
                        return n;
                return root.AppendChild(xml.CreateElement(name));
            }
            public static string GetNodeVal(string name, XmlNode root, string atribName = "val")
            {
                name = ValidName(name);
                foreach (XmlNode n in root.ChildNodes)
                    if (n.Name == name)
                    {
                        if (n.Attributes.GetNamedItem(atribName) != null)
                            return Tools.Decrypt(n.Attributes.GetNamedItem(atribName).Value);
                        else if (n.Attributes.GetNamedItem("null") != null && n.Attributes.GetNamedItem("null").Value == "1")
                            return null;
                    }
                return GetAttrib(name, root);
            }
            public static bool HasNode(string name, XmlNode root)
            {
                name = ValidName(name);
                foreach (XmlNode n in root.ChildNodes)
                    if (n.Name == name)
                        return true;
                return false;
            }
            public static bool HasAttrib(string name, XmlNode node)
            {
                return node.Attributes.GetNamedItem(name) != null;
            }

            public static string GetAttrib(string name, XmlNode node)
            {
                var res = node.Attributes.GetNamedItem(name);
                if (res != null)
                    return Tools.Decrypt(res.Value);
                else
                    return null;
            }
            public static Type GetTpyeFromNode(XmlNode node, Type Default)
            {
                var type = GetAttrib("Type", node) + "";
                if (type != "")
                {
                    try
                    {
                        var T = Assembly.GetEntryAssembly().GetType(type);
                        if (T != null) return T;
                    }
                    catch { }
                    try
                    {
                        var T = Default.Assembly.GetType(type);
                        if (T != null) return T;
                    }
                    catch { }
                    try
                    {
                        var T = Assembly.GetExecutingAssembly().GetType(type);
                        if (T != null) return T;
                    }
                    catch { }
                    try
                    {
                        var T = Assembly.GetCallingAssembly().GetType(type);
                        if (T != null) return T;
                    }
                    catch { }
                }
                return Default;
            }

            #endregion
            #region Definitions
            public delegate XmlNode SaveFunc(string name, object v, XmlNode root);
            public delegate object LoadFunc(Type type, string name, XmlNode root, object v);
            public delegate string SaveFuncSimple(object value);
            public delegate object LoadFuncSimple(string str);
            #endregion
            #region OPtions
            /// <summary>
            /// when an exception occurs 
            /// </summary>
#if DEBUG
            public static ExceptionBehavior exceptionBehavior = ExceptionBehavior.ThrowException;
#else
            public static ExceptionBehavior exceptionBehavior = ExceptionBehavior.MessageBox;
#endif
            /// <summary>
            /// Encryption names?
            /// </summary>
            public static bool Encryption = false;
            /// <summary>
            /// Save static fields?
            /// </summary>
            public static StaticFieldsBehavior saveStaticFields = StaticFieldsBehavior.DontSaveAtAll;
            /// <summary>
            /// save array in binary format?
            /// </summary>
            public static bool SerializeArrays = false;
            #region definition
            /// <summary>
            /// when an exception occurs 
            /// </summary>
            public enum ExceptionBehavior
            {
                Hide, MessageBox, ThrowException
            }
            /// <summary>
            /// Save static fields?
            /// </summary>
            public enum StaticFieldsBehavior
            {
                DontSaveAtAll, SaveIfRequested, SaveAll
            }
            #endregion
            #endregion
            /// <summary>
            /// 
            /// </summary>
            /// <returns>aborted?</returns>
            internal static bool Error(Exception ex, string comment = "")
            {
                if (exceptionBehavior == ExceptionBehavior.ThrowException) throw new Exception(ex.Message + "\r\n" + comment);
                else if (exceptionBehavior == ExceptionBehavior.MessageBox)
                {
                    var res = MessageBox.Show(ex.Message + "\r\n" + comment +
                        "\r\nYes: Continue\r\nNo: Continue, don't show errors any more\r\nCancel: Abort", "Error in saver", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (res == DialogResult.No)
                        exceptionBehavior = ExceptionBehavior.Hide;
                    else if (res == DialogResult.Cancel)
                        return abort = true;
                }
                return false;
            }
            /// <summary>
            /// will be set at Error()
            /// </summary>
            [ThreadStatic]
            internal static bool abort = false;

        }
        #endregion

        #region Defined Types
        [DontSave]
        static int reg = RegisterTypesIfNot();
        static int RegisterTypesIfNot()
        {
            if (Settings.Types.Count > 0) return 0;

            #region Object
            Settings.SaveAbleObjectType = new Settings.SaveAbleType()
            {
                type = typeof(object),
                Saver = ObjectSaver,
                Loader = ObjectLoader,
            };
            #endregion

            #region base types
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Enum),
                Saver = (n, v, r) => { try { return Settings.AddNode(n, ((int)v).ToString(), r); } catch { return Settings.AddNode(n, "0", r); } },
                Loader = (t, n, r, v) => Convert.ToInt32(Settings.GetNodeVal(n, r))
            });
            Settings.AddSaveAbleType(typeof(System.Drawing.Color),
                (t, n, r, v) =>
                {
                    try
                    {
                        var s = Settings.GetNodeVal(n, r);
                        return System.Drawing.Color.FromArgb(
                            byte.Parse(s.Substring(1, 2), System.Globalization.NumberStyles.HexNumber),
                            byte.Parse(s.Substring(3, 2), System.Globalization.NumberStyles.HexNumber),
                            byte.Parse(s.Substring(5, 2), System.Globalization.NumberStyles.HexNumber),
                            byte.Parse(s.Substring(7, 2), System.Globalization.NumberStyles.HexNumber));
                    }
                    catch
                    {
                        return ObjectLoader(t, n, r, v);
                    }
                },
                (n, v, r) =>
                {
                    var c = (System.Drawing.Color)v;
                    return Settings.AddNode(n, "#" + c.A.ToString("X2") + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"), r);
                },
                false);
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(UInt16),
                Loader = (t, n, r, v) => Convert.ToUInt16(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(uint),
                Loader = (t, n, r, v) => Convert.ToUInt32(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(UInt64),
                Loader = (t, n, r, v) => Convert.ToUInt64(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Int16),
                Loader = (t, n, r, v) => Convert.ToInt16(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Int32),
                Loader = (t, n, r, v) => Convert.ToInt32(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Int64),
                Loader = (t, n, r, v) => Convert.ToInt64(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(String),
                Loader = (t, n, r, v) => (Settings.GetNodeVal(n, r)),
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Double),
                Loader = (t, n, r, v) => Convert.ToDouble(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Single),
                Loader = (t, n, r, v) => Convert.ToSingle(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Boolean),
                Loader = (t, n, r, v) => Convert.ToBoolean(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Byte),
                Loader = (t, n, r, v) => Convert.ToByte(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(SByte),
                Loader = (t, n, r, v) => Convert.ToSByte(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Char),
                Loader = (t, n, r, v) => Convert.ToChar(Settings.GetNodeVal(n, r))
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Decimal),
                Loader = (t, n, r, v) => Convert.ToDecimal(Settings.GetNodeVal(n, r))
            });
            #endregion

            #region Array

            var ArraySaver = new Settings.SaveAbleType()
            {
                Saver = (n, v, r) =>
                {
                    if (Settings.SerializeArrays)
                    {
                        using (var str = new MemoryStream())
                        {
                            new BinaryFormatter().Serialize(str, v);
                            if (str.Length > 50)
                            {
                                var n_ = Settings.AddNode(n, Convert.ToBase64String(str.ToArray()), r);
                                Settings.SetAttrib("serialized", "1", n_);
                                return n_;
                            }
                        }
                    }
                    {
                        var val = new StringBuilder();
                        foreach (var a in (System.Collections.IEnumerable)v)
                            val.Append(a + ",");
                        var n_ = Settings.AddNode(n, val.ToString(), r);
                        Settings.SetAttrib("array", "1", n_);
                        return n_;
                    }
                    {
                        //return Settings.SaveAbleObjectType.Saver(n, v, r);
                    }
                },
                Loader = (t, n, r, v) =>
                {
                    {
                        var serialized = false;
                        if (Settings.LoadedVersion < 2.0) serialized = true;
                        var n_ = Settings.GetNode(n, r);
                        if (n_ != null && Settings.GetAttrib("serialized", n_) == "1") serialized = true;
                        if (serialized)
                        {
                            var s = Settings.GetNodeVal(n, r);
                            if (s == null) return null;
                            var bytes = Convert.FromBase64String(s);
                            using (var str = new MemoryStream(bytes))
                            {
                                return new BinaryFormatter().Deserialize(str);
                            }
                        }
                    }
                    {
                        var n_ = Settings.GetNode(n, r);
                        if (n_ != null && Settings.GetAttrib("array", n_) == "1")
                        {
                            var V = Settings.GetNodeVal(n, r).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (t.IsArray)
                            {
                                var t_ = t.GetElementType();
                                v = Array.CreateInstance(t_, V.Length);
                                for (int i = 0; i < V.Length; i++)
                                    try
                                    {
                                        if (Settings.abort) return null;
                                        ((Array)v).SetValue(ConvertBaseTypes(t_, V[i]), i);
                                    }
                                    catch (Exception ex)
                                    {
                                        Settings.Error(ex, "Error in set value for: " + n + "." + n + i);
                                    }
                            }
                            if (t.IsGenericType)
                            {
                                var t_ = t.GetGenericArguments()[0];
                                var T_ = typeof(List<>).MakeGenericType(t_);
                                v = Activator.CreateInstance(T_);
                                for (int i = 0; i < V.Length; i++)
                                    try
                                    {
                                        if (Settings.abort) return null;
                                        T_.InvokeMember("Add", BindingFlags.InvokeMethod, null, v, new object[] { ConvertBaseTypes(t_, V[i]) });
                                    }
                                    catch (Exception ex)
                                    {
                                        Settings.Error(ex, "Error in set value for: " + n + "." + n + i);
                                    }
                            }
                            return v;
                        }
                    }
                    {
                        return Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    }
                }
            };
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(double[]),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Byte[]),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Boolean[]),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(int[]),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(List<double>),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(List<int>),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(List<Byte>),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(List<Boolean>),
                Saver = ArraySaver.Saver,
                Loader = ArraySaver.Loader
            });

            #endregion

            #region Controls
            Settings.IgnorUIDs.Add(typeof(TextBox));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(TextBox),
                Loader = (t, n, r, v) =>
                {
                    v = Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    ((TextBox)v).Text = Settings.GetAttrib("text", node);
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    Settings.SaveAbleObjectType.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("text", node))
                        Settings.SetAttrib("text", ((TextBox)v).Text, node);
                    return node;
                }
            });
            Settings.IgnorUIDs.Add(typeof(ProgressBar));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(ProgressBar),
                Loader = (t, n, r, v) =>
                {
                    v = Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("value", node))
                        ((ProgressBar)v).Value = Convert.ToInt32(Settings.GetAttrib("value", node));
                    ((ProgressBar)v).Value++;
                    ((ProgressBar)v).Value--;
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    Settings.SaveAbleObjectType.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    Settings.SetAttrib("value", ((ProgressBar)v).Value + "", node);
                    return node;
                }
            });
            Settings.IgnorUIDs.Add(typeof(Form));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(Form),
                Loader = (t, n, r, v) =>
                {
                    v = Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("Width", node))
                        ((Form)v).Width = Convert.ToInt32(Settings.GetAttrib("Width", node));
                    if (Settings.HasAttrib("Height", node))
                        ((Form)v).Height = Convert.ToInt32(Settings.GetAttrib("Height", node));
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    Settings.SaveAbleObjectType.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    Settings.SetAttrib("Width", ((Form)v).Width + "", node);
                    Settings.SetAttrib("Height", ((Form)v).Height + "", node);
                    return node;
                }
            });
            Settings.IgnorUIDs.Add(typeof(TabControl));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(TabControl),
                Loader = (t, n, r, v) =>
                {
                    v = Settings.SaveAbleObjectType.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("index", node))
                        ((TabControl)v).SelectedIndex = Convert.ToInt32(Settings.GetAttrib("index", node));
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    Settings.SaveAbleObjectType.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    Settings.SetAttrib("index", ((TabControl)v).SelectedIndex + "", node);
                    return node;
                }
            });
            Settings.IgnorUIDs.Add(typeof(ComboBox));
            Settings.Types.Add(new Settings.SaveAbleType()
            {
                type = typeof(ComboBox),
                Loader = (t, n, r, v) =>
                {
                    //v = SaveAbleObject.Loader(t, n, r, v);
                    var node = Settings.GetNode(n, r);
                    if (Settings.HasAttrib("index", node))
                        ((ComboBox)v).SelectedIndex = Convert.ToInt32(Settings.GetAttrib("index", node));
                    return v;
                },
                Saver = (n, v, r) =>
                {
                    //SaveAbleObject.Saver(n, v, r);
                    var node = Settings.GetNode(n, r);
                    Settings.SetAttrib("index", ((ComboBox)v).SelectedIndex + "", node);
                    return node;
                }
            });
            #endregion

            Settings.IgnorTypes.Add(typeof(Pointer));
            Settings.IgnorTypes.Add(typeof(Button));
            Settings.IgnorTypes.Add(typeof(TabControl.TabPageCollection));
            Settings.IgnorUIDs.Add(typeof(System.Drawing.Point));
            Settings.IgnorUIDs.Add(typeof(System.Drawing.Size));
            Settings.IgnorUIDs.Add(typeof(System.Diagnostics.Process));
            return 1;
        }
        static object ConvertBaseTypes(Type t, string val)
        {
            if (t == typeof(Int16)) return Convert.ToInt16(val);
            if (t == typeof(Int32)) return Convert.ToInt32(val);
            if (t == typeof(Int64)) return Convert.ToInt64(val);
            if (t == typeof(UInt16)) return Convert.ToUInt16(val);
            if (t == typeof(uint)) return Convert.ToUInt32(val);
            if (t == typeof(UInt64)) return Convert.ToUInt64(val);
            if (t == typeof(Double)) return Convert.ToDouble(val);
            if (t == typeof(Single)) return Convert.ToSingle(val);
            if (t == typeof(Decimal)) return Convert.ToDecimal(val);
            if (t == typeof(Char)) return Convert.ToChar(val);
            if (t == typeof(Boolean)) return Convert.ToBoolean(val);
            if (t == typeof(Byte)) return Convert.ToByte(val);
            if (t == typeof(SByte)) return Convert.ToSByte(val);
            if (t == typeof(String)) return val;
            return null;
        }
        static XmlNode get_node_ref(XmlNode node, string name, string id)
        {
            if (Settings.abort) return null;
            foreach (XmlNode n in node.ChildNodes)
            {
                if (n.Name == name && Settings.GetAttrib("id", n) + "" == id && Settings.GetAttrib("ref", n) == null)
                    return n;
                var n_ = get_node_ref(n, name, id);
                if (n_ != null) return n_;
            }
            return null;
        }
        static object ObjectLoader(Type t, string n, XmlNode r, object val)
        {
#if DEBUG
            File.AppendAllText(Settings.debug_file, "Load: " + n + " - " + t + " - " + r.Name + " - " + val + "\r\n");
#endif
            if (Settings.abort) return null;
            n = ValidName(n);
            foreach (XmlNode node_ in r.ChildNodes)
                if (node_.Name == n)
                {
                    var node = node_;
                    if (Settings.abort) return null;
                    if (Settings.GetAttrib("null", node) == "1") return null;
                    var id = Settings.GetAttrib("id", node) + "";
                    if (id != "")
                        if (!Settings.IgnorUIDs.Contains(t))
                            if (UIDs.ContainsKey(id))
                                return UIDs[id];
                    if (Settings.GetAttrib("ref", node) != null) // is ref but origin value is not loaded yet
                    {
                        t = Settings.GetTpyeFromNode(node, t);
                        node = get_node_ref(xml.ChildNodes[0], Settings.GetAttrib("ref", node), id + "");
                        if (Settings.GetAttrib("null", node) == "1") return null;
                    }
                    object v = val;
                    // برای دیکشنری و آرایه دو بعدی باید مطابق کلون اصلاح شود ?????????????
                    if (t.IsArray)
                    {
                        var t_ = t.GetElementType();
                        v = Array.CreateInstance(t_, node.ChildNodes.Count);
                        if (!Settings.IgnorUIDs.Contains(t))
                        {
                            if (!UIDs.ContainsKey(id)) UIDs.Add(id, v);
                            else UIDs[id] = v;
                        }
                        for (int i = 0; i < node.ChildNodes.Count; i++)
                            try
                            {
                                if (Settings.abort) return null;
                                var fT = Settings.GetTpyeFromNode(node.ChildNodes[i], t_);
                                var st = Settings.SaveAbleType.Get(fT);
                                ((Array)v).SetValue(st.Loader(fT, Tools.Decrypt(node.ChildNodes[i].Name), node, null), i);
                            }
                            catch (Exception ex)
                            {
                                Settings.Error(ex, "Error in set value for: " + n + "[" + i + "]");
                            }
                        return v;
                    }
                    if (t.IsGenericType && t.GetGenericArguments().Length == 1)
                    {
                        {
                            var t_ = t.GetGenericArguments()[0];
                            var T_ = typeof(List<>).MakeGenericType(t_);
                            if (t == T_)
                            {
                                v = Activator.CreateInstance(T_);
                                if (!Settings.IgnorUIDs.Contains(t))
                                {
                                    if (!UIDs.ContainsKey(id)) UIDs.Add(id, v);
                                    else UIDs[id] = v;
                                }
                                for (int i = 0; i < node.ChildNodes.Count; i++)
                                {
                                    object item = null;
                                    try
                                    {
                                        if (Settings.abort) return null;
                                        var fT = Settings.GetTpyeFromNode(node.ChildNodes[i], t_);
                                        var st = Settings.SaveAbleType.Get(fT);
                                        if (st != null)
                                            item = st.Loader(fT, Tools.Decrypt(node.ChildNodes[i].Name), node, null);
                                        var met = T_.GetMethod("Add");
                                        met.Invoke(v, new object[] { item });
                                    }
                                    catch (Exception ex)
                                    {
                                        Settings.Error(ex, "Error in set value for: " + n + "[" + i + "] id:" + GetUID(item));
                                        T_.InvokeMember("Add", BindingFlags.InvokeMethod, null, v, new object[] { null });
                                    }
                                }
                                return v;
                            }
                        }
                    }

                    {
                        if (!t.IsSubclassOf(typeof(Control)) && v == null)
                            try
                            {
                                v = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
                            }
                            catch
                            {
                                try
                                {
                                    v = Activator.CreateInstance(t);
                                }
                                catch (Exception ex)
                                {
                                    Settings.Error(ex, n + ", " + (t != null ? t.FullName : "!!" + t.FullName) + ", " + v);
                                }
                            }
                        if (!Settings.IgnorUIDs.Contains(t))
                        {
                            if (!UIDs.ContainsKey(id)) UIDs.Add(id, v);
                            else UIDs[id] = v;
                        }
                        var F = Settings.GetFields(v.GetType());
                        foreach (var f in F)
                            if (Settings.HasNode(GetFieldName(f), node))
                                try
                                {
                                    if (Settings.abort) return null;
                                    var n_ = Settings.GetNode(GetFieldName(f), node);
                                    var fT = Settings.GetTpyeFromNode(n_, f.FieldType);
                                    var st = Settings.SaveAbleType.Get(fT);
                                    if (st != null)
                                        f.SetValue(v, st.Loader(fT, GetFieldName(f), node, f.GetValue(v)));
                                }
                                catch (Exception ex)
                                {
                                    Settings.Error(ex, "Error in set value for: " + n + "." + GetFieldName(f));
                                }
                    }
                    return v;
                }
            return null;
        }
        static object ObjectCloner(object v, object clone_to = null)
        {
#if DEBUG
            File.AppendAllText(Settings.debug_file, "Clone: " + v + "\r\n");
#endif
            if (v == null)
                return null;
            var t = v.GetType();
            if (t.IsValueType) return v;
            if (v is string) return v;
            if (Settings.IgnorTypes.Contains(t) || !DontSave.Save(v) || !SaveCondition.Save(v))
                return null;
            var id = SaveAble.GetUID(v);
            {
                if (UIDs.ContainsKey(id))
                {
                    var o = UIDs[id];
                    return o;
                }
            }
            if (t.IsArray)
            {
                var t_ = t.GetElementType();
                if (t.GetArrayRank() == 1)
                {
                    var v2 = clone_to ?? Array.CreateInstance(t_, ((Array)v).Length);

                    if (!Settings.IgnorUIDs.Contains(t))
                    {
                        if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                        else UIDs[id] = v2;
                    }
                    for (int i = 0; i < ((Array)v).Length; i++)
                        try
                        {
                            if (Settings.abort) return null;
                            ((Array)v2).SetValue(ObjectCloner(((Array)v).GetValue(i)), i);
                        }
                        catch (Exception ex)
                        {
                            Settings.Error(ex, "Error in set value for: " + v + "." + i);
                        }
                    return v2;
                }
                if (t.GetArrayRank() == 2)
                {
                    var L1 = ((Array)v).GetLength(0);
                    var L2 = ((Array)v).GetLength(1);
                    var v2 = clone_to ?? Array.CreateInstance(t_, L1, L2);

                    if (!Settings.IgnorUIDs.Contains(t))
                    {
                        if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                        else UIDs[id] = v2;
                    }
                    for (int i = 0; i < L1; i++)
                        for (int j = 0; j < L2; j++)
                            try
                            {
                                if (Settings.abort) return null;
                                ((Array)v2).SetValue(ObjectCloner(((Array)v).GetValue(i, j)), i, j);
                            }
                            catch (Exception ex)
                            {
                                Settings.Error(ex, "Error in set value for: " + v + "." + i + "," + j);
                            }
                    return v2;
                }
                if (t.GetArrayRank() == 3)
                {
                    var L1 = ((Array)v).GetLength(0);
                    var L2 = ((Array)v).GetLength(1);
                    var L3 = ((Array)v).GetLength(2);
                    var v2 = clone_to ?? Array.CreateInstance(t_, L1, L2, L3);

                    if (!Settings.IgnorUIDs.Contains(t))
                    {
                        if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                        else UIDs[id] = v2;
                    }
                    for (int i = 0; i < L1; i++)
                        for (int j = 0; j < L2; j++)
                            for (int k = 0; k < L3; k++)
                                try
                                {
                                    if (Settings.abort) return null;
                                    ((Array)v2).SetValue(ObjectCloner(((Array)v).GetValue(i, j, k)), i, j, k);
                                }
                                catch (Exception ex)
                                {
                                    Settings.Error(ex, "Error in set value for: " + v + "." + i + "," + j + "," + k);
                                }
                    return v2;
                }
                Settings.Error(new Exception("Can't clone type: " + t));
            }
            else if (t.IsGenericType)
            {
                if (t.GetGenericArguments().Length == 1)
                {
                    var t_ = t.GetGenericArguments()[0];
                    var T_ = typeof(List<>).MakeGenericType(t_);
                    if (t == T_)
                    {
                        var v2 = clone_to ?? Activator.CreateInstance(t);
                        if (!Settings.IgnorUIDs.Contains(t))
                        {
                            if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                            else UIDs[id] = v2;
                        }
                        foreach (var a in (System.Collections.IEnumerable)v)
                        {
                            object item = null;
                            try
                            {
                                if (Settings.abort) return null;
                                item = ObjectCloner(a);
                                var met = T_.GetMethod("Add");
                                met.Invoke(v2, new object[] { item });
                            }
                            catch (Exception ex)
                            {
                                Settings.Error(ex, "Error in set value for: " + v + ".");
                                T_.InvokeMember("Add", BindingFlags.InvokeMethod, null, v2, new object[] { null });
                            }
                        }
                        return v2;
                    }
                }
                else if (t.GetGenericArguments().Length == 2)
                {
                    var t1_ = t.GetGenericArguments()[0];
                    var t2_ = t.GetGenericArguments()[1];
                    var T_ = typeof(Dictionary<,>).MakeGenericType(t1_, t2_);
                    if (t == T_)
                    {
                        var v2 = clone_to ?? Activator.CreateInstance(t);
                        if (!Settings.IgnorUIDs.Contains(t))
                        {
                            if (!UIDs.ContainsKey(id)) UIDs.Add(id, v2);
                            else UIDs[id] = v2;
                        }
                        var key = t.GetProperty("Keys").GetValue(v, null);
                        var val = t.GetProperty("Values").GetValue(v, null);
                        var tmp_vals_array = new List<object>();
                        foreach (var v_ in (System.Collections.IEnumerable)val)
                            tmp_vals_array.Add(v_);
                        var tmp_keys_array = new List<object>();
                        foreach (var k_ in (System.Collections.IEnumerable)key)
                            tmp_keys_array.Add(k_);
                        for (int i = 0; i < tmp_keys_array.Count; i++)
                        {
                            try
                            {
                                if (Settings.abort) return null;
                                var val_ = ObjectCloner(tmp_vals_array[i]);
                                var key_ = ObjectCloner(tmp_keys_array[i]);
                                var met = T_.GetMethod("Add");
                                met.Invoke(v2, new object[] { key_, val_ });
                            }
                            catch (Exception ex)
                            {
                                Settings.Error(ex, "Error in set value for: " + v + ".");
                                T_.InvokeMember("Add", BindingFlags.InvokeMethod, null, v2, new object[] { null, null });
                            }
                        }
                        return v2;
                    }
                }
                Settings.Error(new Exception("Can't clone type: " + t));
            }

            {
                object v2 = null;
                try
                {
                    v2 = clone_to ?? System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
                }
                catch
                {
                    try
                    {
                        v2 = clone_to ?? Activator.CreateInstance(t);
                    }
                    catch (Exception ex)
                    {
                        Settings.Error(ex, "Clone: (new) " + (t != null ? t.FullName : "!!") + ", " + v);
                    }
                }
                try
                {
                    UIDs.Add(id, v2);
                    CloneFields(v, v2);
                }
                catch (Exception ex)
                {
                    Settings.Error(ex, "Clone: " + (t != null ? t.FullName : "!!" + v));

                }
                return v2;
            }
        }
        static XmlNode ObjectSaver(string n, object v, XmlNode r)
        {
#if DEBUG
            File.AppendAllText(Settings.debug_file, "Save: " + n + " - " + (v != null ? v.GetType() + "" : "null") + " - " + r.Name + " - " + v + "\r\n");
#endif
            if (Settings.abort) return null;
            n = ValidName(n);
            var node = r.AppendChild(xml.CreateElement(n));
            if (v == null)
            {
                Settings.SetAttrib("null", "1", node);
                return null;
            }
            if (Settings.IgnorTypes.Contains(v.GetType()) || !DontSave.Save(v) || !SaveCondition.Save(v))
                return null;
            {
                var id = SaveAble.GetUID(v);
                Settings.SetAttrib("id", id + "", node);
                if (UIDs.ContainsKey(id))
                {
                    var o = UIDs[id];
                    Settings.SetAttrib("ref", UIDs_node_name[id], node);
                    //if (v.GetType() != o.GetType())
                    Settings.SetAttrib("Type", o.GetType() + "", node);
                    return null;
                }
                UIDs.Add(id, v);
                UIDs_node_name.Add(id, n);
            }
            // برای دیکشنری و آرایه دو بعدی باید مطابق کلون اصلاح شود ?????????????
            var t = v.GetType();
            if (v is System.Collections.IEnumerable && !(v is string) && (t.IsArray || (t.IsGenericType && t.GetGenericArguments().Length == 1)))
            {
                if (t.IsArray && t.GetArrayRank() != 1)
                    Settings.Error(new Exception("Can't save type: " + t));
                if (t.IsGenericType && t.GetGenericArguments().Length != 1)
                    Settings.Error(new Exception("Can't save type: " + t));
                int i = 0;
                var vt_ = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
                try
                {
                    var object_node = new Dictionary<object, XmlNode>();
                    foreach (var a in (System.Collections.IEnumerable)v)
                    {
                        if (Settings.abort) break;
                        if (a == null)
                            Settings.AddNode("_" + (i++), "1", node, "null");
                        else
                        {
                            var fT = a != null ? a.GetType() : vt_;
                            var st = Settings.SaveAbleType.Get(fT);
                            if (st == Settings.SaveAbleObjectType && a is SaveAble)
                                ignor_save_fields = true;
                            var n_ = st.Saver("_" + (i++), a, node);
                            if (n_ != null && ignor_save_fields)
                                object_node[a] = n_;
                            ignor_save_fields = false;
                            if (n_ != null && vt_ != fT)
                                Settings.SetAttrib("Type", fT + "", n_);
                        }
                    }
                    foreach (var a in object_node)
                    {
                        if (Settings.abort) break;
                        ignor_save_fields = false;
                        SaveFields(a.Key, a.Value);
                    }
                }
                finally { ignor_save_fields = false; }
            }
            else
            {
                SaveFields(v, node);
            }
            if (Settings.abort) return null;
            return node;
        }
        [DontSave, ThreadStatic]
        static bool ignor_save_fields = false;
        static void SaveFields(object v, XmlNode node)
        {
            if (ignor_save_fields) return;
            var F = Settings.GetFields(v.GetType());
            foreach (var f in F)
            {
                if (Settings.abort) break;
                if (f.IsStatic && Settings.saveStaticFields != Settings.StaticFieldsBehavior.SaveAll)
                {
                    if (Settings.saveStaticFields == Settings.StaticFieldsBehavior.DontSaveAtAll)
                        continue;
                    if (!SaveStaticFields.Save(f) && !SaveStaticFields.Save(v))
                        continue;
                }
                var fval = f.GetValue(v);
                var fT = fval != null ? fval.GetType() : f.FieldType;
                if (Settings.IgnorTypes.Contains(fT) || !DontSave.Save(f) || !SaveCondition.Save(f))
                    continue;
                if (fval == null)
                    Settings.AddNode(GetFieldName(f), "1", node, "null");
                else
                {
                    var st = Settings.SaveAbleType.Get(fT);
                    if (st != null)
                    {
                        var n_ = st.Saver(GetFieldName(f), fval, node);
                        // if (!(fval.GetType() + "").Contains("["))
                        if (n_ != null && f.FieldType != fT)
                            Settings.SetAttrib("Type", fT + "", n_);
                    }
                }
            }
        }
        static void CloneFields(object v, object v2)
        {
            if (ignor_save_fields) return;
            var F = Settings.GetFields(v.GetType());
            foreach (var f in F)
                try
                {
                    if (Settings.abort) break;
                    if (f.IsStatic)
                        continue;
                    var fval = f.GetValue(v);
                    if (!DontSave.Save(f) || !SaveCondition.Save(f))
                        continue;
                    f.SetValue(v2, ObjectCloner(fval));
                }
                catch (Exception ex)
                {
                    Settings.Error(ex, "Error in field: " + v.GetType().FullName + " > " + f.Name);
                }
        }
        #endregion

        #region unique id
        /*[DontSave, ThreadStatic]
        static uint counter = 0;
        [DontSave]
        string _uid_ = Get_next_uid_();
        static string Get_next_uid_()
        {
            counter++;
            //if (counter++ > 10000)
            //    counter = 1;
            return System.Threading.Thread.CurrentThread.ManagedThreadId + "." + counter;
        }*/
        /// <summary>
        /// لیستی از آبجکت های ذخیره یا لود شده بر اساس آی دی
        /// </summary>
        [DontSave, ThreadStatic]
        static Dictionary<string, object> UIDs;
        /// <summary>
        /// for save
        /// </summary>
        [DontSave, ThreadStatic]
        static Dictionary<string, string> UIDs_node_name;
        static System.Runtime.Serialization.ObjectIDGenerator id_gen = new System.Runtime.Serialization.ObjectIDGenerator();
        public static string GetUID(object obj)
        {
            try
            {
                var first = false;
                return id_gen.GetId(obj, out first) + "";
                /*
                if (obj == null) return "";
                if (obj is SaveAble)
                {
                    if (((SaveAble)obj)._uid_ + "" == "")
                        ((SaveAble)obj)._uid_ = Get_next_uid_();
                    return ((SaveAble)obj)._uid_;
                }
                else
                {
                    var f = obj.GetType().GetField("_uid_");
                    if (f != null)
                    {
                        var uid = f.GetValue(obj) + "";
                        if (uid == "")
                            f.SetValue(obj, uid = Get_next_uid_());
                        return uid;
                    }
                }
                var t = obj.GetType();
                var ext = t.Namespace + "." + t.Name + "=";
                if (t.IsArray)
                {
                    var t_ = t.GetElementType();
                    ext = "1-" + t_?.Namespace + "." + t_?.Name;
                }
                else if (t.IsGenericType)
                {
                    ext = 2 + "";
                    for (int i = 0; i < t.GetGenericArguments().Length; i++)
                    {
                        var t_ = t.GetGenericArguments()[i];
                        ext = "-" + t_?.Namespace + "." + t_?.Name;
                    }
                }
#if DEBUG
                return id_gen.GetId(obj, out first)  + "-" + (ext);
#else
                return id_gen.GetId(obj, out first) + "-" + Tools.Hash(ext);
#endif */
            }
            catch
            {
                return obj.GetHashCode() + "!";
            }
        }
        #endregion

        #region Not Public
        /// <summary>
        /// before save done, (internally)
        /// </summary>
        /// <returns></returns>
        protected static void BeginSave()
        {
            Settings.abort = false;
            UIDs = new Dictionary<string, object>();
            UIDs_node_name = new Dictionary<string, string>();
            xml = new XmlDocument();
            root = xml.AppendChild(xml.CreateElement("Root"));
            Settings.SetAttrib("ver", Settings.Version + "", root);
            var d = DateTime.Now;
            Settings.SetAttrib("time", (d.Month + "/" + d.Day + "/" + d.Year + "-" + d.ToLongTimeString() + "     "), root);
        }
        [DontSave, ThreadStatic]
        static XmlDocument xml;
        [DontSave, ThreadStatic]
        static XmlNode root;
        /// <summary>
        /// after save done, (internally)
        /// </summary>
        /// <returns></returns>
        protected static string EndSave()
        {
            if (!Settings.FormatedXML)
                return xml.OuterXml;
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                xml.Save(sw);
            }
            var str = sb + "";
            var p1 = str.IndexOf("\n");
            UIDs = null;
            UIDs_node_name = null;
            xml = null;
            root = null;
            GC.Collect();
            return str.Substring(p1 + 1);
        }
        /// <summary>
        /// before load done, (internally)
        /// </summary>
        /// <returns></returns>
        protected static void BeginLoad(string fileName)
        {
            Settings.abort = false;
            UIDs = new Dictionary<string, object>();
            xml = new XmlDocument();
            xml.Load(fileName);
            root = xml.ChildNodes[0];
            Settings.LoadedVersion = -1;
            double.TryParse(Settings.GetAttrib("ver", root), out Settings.LoadedVersion);
        }
        protected static void EndLoad()
        {
            xml = null;
            root = null;
            UIDs = null;
            UIDs_node_name = null;
            GC.Collect();
        }
        static string GetFieldName(FieldInfo f)
        {
            var name = SaveAs.Name(f);
            try
            {
                XmlConvert.VerifyName(name);
                return name;
            }
            catch
            {
                return Tools.Base64Encode(Tools.Base64Encode(name));
            }
        }

        static string ValidName(string name)
        {
            return Tools.Encrypt(name.Replace("<", "_").Replace(">", "_").Replace(" ", "_"));
        }

        static string GetFileHash(string file)
        {
            return GetStrHash(File.ReadAllText(file));
        }
        static string GetStrHash(string str)
        {
            using (var md5Hash = System.Security.Cryptography.MD5.Create())
            {
                var rg = new System.Text.RegularExpressions.Regex("id=\"[^\"]+\"", System.Text.RegularExpressions.RegexOptions.Compiled);
                str = rg.Replace(str.Substring(50), "$");
                //rg = new System.Text.RegularExpressions.Regex("time=\"[^\"]+\"");
                //str = rg.Replace(str, "time");
                var data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(str));
                var sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                    sBuilder.Append(data[i].ToString("x2"));
                return sBuilder.ToString();
            }
        }
        /// <summary>
        /// will not update current hash
        /// </summary>
        /// <returns>Hash Code</returns>
        string Save4Hash()
        {
            BeginSave();
            Settings.SaveAbleType.Get(this.GetType()).Saver("Data", this, root);
            return GetStrHash(EndSave());
        }
        [DontSave, ThreadStatic]
        string FileHash = "";
        #endregion

        #region Save/Load methods
        public virtual object Clone(params object[] IgnorObjects)
        {
            return Clone(this, IgnorObjects);

        }
        /// <summary>
        /// clone an abject to this
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="IgnorObjects"></param>
        public virtual void LoadFromObject(object obj, params object[] IgnorObjects)
        {
            var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
            try
            {
                if (IgnorObjects != null)
                    Settings.IgnorSaveObjects.AddRange(IgnorObjects);
                Settings.abort = false;
                UIDs = new Dictionary<string, object>();
                UIDs_node_name = new Dictionary<string, string>();

                ObjectCloner(obj, this);
            }
            finally
            {
                UIDs = null;
                UIDs_node_name = null;
                while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                    Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
            }
        }
        public static T Clone<T>(T obj, params object[] IgnorObjects)
        {
            return (T)Clone_(obj, IgnorObjects);
        }
        static object Clone_(object obj, params object[] IgnorObjects)
        {
            var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
            try
            {
                if (IgnorObjects != null)
                    Settings.IgnorSaveObjects.AddRange(IgnorObjects);
                Settings.abort = false;
                UIDs = new Dictionary<string, object>();
                UIDs_node_name = new Dictionary<string, string>();

                return ObjectCloner(obj);
            }
            finally
            {
                UIDs = null;
                UIDs_node_name = null;
                while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                    Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="IgnorObjects">List of objects that will not be saved</param>
        /// <returns>Hash Code</returns>
        public virtual string Save(string fileName, params object[] IgnorObjects)
        {
            var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
            try
            {
                if (IgnorObjects != null)
                    Settings.IgnorSaveObjects.AddRange(IgnorObjects);
                BeginSave();
                this.FileHash = "";
                Settings.SaveAbleType.Get(this.GetType()).Saver("Data", this, root);
                var str = EndSave();
                File.WriteAllText(fileName, str, encoding: Encoding.Unicode);
                FileHash = GetStrHash(str);
                return FileHash;
            }
            finally
            {
                while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                    Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
            }
        }
        /// <summary>
        /// Load from file
        /// </summary>
        /// <param name="fileName"></param>
        public virtual void Load(string fileName)
        {
            BeginLoad(fileName);
            Settings.SaveAbleType.Get(this.GetType()).Loader(this.GetType(), "Data", root, this);
            FileHash = GetFileHash(fileName);
            EndLoad();
        }
        /// <summary>
        /// Save an object to file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="obj">objact that will be saved in file</param>
        /// <returns>Hash Code</returns>
        public static string Save(string fileName, object obj)
        {
            BeginSave();
            if (obj is SaveAble)
                ((SaveAble)obj).FileHash = "";
            Settings.SaveAbleType.Get(obj.GetType()).Saver("Data", obj, root);
            var str = EndSave();
            File.WriteAllText(fileName, str, Encoding.Unicode);
            var LastFileHash = GetStrHash(str);
            if (obj is SaveAble)
                ((SaveAble)obj).FileHash = LastFileHash;
            return LastFileHash;
        }
        /// <summary>
        /// load from file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T Load<T>(string fileName)
        {
            BeginLoad(fileName);
            var res = Settings.SaveAbleType.Get(typeof(T)).Loader(typeof(T), "Data", root, null);
            if (res is SaveAble)
                ((SaveAble)res).FileHash = GetFileHash(fileName);
            return (T)res;
        }
        /// <summary>
        /// Load from file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static object Load(string fileName, object obj)
        {
            BeginLoad(fileName);
            var res = Settings.SaveAbleType.Get(obj.GetType()).Loader(obj.GetType(), "Data", root, obj);
            if (res is SaveAble)
                ((SaveAble)res).FileHash = GetFileHash(fileName);
            return res;
        }
        /// <summary>
        /// Load from file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object Load_(string fileName, Type type)
        {
            BeginLoad(fileName);
            object v;
            try
            {
                v = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            }
            catch
            {
                v = Activator.CreateInstance(type);
            }
            var res = Settings.SaveAbleType.Get(type).Loader(type, "Data", root, v);
            if (res is SaveAble)
                ((SaveAble)res).FileHash = GetFileHash(fileName);
            return res;
        }

        /// <summary>
        /// Save object as a string instead a file
        /// </summary>
        /// <param name="IgnorObjects"></param>
        /// <returns></returns>
        public virtual string SaveString(params object[] IgnorObjects)
        {
            var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
            try
            {
                if (IgnorObjects != null)
                    Settings.IgnorSaveObjects.AddRange(IgnorObjects);
                BeginSave();
                Settings.SaveAbleType.Get(this.GetType()).Saver("Data", this, root);
                return EndSave();
            }
            finally
            {
                while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                    Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
            }
        }
        /// <summary>
        /// Save object as a string instead a file
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string SaveString(object obj)
        {
            var IgnorSaveObjects_count = Settings.IgnorSaveObjects.Count;
            try
            {
                BeginSave();
                Settings.SaveAbleType.Get(obj.GetType()).Saver("Data", obj, root);
                return EndSave();
            }
            finally
            {
                while (Settings.IgnorSaveObjects.Count > IgnorSaveObjects_count)
                    Settings.IgnorSaveObjects.RemoveAt(IgnorSaveObjects_count);
            }
        }
        /// <summary>
        /// Load object from a string instead an file
        /// </summary>
        /// <param name="str"></param>
        public virtual void LoadString(string str)
        {
            var f = Path.GetTempFileName() + "-LS";
            try
            {
                File.WriteAllText(f, str);
                Load(f);
            }
            finally
            {
                try
                {
                    File.Delete(f);
                }
                catch
                {
#if DEBUG
                    throw;
#endif
                }
            }
        }
        /// <summary>
        /// Load object from a string instead an file
        /// </summary>
        /// <param name="str"></param>
        public static object LoadString(string str, object obj)
        {
            var f = Path.GetTempFileName() + "-LS";
            try
            {
                File.WriteAllText(f, str);
                return Load(f, obj);
            }
            finally
            {
                try
                {
                    File.Delete(f);
                }
                catch
                {
#if DEBUG
                    throw;
#endif
                }
            }
        }
        public static object LoadString_(string str, Type type)
        {
            var f = Path.GetTempFileName() + "-LS";
            try
            {
                File.WriteAllText(f, str);
                return Load_(f, type);
            }
            finally
            {
                try
                {
                    File.Delete(f);
                }
                catch
                {
#if DEBUG
                    throw;
#endif
                }
            }
        }
        /// <summary>
        /// is saved in a file
        /// </summary>
        [Browsable(false)]
        public virtual bool IsSaved
        {
            get { return !IsChanged; }
            set { IsChanged = !value; }
        }
        /// <summary>
        /// is changed after last save in a file or load from a file
        /// </summary>
        [Browsable(false)]
        public virtual bool IsChanged
        {
            get
            {
                return Save4Hash() != FileHash;
            }
            set
            {
                if (value)
                    FileHash = "!";
                else
                    FileHash = Save4Hash();
            }
        }
        #endregion
    }

    #region Attributes
    /// <summary>
    /// this field will not be saved
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Class)]
    public class DontSave : Attribute
    {
        /// <summary>
        /// DontSave Attribute will not work at all
        /// </summary>
        public static bool DisableGlobaly = false;
        /// <summary>
        /// save or not?
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static bool Save(FieldInfo f)
        {
            if (f.Name == "_uid_") return false;
            if (DisableGlobaly) return true;
            var AT = f.FieldType.GetCustomAttributes(typeof(DontSave), true);
            return
              (AT == null || AT.Length == 0) &&
              f.GetCustomAttributes(typeof(DontSave), true).Length == 0;
        }
        /// <summary>
        /// save or not?
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool Save(object v)
        {
            if (DisableGlobaly) return true;
            if (v != null)
            {
                var AT = v.GetType().GetCustomAttributes(typeof(DontSave), true);
                return (AT == null || AT.Length == 0);
            }
            return true;
        }

    }
    /// <summary>
    /// will be saved if SaveIf.AllowSave is true
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Class)]
    public class SaveCondition : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="save_if_this_tag_exist"></param>
        /// <param name="dont_save_if_this_tag_exist"></param>
        public SaveCondition(object save_if_this_tag_exist = null, object dont_save_if_this_tag_exist = null)
        {
            this.save_if_this_tag_exist = save_if_this_tag_exist;
            this.dont_save_if_this_tag_exist = dont_save_if_this_tag_exist;
        }
        public static bool SaveAll = false;
        object save_if_this_tag_exist, dont_save_if_this_tag_exist;
        public static List<object> Tags = new List<object>();
        /// <summary>
        /// save or not
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static bool Save(FieldInfo f)
        {
            if (SaveAll) return true;
            var AT = f.GetCustomAttributes(typeof(SaveCondition), true);
            if (AT == null || AT.Length == 0)
                return true;
            var at = AT[0] as SaveCondition;
            return at.Save();
        }
        /// <summary>
        /// save or not
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool Save(object v)
        {
            if (SaveAll) return true;
            if (v == null) return true;
            var AT = v.GetType().GetCustomAttributes(typeof(SaveCondition), true);
            if (AT == null || AT.Length == 0)
                return true;
            var at = AT[0] as SaveCondition;
            return at.Save();
        }
        bool Save()
        {
            if (dont_save_if_this_tag_exist != null && Tags.Contains(dont_save_if_this_tag_exist)) return false;
            if (save_if_this_tag_exist != null && !Tags.Contains(save_if_this_tag_exist)) return false;
            return true;
        }
    }
    /// <summary>
    /// define a name for the field
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.Property)]
    public class SaveAs : Attribute
    {
        /// <summary>
        /// SaveAs Attribute will not work at all
        /// </summary>
        public static bool DisableGlobaly = false;
        /// <summary>
        /// name for save
        /// </summary>
        public string name;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">given name for save and load</param>
        public SaveAs(string name)
        {
            this.name = name;
        }
        /// <summary>
        /// given name for save and load
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static string Name(FieldInfo f)
        {
            var fName = f.Name;
            if (fName.Contains(">k__BackingField"))
            {
                var name = fName.Replace(">k__BackingField", "").Substring(1);
                if (DisableGlobaly) return name;
                var p = f.DeclaringType.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (p != null)
                {
                    var A = p.GetCustomAttributes(typeof(SaveAs), true);
                    if (A.Length > 0)
                        return (A[0] as SaveAs).name;
                }
                return name;
            }
            else
            {
                if (DisableGlobaly) return fName;
                var A = f.GetCustomAttributes(typeof(SaveAs), true);
                if (A.Length > 0)
                    return (A[0] as SaveAs).name;
            }
            return fName;
        }

    }
    /// <summary>
    /// Static Fields will be saved for this class | field
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
    public class SaveStaticFields : Attribute
    {
        /// <summary>
        /// SaveStaticFields Attribute will not work at all
        /// </summary>
        public static bool DisableGlobaly = false;
        /// <summary>
        /// save or not?
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static bool Save(FieldInfo f)
        {
            if (DisableGlobaly) return false;
            var AT = f.FieldType.GetCustomAttributes(typeof(SaveStaticFields), true);
            return (AT != null && AT.Length > 0);
        }
        /// <summary>
        /// save or not?
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool Save(object v)
        {
            if (DisableGlobaly) return false;
            if (v != null)
            {
                var AT = v.GetType().GetCustomAttributes(typeof(SaveStaticFields), true);
                return (AT != null && AT.Length > 0);
            }
            return false;
        }

    }
    #endregion

    #region Tools
    /// <summary>
    /// Settings
    /// </summary>
    public class Settings : SaveAble.Settings { }
    internal class Tools
    {
        public static string Hash(string str)
        {
            return _Decrypt(str);
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes).Replace("=", "");
        }
        static string XXXX = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz",
                     YYYY = "w9abc2FGHRSTUVWXYixyz1mnoBCDEldefghvpqrstuZ0jk3456AIJKLMNOPQ78",
                     XXXX2 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz#$%^&*()_-+=!.~`;:<>{}",
                     YYYY2 = "w9^ghab)z1f>ST}FGH:<(n.6AI&*JKYixy%vpR#LMNOP-+UVWm{c2~oBCDElde$qrst_`;uZ0jk345=!XQ78";
        public static string Encrypt(string str)
        {
            if (!Settings.Encryption)
                return str;
            return _Encrypt(str);
        }
        static string _Encrypt(string str)
        {
            var res = new StringBuilder();
            foreach (var c in str)
            {
                var i = XXXX.IndexOf(c);
                if (i >= 0)
                    res.Append(YYYY[i]);
                else
                    res.Append(c);
            }
            return "_" + res;
        }
        static string _Encrypt2(string str)
        {
            var res = new StringBuilder();
            foreach (var c in str)
            {
                var i = XXXX2.IndexOf(c);
                if (i >= 0)
                    res.Append(YYYY2[i]);
                else
                    res.Append(c);
            }
            return res.ToString();
        }
        public static string Decrypt(string str)
        {
            if (!Settings.Encryption)
                return str;
            return _Decrypt(str);
        }
        static string _Decrypt(string str)
        {
            str = str.Substring(1);
            var res = new StringBuilder();
            foreach (var c in str)
            {
                var i = YYYY.IndexOf(c);
                if (i >= 0)
                    res.Append(XXXX[i]);
                else
                    res.Append(c);
            }
            return res.ToString();
        }
        static string _Decrypt2(string str)
        {
            var res = new StringBuilder();
            foreach (var c in str)
            {
                var i = YYYY2.IndexOf(c);
                if (i >= 0)
                    res.Append(XXXX2[i]);
                else
                    res.Append(c);
            }
            return res.ToString();
        }
        ///<summary>
        /// Steve Lydford - 12/05/2008.
        ///
        /// Encrypts a file using Rijndael algorithm.
        ///</summary>
        ///<param name="inputFile"></param>
        ///<param name="outputFile"></param>
        ///<param name="password"></param>
        public static void EncryptFile(string inputFile, string outputFile, string password)
        {
            using (RijndaelManaged aes = new RijndaelManaged())
            {
                byte[] key = ASCIIEncoding.UTF8.GetBytes(password);

                /* This is for demostrating purposes only. 
                 * Ideally you will want the IV key to be different from your key and you should always generate a new one for each encryption in other to achieve maximum security*/
                byte[] IV = ASCIIEncoding.UTF8.GetBytes(password);

                using (FileStream fsCrypt = new FileStream(outputFile, FileMode.Create))
                {
                    using (ICryptoTransform encryptor = aes.CreateEncryptor(key, IV))
                    {
                        using (CryptoStream cs = new CryptoStream(fsCrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (FileStream fsIn = new FileStream(inputFile, FileMode.Open))
                            {
                                int data;
                                while ((data = fsIn.ReadByte()) != -1)
                                    cs.WriteByte((byte)data);
                            }
                        }
                    }
                }
            }
        }
        ///<summary>
        /// Steve Lydford - 12/05/2008.
        ///
        /// Decrypts a file using Rijndael algorithm.
        ///</summary>
        ///<param name="inputFile"></param>
        ///<param name="outputFile"></param>
        ///<param name="password"></param>
        public static void DecryptFile(string inputFile, string outputFile, string password)
        {
            using (RijndaelManaged aes = new RijndaelManaged())
            {
                byte[] key = ASCIIEncoding.UTF8.GetBytes(password);

                /* This is for demostrating purposes only. 
                 * Ideally you will want the IV key to be different from your key and you should always generate a new one for each encryption in other to achieve maximum security*/
                byte[] IV = ASCIIEncoding.UTF8.GetBytes(password);

                using (FileStream fsCrypt = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                {
                    if (File.Exists(outputFile)) File.Delete(outputFile);
                    using (FileStream fsOut = new FileStream(outputFile, FileMode.Create))
                    {
                        using (ICryptoTransform decryptor = aes.CreateDecryptor(key, IV))
                        {
                            using (CryptoStream cs = new CryptoStream(fsCrypt, decryptor, CryptoStreamMode.Read))
                            {
                                int data;
                                while ((data = cs.ReadByte()) != -1)
                                {
                                    fsOut.WriteByte((byte)data);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    #endregion
}
