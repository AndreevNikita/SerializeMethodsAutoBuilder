using InSharp;
using NetBinSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SerializeMethodsAutoBuilder {

	public class SMAB : ISerializationMethodsBuilder {

		public static bool ENABLE_LISTING { get; set; } = false;
		private readonly bool enableClassesTypes;

		public SMAB(bool enableClassesTypes = true) { 
			this.enableClassesTypes = enableClassesTypes;
		}

		public SerializationMethodsBase GetSerializationMethods(Type type, bool withCache) {
			GlobalCompilationContext context = new GlobalCompilationContext(type, enableClassesTypes, ENABLE_LISTING);
			context.CompileAndCompose();

			if(withCache) {
				foreach(KeyValuePair<Type, SerializationMethodsBase> pair in context.Dependencies)
					Serializer.Cache(pair.Value, pair.Key);
			}
			return context.Dependencies[type];
		}
	}

	public class SerializationMethodsWithDependencies : SerializationMethodsBase {

		public delegate void SerializeMethod(SerializeStream stream, object obj, SerializationContext context, SerializationMethodsBase[] dependencies);
		public SerializeMethod TypeSerializeMethod { get; set; }
		public delegate object DeserializeMethod(SerializeStream stream, DeserializationContext context, SerializationMethodsBase[] dependencies);
		public DeserializeMethod TypeDeserializeMethod { get; set; }
		public SerializationMethodsBase[] Dependencies { get; set; }

		public SerializationMethodsWithDependencies() {
			this.TypeSerializeMethod = null;
			this.TypeDeserializeMethod = null;
			this.Dependencies = null;
		}

		public override void Serialize(SerializeStream stream, object obj, SerializationContext context) {
			TypeSerializeMethod(stream, obj, context, Dependencies);
		}

		public override object Deserialize(SerializeStream stream, DeserializationContext context) {
			return TypeDeserializeMethod(stream, context, Dependencies);
		}
	}

	class GlobalCompilationContext { 

		Stack<FieldInfo> fieldsStack = new Stack<FieldInfo>();
		
		//Only compiled or added to compile queue methods containers
		public Dictionary<Type, SerializationMethodsBase> Dependencies { private set; get; } = new Dictionary<Type, SerializationMethodsBase>();

		Queue<LocalCompilationContext> composeQueue = new Queue<LocalCompilationContext>();
		Queue<LocalCompilationContext> compileQueue = new Queue<LocalCompilationContext>();
		public bool WithComments { get; private set; }
		public bool EnableClassesTypes { get; }

		public GlobalCompilationContext(Type targetType, bool enableClassesTypes, bool withComments = false) { 
			this.WithComments = withComments;
			this.EnableClassesTypes = enableClassesTypes;

			//Wow! It's beautifull
			AddTypeDependence(targetType);
		}

		public SerializationMethodsBase AddTypeDependence(Type type) { 
			SerializationMethodsBase result;
			if(Dependencies.TryGetValue(type, out result)) {
				return result;
			} else {
				if(Serializer.GetCached(type, out result)) { 
				} else {
					
					// Collections like types
					if(type.IsArray) {
						result = new ArraySerializationMethodsChain(type, AddTypeDependence(type.GetElementType()), false);
					} else if(type.GetInterfaces().Any((Type t) => SerializeStream.IsCollectionType(t))) { 
						Type elementType = type.GetInterfaces().First((Type interfaceType) => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>)).GetGenericArguments()[0];
						result = (SerializationMethodsBase)typeof(CollectionSerializationMethodsChain<,>).MakeGenericType(type, elementType).GetConstructor(new Type[] { typeof(SerializationMethodsBase), typeof(bool) }).Invoke(new object[] { AddTypeDependence(elementType), false });
					} else if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) { 
						Type[] genericArgs = type.GetGenericArguments();
						result = (SerializationMethodsBase)typeof(KeyValuePairSerializationMethodsChain<,,>).MakeGenericType(type, genericArgs[0], genericArgs[1]).GetConstructor(new Type[] { typeof(SerializationMethodsBase), typeof(SerializationMethodsBase), typeof(bool) }).Invoke(new object[] { AddTypeDependence(genericArgs[0]), AddTypeDependence(genericArgs[1]), false });
					} 
					else {
						LocalCompilationContext compileContext = new LocalCompilationContext(type, this);
						compileQueue.Enqueue(compileContext);
						result = compileContext.serializationMethods;
					}
				}
			}
			Dependencies.Add(type, result);
			return result;
		}

		public SerializationMethodsBase GetTypeSerializationMethods(Type type) { 
			return Dependencies[type];
		}

		public void CompileAndCompose() { 
			while(compileQueue.Count != 0) {
				LocalCompilationContext context = compileQueue.Dequeue();
				context.Compile();
				composeQueue.Enqueue(context);
			}

			if(WithComments) { 
				Console.WriteLine();
				Console.WriteLine("Dependencies list:");
				foreach(Type dependenceType in Dependencies.Keys)
					Console.WriteLine(dependenceType);
			}

			while (composeQueue.Count != 0) {
				LocalCompilationContext context = composeQueue.Dequeue();
				context.Compose();
			}
		}
		

	}

	class LocalCompilationContext { 

		GlobalCompilationContext globalContext;
		Dictionary<Type, int> serializeationethodsIndices = new Dictionary<Type, int>();
		int currentSerializeMethodsIndex = 0;
		public SerializationMethodsWithDependencies serializationMethods { private set; get; }
		Type type;

		public LocalCompilationContext(Type type, GlobalCompilationContext globalContext) { 
			this.globalContext = globalContext;
			this.serializationMethods = new SerializationMethodsWithDependencies();
			this.type = type;
		}

		private static readonly MethodInfo SerializeMethodInfo;
		private static readonly MethodInfo DeserializeMethodInfo;

		private static readonly MethodInfo OptimizeSerializationContextMethodInfo;
		private static readonly MethodInfo OptimizeDeserializationContextMethodInfo;
		private static readonly MethodInfo AddObjectDesreializationContextMethodInfo;

		private static readonly FieldInfo OptimizationResultItem1;
		private static readonly FieldInfo OptimizationResultItem2;

		private static void AssertNull(object obj, string message) {
			if(obj == null) {
				throw new Exception(message);
			}
		}
		

		static LocalCompilationContext() { 
			AssertNull(SerializeMethodInfo = typeof(SerializationMethodsBase).GetMethod("Serialize", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(SerializeStream), typeof(object), typeof(SerializationContext)}, null), "SerializeStream.serialize MethodInfo is null");
			AssertNull(DeserializeMethodInfo = typeof(SerializationMethodsBase).GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(SerializeStream), typeof(DeserializationContext)}, null), "SerializeStream.deserialize MethodInfo is null");

			AssertNull(OptimizeSerializationContextMethodInfo = typeof(SerializationContext).GetMethod("Optimize", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(SerializeStream), typeof(object) }, null), "SerializationContext.optimize MethodInfo is null");
			AssertNull(OptimizeDeserializationContextMethodInfo = typeof(DeserializationContext).GetMethod("Optimize", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(SerializeStream) }, null), "DeserializationContext.optimize MethodInfo is null");
			AssertNull(AddObjectDesreializationContextMethodInfo = typeof(DeserializationContext).GetMethod("AddObject", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(object) }, null), "DeserializationContext.addObject MethodInfo is null");
			
			AssertNull(OptimizationResultItem1 = typeof((bool, object)).GetField("Item1", BindingFlags.Public | BindingFlags.Instance), "Item1 field not found in optimizationResult tuple");
			AssertNull(OptimizationResultItem2 = typeof((bool, object)).GetField("Item2", BindingFlags.Public | BindingFlags.Instance), "Item2 field not found in optimizationResult tuple");
		}

		public int GetTypeSerializationMethodsIndex(Type type) { 
			globalContext.AddTypeDependence(type);

			if(serializeationethodsIndices.TryGetValue(type, out int typeIndex))
				return typeIndex;
			serializeationethodsIndices.Add(type, currentSerializeMethodsIndex);
			return currentSerializeMethodsIndex++;
		}

		public void Compose() { 
			serializationMethods.Dependencies = new SerializationMethodsBase[serializeationethodsIndices.Count];
			foreach(KeyValuePair<Type, int> pair in serializeationethodsIndices) {
				serializationMethods.Dependencies[pair.Value] = globalContext.GetTypeSerializationMethods(pair.Key);
			}
		}


		public void Compile() { 
			if(!globalContext.EnableClassesTypes)
				if(type.IsClass)
					throw new ArgumentException($"Can't compile serialization functions for type {type}. To enable classes types use SMAB(enableClassesTypes = true) constructor");

			var serializeGen = new ILGen<Action<SerializeStream, object, SerializationContext, SerializationMethodsBase[]>>(type + "_serialize", true);
			var deserializeGen = new ILGen<Func<SerializeStream, DeserializationContext, SerializationMethodsBase[], object>>(type + "_deserialize", true);

			SerializationRule typeRule = (SerializationRule)type.GetCustomAttribute(typeof(SerializationRule));
			bool defaultIsSerializable = typeRule != null ? typeRule.IsSerializable : true;

			ILVar optimizationResult = deserializeGen.DeclareVar(typeof((bool, object)));
			//Context optimization
			if(!type.IsValueType) { 
			
				serializeGen.If(serializeGen.args[2].CallMethod(OptimizeSerializationContextMethodInfo, serializeGen.args[0], serializeGen.args[1]));
					serializeGen.Return();
				serializeGen.EndIf();

				
				deserializeGen.Line(optimizationResult.Set(deserializeGen.args[1].CallMethod(OptimizeDeserializationContextMethodInfo, deserializeGen.args[0])));
				deserializeGen.If(optimizationResult.Field(OptimizationResultItem1));
					deserializeGen.Return(optimizationResult.Field(OptimizationResultItem2));
				deserializeGen.EndIf();
			
			}

			ILVar serializeObject = serializeGen.DeclareVar(type);
			ILVar deserializeObject = deserializeGen.DeclareVar(type);


			
			serializeGen.Line(serializeObject.Set(Expr.Cast(serializeGen.args[1], type)));
			deserializeGen.Line(deserializeObject.Set(Expr.CreateUninitialized(type)));

			if(!type.IsValueType) {
				deserializeGen.Line(deserializeGen.args[1].CallMethod(AddObjectDesreializationContextMethodInfo, deserializeObject));
			}
			
			foreach(FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy((FieldInfo fieldInfo) => fieldInfo.MetadataToken)) { 
				SerializationRule fieldRule = (SerializationRule)fieldInfo.GetCustomAttribute(typeof(SerializationRule));
				if(!(fieldRule != null ? fieldRule.IsSerializable : defaultIsSerializable))
					continue;

				Type fieldType = fieldInfo.FieldType;

				if(SerializeStream.GetBaseTypeRWMethodsIfExists(fieldType, out RWMethodsInfo rwMethods)) { 
					serializeGen.Line(serializeGen.args[0].CallMethod(rwMethods.writeMethodInfo, serializeObject.Field(fieldInfo)));
					deserializeGen.Line(deserializeObject.Field(fieldInfo).Set(deserializeGen.args[0].CallMethod(rwMethods.readMethodInfo)));
				} else { 
					int fieldTypeSerializationIndex = GetTypeSerializationMethodsIndex(fieldType);
					serializeGen.Line(serializeGen.args[3].Index(Expr.Const(fieldTypeSerializationIndex)).CallMethod(SerializeMethodInfo, serializeGen.args[0], serializeObject.Field(fieldInfo), serializeGen.args[2]));
					deserializeGen.Line(deserializeObject.Field(fieldInfo).Set(Expr.Cast(deserializeGen.args[2].Index(Expr.Const(fieldTypeSerializationIndex)).CallMethod(DeserializeMethodInfo, serializeGen.args[0], serializeGen.args[1]), fieldType)));
				}
				
			}
			
			deserializeGen.Return(deserializeObject);
			
			serializationMethods.TypeSerializeMethod = new SerializationMethodsWithDependencies.SerializeMethod(serializeGen.compile(globalContext.WithComments));
			serializationMethods.TypeDeserializeMethod = new SerializationMethodsWithDependencies.DeserializeMethod(deserializeGen.compile(globalContext.WithComments));
		}
	}
}
