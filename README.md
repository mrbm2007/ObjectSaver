# MRB_ObjectSaver
This project helps you save/load/clone any object in c# to/from a file/string.

In compare to "c# serialization" this method keeps reference to objects and link between objects will not break. 

(see: SerializeObjectTest.cs for an example or here https://stackoverflow.com/questions/47477234/serializeobject-deserializeobject-escapes-references)

Furthermore, the type have not to be marked as [Serializable]

This method can save public/internal/private fields and auto generated fileds for properties (set;get;).

# MRB_ObjectSaver
