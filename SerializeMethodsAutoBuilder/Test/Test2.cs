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
		Friend friend;

		public Friend(string name, Friend friend = null) { 
			this.name = name;
			this.friend = friend;
		}

		public void print() {
			
			Friend currentDuck = this;
			while(currentDuck != null) {
				Console.WriteLine(currentDuck == this ? $"Hello! My name is {currentDuck.name}!" : $"His name is {currentDuck.name}");
				if(currentDuck.friend != null) {
					Console.WriteLine(currentDuck == this ? "I have a friend!" : "He has a friend!");
				} else { 
					Console.WriteLine(currentDuck == this ? "I have no friend :(" : "He has no friend :(");
				}
				currentDuck = currentDuck.friend;
			}

		}

	}

	class Test2 {

		public static void test() { 
			Serializer.useMethodsBuilder(new SMAB());
			Friend friend = new Friend("Tom", new Friend("Bob", new Friend("Robin", new Friend("Marry"))));
			SerializeStream sstream = new SerializeStream();
			Serializer.serialize<Friend>(sstream, friend);
			Serializer.deserialize<Friend>(new SerializeStream(sstream.getBytes())).print();
		}
	}
}
