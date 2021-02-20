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
		private int[,] matrix;
		
		public MyObject(Vector pos, string name, params string[] hobbies) {
			this.Pos = pos;
			this.Name = name;
			this.Hobbies = new List<string>(hobbies);
			this.matrix = new int[,]{ {1, 2, 1}, {2, 4, 2}, {1, 2, 1} };
			KnownWords = new Dictionary<string, Dictionary<string, string>>(); 
		}

		public void sayHello() {
			Console.WriteLine($"Hello! My name is {Name}");
			Console.WriteLine($"My pos: {Pos}");
			Console.Write("I like ");
			for(int index = 0; index < Hobbies.Count; index++)
				Console.Write(index == 0 ? $"{Hobbies[index]}" : (index != Hobbies.Count - 1 ? $", {Hobbies[index]}" : $" and {Hobbies[index]}\n"));
			Console.WriteLine("I was translated by bytes array! It means, that I can be translated by network");

			Console.WriteLine("So it's my matrix: ");
			for(int i = 0; i < matrix.GetLength(0); i++) { 
				for(int j = 0; j < matrix.GetLength(1); j++) { 
					Console.Write($"{matrix[i, j]} ");
				}
				Console.WriteLine();
			}

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
			//SMAB.ENABLE_LISTING = true;
			Console.WriteLine("- Disable cache test");
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));

			test(false);
			test(false);
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("- Enable cache test");
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));
			test(true);
			test(true);

			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("- Test serializer and nulls");
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));
			Console.WriteLine(String.Concat(Enumerable.Repeat("-", 100).ToArray()));
			Test2.test();
			Console.ReadKey();
		}

		public static void test(bool enableCache) { 
			SerializationMethodsBase methods = new SMAB().GetSerializationMethods(typeof(MyObject), enableCache);

			MyObject writeObject = new MyObject(new Vector { x = 4, y = 3, z = 5 }, "Tom", "to play in computer games", "eat testy food", "code");
			writeObject.KnownWords["russian"] = new Dictionary<string, string> { 
				{ "cat", "кот" },
				{ "language", "язык" },
				{ "mouse", "мышь" },
				{ "code", "код" },
			};
			
			SerializeStream sstream = new SerializeStream();
			methods.Serialize(sstream, writeObject);

			Console.WriteLine();
			SerializeStream dstream = new SerializeStream(sstream.GetBytes());
			MyObject readObject = ((MyObject)methods.Deserialize(dstream));
			readObject.sayHello();
		}

	}
}
