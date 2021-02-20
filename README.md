# SerializeMethodsAutoBuilder (SMAB)
**ISerializationMethodsBuilder** realization for [NetBinSerializer](https://github.com/AndreevNikita/NetBinSerializer) library.

- Makes difficult types (custom classes and structures) serialization/deserialization possible with NetBinSerializer.


## How to use
Very simple! `Serializer.UseMethodsBuilder(new SMAB());` and Serializer will use this AutoBuilder for unknown types.


## Examples

```c#
//Set SMAB as SerializeMethodsBuilder
Serializer.UseMethodsBuilder(new SMAB());

//Example object init
MyObject obj = new MyObject();
//...

//Serialization
SerializeStream sstream = new SerializeStream();
Serializer.Serialize<MyObject>(sstream, obj);
byte[] data = sstream.GetBytes(); //Object bytes

//Deserialization
SerializeStream dstream = new SerializeStream(data);
MyObject dobj = Serializer.Deserialize<MyObject>(dstream); //Your deserialized object here

//...
```

Also you can build serialization methods by yourself and use it without Serializer class
```c#
SerializationMethodsBase methods = new SMAB().GetSerializationMethods(typeof(MyObject), false /* Enable built methods cache in Serializer */);

MyObject obj = new MyObject();
//...

//Serialization
SerializeStream sstream = new SerializeStream();
methods.Serialize(sstream, obj);
byte[] data = sstream.GetBytes(); //Object bytes

//Deserialization
SerializeStream dstream = new SerializeStream(data);
MyObject dobj = (MyObject)methods.Deserialize(dstream); //Your deserialized object here

//...
```

## Full examples and tests
* [Serialization/deserialization example and test](https://github.com/AndreevNikita/SerializeMethodsAutoBuilder/blob/main/SerializeMethodsAutoBuilder/Test/Program.cs)
* [Nulls and reference cycles test](https://github.com/AndreevNikita/SerializeMethodsAutoBuilder/blob/main/SerializeMethodsAutoBuilder/Test/Test2.cs)

## Also
The library uses [InSharp](https://github.com/AndreevNikita/InSharp) to compile serialize/deserialize methods