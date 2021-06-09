using System;
using System.Threading;

namespace client {
	class Program {
		static void Main( string[] args ) {
			log.create( "logs/headless.log" );

			log.info( "Create a new client" );
			var client = new headless.Client();

			client.connect("localhost", 7887);

			client.startReceiving();


			while (true) {
				client.sendExampleObjects();
				Thread.Sleep(5000);
			}
		}
	}
}
