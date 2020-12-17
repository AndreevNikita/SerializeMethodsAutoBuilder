using InSharp;
using NetBinSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SerializeMethodsAutoBuilder {


	public static class SMAB {
		public delegate void Test(object obj, SerializeStream stream);
		public static SerializeMethods BuildSerializeMethods(Type type, bool withComments = false) { 
			var serializeGen = new ILGen<Action<object, SerializeStream>>(type + "_serialize", true);
			var deserializeGen = new ILGen<Func<SerializeStream, object>>(type + "_deserialize", true);
			ILVar serializeObject = serializeGen.DeclareVar(type);
			ILVar deserializeObject = deserializeGen.DeclareVar(type);

			serializeGen.Line(serializeObject.Set(serializeGen.args[0]));

			EmitCodeRecursive(serializeGen, deserializeGen, serializeObject, deserializeObject, new Stack<FieldInfo>(), type);
			
			deserializeGen.Return(deserializeObject);

			try {
				var serializeFunc = serializeGen.compile(withComments);
				var deserializeFunc = deserializeGen.compile(withComments);
				return new SerializeMethods(new SerializeMethods.SerializeMethod(serializeFunc), new SerializeMethods.DeserializeMethod(deserializeFunc));
			} catch(Exception e) { 
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
			}
			return null;
		}



		public static void EmitCodeRecursive(ILGen serializeGen, ILGen deserializeGen, ILVar serializeObject, ILVar deserializeObject, Stack<FieldInfo> fieldsStack, Type currentType) { 
			
			//Get the current field path path
			ILAssignable serializeFieldsPath = serializeObject;
			ILAssignable deserializeFieldsPath = deserializeObject;
			foreach(FieldInfo field in fieldsStack.Reverse()) {
				serializeFieldsPath = serializeFieldsPath.Field(field);
				deserializeFieldsPath = deserializeFieldsPath.Field(field);
			}

			RWMethodsInfo rwMethods = default(RWMethodsInfo);
			bool castToType = false;
			Type interfaceTypeBuffer = null;

			if(SerializeStream.getBaseTypeRWMethodsIfExists(currentType, out rwMethods)) { 
			} else if(typeof(Serializable).IsAssignableFrom(currentType)) { 
				rwMethods = SerializeStream.rwSerializableMethodsInfo;
				castToType = true;
			} else if(currentType.IsArray) {
				rwMethods = new RWMethodsInfo(
					SerializeStream.rwArrayMethodsInfo.readMethodInfo.MakeGenericMethod(currentType),
					SerializeStream.rwArrayMethodsInfo.writeMethodInfo.MakeGenericMethod(currentType)
				);
				castToType = true;
			} else if(currentType.GetInterfaces().Any((Type checkInterface) => { 
					if(checkInterface.IsGenericType && checkInterface.GetGenericTypeDefinition() == typeof(ICollection<>))  { 
						interfaceTypeBuffer = checkInterface;  
						return true; 
					}
					return false;
				})) { 
				Type elementType = interfaceTypeBuffer.GetGenericArguments()[0];
				if(elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) {
					Type[] genericArgs = elementType.GetGenericArguments();
					rwMethods = new RWMethodsInfo(
						SerializeStream.rwKeyValuePairsCollectionMethodsInfo.readMethodInfo.MakeGenericMethod(currentType, genericArgs[0], genericArgs[1]),
						SerializeStream.rwKeyValuePairsCollectionMethodsInfo.writeMethodInfo.MakeGenericMethod(genericArgs[0], genericArgs[1])
					);

				} else { 
					rwMethods = new RWMethodsInfo(
						SerializeStream.rwCollectionMethodsInfo.readMethodInfo.MakeGenericMethod(currentType, elementType),
						SerializeStream.rwCollectionMethodsInfo.writeMethodInfo.MakeGenericMethod(elementType)
					);
				}
			} else {
				
				deserializeGen.Line(deserializeFieldsPath.Set(Expr.CreateUninitialized(currentType)));

				foreach(FieldInfo fieldInfo in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) { 
					Type fieldType = fieldInfo.FieldType;
					fieldsStack.Push(fieldInfo);


					EmitCodeRecursive(serializeGen, deserializeGen, serializeObject, deserializeObject, fieldsStack, fieldType);

					fieldsStack.Pop();
				}
				return;
			}

			Expr serializeExpr = serializeFieldsPath;
			ILAssignable deserializeExpr = deserializeFieldsPath;

			serializeGen.Line(serializeGen.args[1].CallMethod(rwMethods.writeMethodInfo, serializeExpr));
			deserializeGen.Line(deserializeExpr.Set(castToType ? deserializeGen.args[0].CallMethod(rwMethods.readMethodInfo).Cast(currentType) : deserializeGen.args[0].CallMethod(rwMethods.readMethodInfo)));
				
		}


	}
}
