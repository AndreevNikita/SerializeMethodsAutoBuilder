using NetBinSerializer;
using SerializeMethodsAutoBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test {

	class Friend { 

		string name;
		public Friend friend;

		public Friend(string name, Friend friend = null) { 
			this.name = name;
			this.friend = friend;
		}

		const int MAX_PRINT_DEPTH = 16;

		public void print() {
			int counter = 0;
			Friend currentFriend = this;
			while(currentFriend != null) {
				Console.WriteLine(currentFriend == this ? $"Hello! My name is {currentFriend.name}!" : $"His name is {currentFriend.name}");
				if(currentFriend.friend != null) {
					Console.WriteLine(currentFriend == this ? "I have a friend!" : "He has a friend!");
				} else { 
					Console.WriteLine(currentFriend == this ? "I have no friend :(" : "He has no friend :(");
				}
				currentFriend = currentFriend.friend;
				counter++; 
				if(counter == MAX_PRINT_DEPTH) { 
					Console.WriteLine("...");
					break;
				}
			}

		}

	}

	class Test2 {

		public static void test() { 
			Serializer.UseMethodsBuilder(new SMAB());
			Friend friend = new Friend("Tom", new Friend("Bob", new Friend("Robin", new Friend("Marry"))));
			SerializeStream sstream = new SerializeStream();
			Serializer.Serialize<Friend>(sstream, friend);
			Serializer.Deserialize<Friend>(new SerializeStream(sstream.GetBytes())).print();

			Console.WriteLine();
			Console.WriteLine($"{String.Concat(Enumerable.Repeat("-", 32).ToArray())}Cicles test{String.Concat(Enumerable.Repeat("-", 32).ToArray())}");
			Friend friend1 = new Friend("VIctor", null);
			Friend friend2 = new Friend("Dan", friend1);
			friend1.friend = friend2;
			Friend cicleFriend = new Friend("Aurora", friend1);
			sstream = new SerializeStream();
			Serializer.Serialize<Friend>(sstream, cicleFriend);
			Serializer.Deserialize<Friend>(new SerializeStream(sstream.GetBytes())).print();
		}
	}
}
