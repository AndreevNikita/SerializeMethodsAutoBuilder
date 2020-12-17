using NetBinSerializer;
using SerializeMethodsAutoBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Test {


	struct Vector {
		public int x, y, z;

		public override string ToString() {
			return $"({x}; {y}; {z})";
		}
	}

	class MyObject { 

		public Vector Pos { get; set; }

		public string Name { get; set; }
		public List<string> Hobbies { get; set;}
		public Dictionary<string, Dictionary<string, string>> KnownWords { get; private set; }
		
		public MyObject(Vector pos, string name, params string[] hobbies) {
			this.Pos = pos;
			this.Name = name;
			this.Hobbies = new List<string>(hobbies);
			KnownWords = new Dictionary<string, Dictionary<string, string>>(); 
		}

		public void sayHello() {
			Console.WriteLine($"Hello! My name is {Name}");
			Console.WriteLine($"My pos: {Pos}");
			Console.Write("I like ");
			for(int index = 0; index < Hobbies.Count; index++)
				Console.Write(index == 0 ? $"{Hobbies[index]}" : (index != Hobbies.Count - 1 ? $", {Hobbies[index]}" : $" and {Hobbies[index]}\n"));
			Console.WriteLine("I was translated by bytes array! It means, that I can be translated by network");

			
			Random rand = new Random();
			string[] languagesArray = KnownWords.Keys.ToArray();
			if(languagesArray.Length == 0) { 
				Console.WriteLine("I don't know any other languages");
			} else {
				string lang = languagesArray[rand.Next(languagesArray.Length)];
				Console.WriteLine("I know, that");
				foreach(KeyValuePair<string, string> wordsPair in KnownWords[lang])
					Console.WriteLine($"{wordsPair.Key} is {wordsPair.Value}");
				Console.WriteLine($"on {lang} language");
			}
		}
	}

	class Program {
		static void Main(string[] args) {
			SerializeMethods methods = SMAB.BuildSerializeMethods(typeof(MyObject));

			MyObject writeObject = new MyObject(new Vector { x = 4, y = 3, z = 5 }, "Tom", "to play in computer games", "eat testy food", "code");
			writeObject.KnownWords["russian"] = new Dictionary<string, string> { 
				{ "cat", "кот" },
				{ "language", "язык" },
				{ "mouse", "мышь" },
				{ "code", "код" },
			};
			
			SerializeStream sstream = new SerializeStream();
			methods.serializeMethod(writeObject, sstream);

			Console.WriteLine();
			SerializeStream dstream = new SerializeStream(sstream.getBytes());
			((MyObject)methods.deserializeMethod(dstream)).sayHello();
			Console.ReadKey();
		}

		public static object readVector(SerializeStream ds) { 
			object obj;
			Vector vec = (Vector)FormatterServices.GetUninitializedObject(typeof(Vector));
			vec.x = ds.readInt32();
			vec.y = ds.readInt32();
			vec.z = ds.readInt32();
			obj = vec;
			return obj;
		}

	}
}
