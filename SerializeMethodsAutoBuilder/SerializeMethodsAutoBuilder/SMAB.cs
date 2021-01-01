using InSharp;
using NetBinSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SerializeMethodsAutoBuilder {

	public class SMAB : SerializationMethodsBuilder {

		public static bool ENABLE_LISTING { get; set; } = false;

		public ISerializationMethods getSerializationMethods(Type type, bool withCache) {
			GlobalCompilationContext context = new GlobalCompilationContext(type, ENABLE_LISTING);
			context.compileAndCompose();

			if(withCache) {
				foreach(KeyValuePair<Type, ISerializationMethods> pair in context.dependencies)
					Serializer.cache(pair.Value, pair.Key);
			}
			return context.dependencies[type];
		}
	}

	public class SerializationMethodsWithDependencies : ISerializationMethods {

		public delegate void SerializeMethod(SerializeStream stream, object obj, ISerializationMethods[] dependencies);
		public SerializeMethod serializeMethod { get; set; }
		public delegate object DeserializeMethod(SerializeStream stream, ISerializationMethods[] dependencies);
		public DeserializeMethod deserializeMethod { get; set; }
		public ISerializationMethods[] dependencies { get; set; }

		public SerializationMethodsWithDependencies() {
			this.serializeMethod = null;
			this.deserializeMethod = null;
			this.dependencies = null;
		}

		public void serialize(SerializeStream stream, object obj) {
			serializeMethod(stream, obj, dependencies);
		}

		public object deserialize(SerializeStream stream) {
			return deserializeMethod(stream, dependencies);
		}
	}

	class GlobalCompilationContext { 

		Stack<FieldInfo> fieldsStack = new Stack<FieldInfo>();
		
		//Only compiled or added to compile queue methods containers
		public Dictionary<Type, ISerializationMethods> dependencies { private set; get; } = new Dictionary<Type, ISerializationMethods>();

		Queue<LocalCompilationContext> composeQueue = new Queue<LocalCompilationContext>();
		Queue<LocalCompilationContext> compileQueue = new Queue<LocalCompilationContext>();
		public bool WithComments { get; private set; }

		public GlobalCompilationContext(Type targetType, bool withComments = false) { 
			this.WithComments = withComments;

			//Wow! It's beautifull
			addTypeDependence(targetType);
		}

		public ISerializationMethods addTypeDependence(Type type) { 
			ISerializationMethods result;
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
						result = (ISerializationMethods)typeof(CollectionSerializationMethodsChain<,>).MakeGenericType(type, elementType).GetConstructor(new Type[] { typeof(ISerializationMethods), typeof(bool) }).Invoke(new object[] { addTypeDependence(elementType), false });
					} else if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) { 
						Type[] genericArgs = type.GetGenericArguments();
						result = (ISerializationMethods)typeof(KeyValuePairSerializationMethodsChain<,,>).MakeGenericType(type, genericArgs[0], genericArgs[1]).GetConstructor(new Type[] { typeof(ISerializationMethods), typeof(ISerializationMethods), typeof(bool) }).Invoke(new object[] { addTypeDependence(genericArgs[0]), addTypeDependence(genericArgs[1]), false });
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

		public ISerializationMethods getTypeSerializationMethods(Type type) { 
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
		static LocalCompilationContext() { 
			serializeMethodInfo = typeof(ISerializationMethods).GetMethod("serialize", BindingFlags.Public | BindingFlags.Instance);
			deserializeMethodInfo = typeof(ISerializationMethods).GetMethod("deserialize", BindingFlags.Public | BindingFlags.Instance);
		}

		public int getTypeSerializationMethodsIndex(Type type) { 
			globalContext.addTypeDependence(type);

			if(serializeationethodsIndices.TryGetValue(type, out int typeIndex))
				return typeIndex;
			serializeationethodsIndices.Add(type, currentSerializeMethodsIndex);
			return currentSerializeMethodsIndex++;
		}

		public void compose() { 
			serializationMethods.dependencies = new ISerializationMethods[serializeationethodsIndices.Count];
			foreach(KeyValuePair<Type, int> pair in serializeationethodsIndices) {
				serializationMethods.dependencies[pair.Value] = globalContext.getTypeSerializationMethods(pair.Key);
			}
		}

		const byte NOT_NULL_FLAG = 0;
		const byte IS_NULL_FLAG = 1;

		public void compile() { 
			var serializeGen = new ILGen<Action<SerializeStream, object, ISerializationMethods[]>>(type + "_serialize", true);
			var deserializeGen = new ILGen<Func<SerializeStream, ISerializationMethods[], object>>(type + "_deserialize", true);

			//Check for null
			if(!type.IsValueType) { 
				RWMethodsInfo byteRWMethodsInfo = SerializeStream.getBaseTypeRWMethods(typeof(byte));
				serializeGen.If(Expr.Equals(serializeGen.args[1], Expr.NULL));
					serializeGen.Line(serializeGen.args[0].CallMethod(byteRWMethodsInfo.writeMethodInfo, Expr.Const(IS_NULL_FLAG)));
					serializeGen.Return();
				serializeGen.EndIf();
				serializeGen.Line(serializeGen.args[0].CallMethod(byteRWMethodsInfo.writeMethodInfo, Expr.Const(NOT_NULL_FLAG)));



				deserializeGen.If(Expr.Equals(serializeGen.args[0].CallMethod(byteRWMethodsInfo.readMethodInfo).Cast<byte>(), Expr.Const(IS_NULL_FLAG)));
					deserializeGen.Return(Expr.NULL);
				deserializeGen.EndIf();
			}

			ILVar serializeObject = serializeGen.DeclareVar(type);
			ILVar deserializeObject = deserializeGen.DeclareVar(type);

			
			serializeGen.Line(serializeObject.Set(Expr.Cast(serializeGen.args[1], type)));
			deserializeGen.Line(deserializeObject.Set(Expr.CreateUninitialized(type)));
			
			foreach(FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) { 
				Type fieldType = fieldInfo.FieldType;

				if(SerializeStream.getBaseTypeRWMethodsIfExists(fieldType, out RWMethodsInfo rwMethods)) { 
					serializeGen.Line(serializeGen.args[0].CallMethod(rwMethods.writeMethodInfo, serializeObject.Field(fieldInfo)));
					deserializeGen.Line(deserializeObject.Field(fieldInfo).Set(deserializeGen.args[0].CallMethod(rwMethods.readMethodInfo)));
				} else { 
					int fieldTypeSerializationIndex = getTypeSerializationMethodsIndex(fieldType);
					serializeGen.Line(serializeGen.args[2].Index(Expr.Const(fieldTypeSerializationIndex)).CallMethod(serializeMethodInfo, serializeGen.args[0], serializeObject.Field(fieldInfo)));
					deserializeGen.Line(deserializeObject.Field(fieldInfo).Set(Expr.Cast(deserializeGen.args[1].Index(Expr.Const(fieldTypeSerializationIndex)).CallMethod(deserializeMethodInfo, serializeGen.args[0]), fieldType)));
				}
				
			}
			
			
			deserializeGen.Return(deserializeObject);
			
			serializationMethods.serializeMethod = new SerializationMethodsWithDependencies.SerializeMethod(serializeGen.compile(globalContext.WithComments));
			serializationMethods.deserializeMethod = new SerializationMethodsWithDependencies.DeserializeMethod(deserializeGen.compile(globalContext.WithComments));
		}
	}
}
