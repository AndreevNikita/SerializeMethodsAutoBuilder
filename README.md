# SerializeMethodsAutoBuilder (SMAB)
*ISerializationMethodsBuilder* realization for [NetBinSerializer](https://github.com/AndreevNikita/NetBinSerializer) library.

- Makes difficult types (custom classes and structures) serialization/deserialization possible with NetBinSerializer.


## How to use
Very simple! `Serializer.useMethodsBuilder(new SMAB());` and Serializer will use this AutoBuilder for unknown types.

```c#
//Set SMAB as SerializeMethodsBuilder
Serializer.useMethodsBuilder(new SMAB());

//Example object init
MyObject obj = new MyObject();
//...

//Serialization
SerializeStream sstream = new SerializeStream();
Serializer.serialize<MyObject>(sstream, obj);
byte[] data = sstream.getBytes(); //Object bytes

//Deserialization
SerializeStream dstream = new SerializeStream(data);
MyObject dobj = Serializer.deserialize<MyObject>(dstream); //Your deserialized object here
```