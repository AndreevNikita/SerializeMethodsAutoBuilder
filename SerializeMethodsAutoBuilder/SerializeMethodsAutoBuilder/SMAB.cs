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

		public SerializationMethodsBase getSerializationMethods(Type type, bool withCache) {
			GlobalCompilationContext context = new GlobalCompilationContext(type, enableClassesTypes, ENABLE_LISTING);
			context.compileAndCompose();

			if(withCache) {
				foreach(KeyValuePair<Type, SerializationMethodsBase> pair in context.dependencies)
					Serializer.cache(pair.Value, pair.Key);
			}
			return context.dependencies[type];
		}
	}

	public class SerializationMethodsWithDependencies : SerializationMethodsBase {

		public delegate void SerializeMethod(SerializeStream stream, object obj, SerializationContext context, SerializationMethodsBase[] dependencies);
		public SerializeMethod serializeMethod { get; set; }
		public delegate object DeserializeMethod(SerializeStream stream, DeserializationContext context, SerializationMethodsBase[] dependencies);
		public DeserializeMethod deserializeMethod { get; set; }
		public SerializationMethodsBase[] dependencies { get; set; }

		public SerializationMethodsWithDependencies() {
			this.serializeMethod = null;
			this.deserializeMethod = null;
			this.dependencies = null;
		}

		public override void serialize(SerializeStream stream, object obj, SerializationContext context) {
			serializeMethod(stream, obj, context, dependencies);
		}

		public override object deserialize(SerializeStream stream, DeserializationContext context) {
			return deserializeMethod(stream, context, dependencies);
		}
	}

	class GlobalCompilationContext { 

		Stack<FieldInfo> fieldsStack = new Stack<FieldInfo>();
		
		//Only compiled or added to compile queue methods containers
		public Dictionary<Type, SerializationMethodsBase> dependencies { private set; get; } = new Dictionary<Type, SerializationMethodsBase>();

		Queue<LocalCompilationContext> composeQueue = new Queue<LocalCompilationContext>();
		Queue<LocalCompilationContext> compileQueue = new Queue<LocalCompilationContext>();
		public bool WithComments { get; private set; }
		public bool EnableClassesTypes { get; }

		public GlobalCompilationContext(Type targetType, bool enableClassesTypes, bool withComments = false) { 
			this.WithComments = withComments;
			this.EnableClassesTypes = enableClassesTypes;

			//Wow! It's beautifull
			addTypeDependence(targetType);
		}

		public SerializationMethodsBase addTypeDependence(Type type) { 
			SerializationMethodsBase result;
			if(dependencies.TryGetValue(type, out result)) {
				return result;
			} else {
				if(Serializer.getCached(type, out result)) { 
				} else {
					
					// Collections like types
					if(type.IsArray) {
						result = new ArraySerializationMethodsChain(type, addTypeDependence(type.GetElementType()), false);
					} else if(type.GetInterfaces().Any((Type t) => SerializeStream.isCollectionType(t))) { 
						Type elementType = type.GetInterfaces().First((Type interfaceType) => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>)).GetGenericArguments()[0];
						result = (SerializationMethodsBase)typeof(CollectionSerializationMethodsChain<,>).MakeGenericType(type, elementType).GetConstructor(new Type[] { typeof(SerializationMethodsBase), typeof(bool) }).Invoke(new object[] { addTypeDependence(elementType), false });
					} else if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) { 
						Type[] genericArgs = type.GetGenericArguments();
						result = (SerializationMethodsBase)typeof(KeyValuePairSerializationMethodsChain<,,>).MakeGenericType(type, genericArgs[0], genericArgs[1]).GetConstructor(new Type[] { typeof(SerializationMethodsBase), typeof(SerializationMethodsBase), typeof(bool) }).Invoke(new object[] { addTypeDependence(genericArgs[0]), addTypeDependence(genericArgs[1]), false });
					} 
					else {
						LocalCompilationContext compileContext = new LocalCompilationContext(type, this);
						compileQueue.Enqueue(compileContext);
						result = compileContext.serializationMethods;
					}
				}
			}
			dependencies.Add(type, result);
			return result;
		}

		public SerializationMethodsBase getTypeSerializationMethods(Type type) { 
			return dependencies[type];
		}

		public void compileAndCompose() { 
			while(compileQueue.Count != 0) {
				LocalCompilationContext context = compileQueue.Dequeue();
				context.compile();
				composeQueue.Enqueue(context);
			}

			if(WithComments) { 
				Console.WriteLine();
				Console.WriteLine("Dependencies list:");
				foreach(Type dependenceType in dependencies.Keys)
					Console.WriteLine(dependenceType);
			}

			while (composeQueue.Count != 0) {
				LocalCompilationContext context = composeQueue.Dequeue();
				context.compose();
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

		private static readonly MethodInfo serializeMethodInfo;
		private static readonly MethodInfo deserializeMethodInfo;

		private static readonly MethodInfo optimizeSerializationContextMethodInfo;
		private static readonly MethodInfo optimizeDeserializationContextMethodInfo;
		private static readonly MethodInfo addObjectDesreializationContextMethodInfo;

		private static readonly FieldInfo optimizationResultItem1;
		private static readonly FieldInfo optimizationResultItem2;

		private static void assertNull(object obj, string message) {
			if(obj == null) {
				throw new Exception(message);
			}
		}
		

		static LocalCompilationContext() { 
			assertNull(serializeMethodInfo = typeof(SerializationMethodsBase).GetMethod("serialize", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(SerializeStream), typeof(object), typeof(SerializationContext)}, null), "SerializeStream.serialize MethodInfo is null");
			assertNull(deserializeMethodInfo = typeof(SerializationMethodsBase).GetMethod("deserialize", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(SerializeStream), typeof(DeserializationContext)}, null), "SerializeStream.deserialize MethodInfo is null");

			assertNull(optimizeSerializationContextMethodInfo = typeof(SerializationContext).GetMethod("optimize", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(SerializeStream), typeof(object) }, null), "SerializationContext.optimize MethodInfo is null");
			assertNull(optimizeDeserializationContextMethodInfo = typeof(DeserializationContext).GetMethod("optimize", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(SerializeStream) }, null), "DeserializationContext.optimize MethodInfo is null");
			assertNull(addObjectDesreializationContextMethodInfo = typeof(DeserializationContext).GetMethod("addObject", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(object) }, null), "DeserializationContext.addObject MethodInfo is null");
			
			assertNull(optimizationResultItem1 = typeof((bool, object)).GetField("Item1", BindingFlags.Public | BindingFlags.Instance), "Item1 field not found in optimizationResult tuple");
			assertNull(optimizationResultItem2 = typeof((bool, object)).GetField("Item2", BindingFlags.Public | BindingFlags.Instance), "Item2 field not found in optimizationResult tuple");
		}

		public int getTypeSerializationMethodsIndex(Type type) { 
			globalContext.addTypeDependence(type);

			if(serializeationethodsIndices.TryGetValue(type, out int typeIndex))
				return typeIndex;
			serializeationethodsIndices.Add(type, currentSerializeMethodsIndex);
			return currentSerializeMethodsIndex++;
		}

		public void compose() { 
			serializationMethods.dependencies = new SerializationMethodsBase[serializeationethodsIndices.Count];
			foreach(KeyValuePair<Type, int> pair in serializeationethodsIndices) {
				serializationMethods.dependencies[pair.Value] = globalContext.getTypeSerializationMethods(pair.Key);
			}
		}


		public void compile() { 
			if(!globalContext.EnableClassesTypes)
				if(type.IsClass)
					throw new ArgumentException($"Can't compile serialization functions for type {type}. To enable classes types use SMAB(enableClassesTypes = true) constructor");

			var serializeGen = new ILGen<Action<SerializeStream, object, SerializationContext, SerializationMethodsBase[]>>(type + "_serialize", true);
			var deserializeGen = new ILGen<Func<SerializeStream, DeserializationContext, SerializationMethodsBase[], object>>(type + "_deserialize", true);

			SerializationRule typeRule = (SerializationRule)type.GetCustomAttribute(typeof(SerializationRule));
			bool defaultIsSerializable = typeRule != null ? typeRule.isSerializable : true;

			ILVar optimizationResult = deserializeGen.DeclareVar(typeof((bool, object)));
			//Context optimization
			if(!type.IsValueType) { 
			
				serializeGen.If(serializeGen.args[2].CallMethod(optimizeSerializationContextMethodInfo, serializeGen.args[0], serializeGen.args[1]));
					serializeGen.Return();
				serializeGen.EndIf();

				
				deserializeGen.Line(optimizationResult.Set(deserializeGen.args[1].CallMethod(optimizeDeserializationContextMethodInfo, deserializeGen.args[0])));
				deserializeGen.If(optimizationResult.Field(optimizationResultItem1));
					deserializeGen.Return(optimizationResult.Field(optimizationResultItem2));
				deserializeGen.EndIf();
			
			}

			ILVar serializeObject = serializeGen.DeclareVar(type);
			ILVar deserializeObject = deserializeGen.DeclareVar(type);


			
			serializeGen.Line(serializeObject.Set(Expr.Cast(serializeGen.args[1], type)));
			deserializeGen.Line(deserializeObject.Set(Expr.CreateUninitialized(type)));

			if(!type.IsValueType) {
				deserializeGen.Line(deserializeGen.args[1].CallMethod(addObjectDesreializationContextMethodInfo, deserializeObject));
			}
			
			foreach(FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) { 
				SerializationRule fieldRule = (SerializationRule)fieldInfo.GetCustomAttribute(typeof(SerializationRule));
				if(!(fieldRule != null ? fieldRule.isSerializable : defaultIsSerializable))
					continue;

				Type fieldType = fieldInfo.FieldType;

				if(SerializeStream.getBaseTypeRWMethodsIfExists(fieldType, out RWMethodsInfo rwMethods)) { 
					serializeGen.Line(serializeGen.args[0].CallMethod(rwMethods.writeMethodInfo, serializeObject.Field(fieldInfo)));
					deserializeGen.Line(deserializeObject.Field(fieldInfo).Set(deserializeGen.args[0].CallMethod(rwMethods.readMethodInfo)));
				} else { 
					int fieldTypeSerializationIndex = getTypeSerializationMethodsIndex(fieldType);
					serializeGen.Line(serializeGen.args[3].Index(Expr.Const(fieldTypeSerializationIndex)).CallMethod(serializeMethodInfo, serializeGen.args[0], serializeObject.Field(fieldInfo), serializeGen.args[2]));
					deserializeGen.Line(deserializeObject.Field(fieldInfo).Set(Expr.Cast(deserializeGen.args[2].Index(Expr.Const(fieldTypeSerializationIndex)).CallMethod(deserializeMethodInfo, serializeGen.args[0], serializeGen.args[1]), fieldType)));
				}
				
			}
			
			deserializeGen.Return(deserializeObject);
			
			serializationMethods.serializeMethod = new SerializationMethodsWithDependencies.SerializeMethod(serializeGen.compile(globalContext.WithComments));
			serializationMethods.deserializeMethod = new SerializationMethodsWithDependencies.DeserializeMethod(deserializeGen.compile(globalContext.WithComments));
		}
	}
}
