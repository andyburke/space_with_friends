﻿using System;

namespace space_with_friends
{
	using System.Collections.Generic;
	using Ceras;
	using Ceras.Helpers;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;
	using System.Threading.Tasks;

	class ServerClient
	{
		readonly TcpClient _tcpClient;
		readonly NetworkStream _netStream;
		readonly CerasSerializer _sendCeras;
		readonly CerasSerializer _receiveCeras;

		string _clientName;

		public ServerClient(TcpClient tcpClient)
		{
			_tcpClient = tcpClient;
			_netStream = tcpClient.GetStream();

			// We want to keep "learned" types
			// That means when the other side sends us a type (using the full name) we never want to transmit that again,
			// the type should (from then on) be known as a some ID.
			var configSend = new SerializerConfig();
			configSend.Advanced.PersistTypeCache = true;
			
			_sendCeras = new CerasSerializer( configSend );

			var configRecv = new SerializerConfig();
			configRecv.Advanced.PersistTypeCache = true;

			_receiveCeras = new CerasSerializer(configRecv);

			StartReceivingMessages();
		}

		void StartReceivingMessages()
		{
			Task.Run(async () =>
			{
				try
				{
					// Keep receiving packets from the client and respond to them
					// Eventually when the client disconnects we'll just get an exception and end the thread...
					while (true)
					{
						var obj = await _receiveCeras.ReadFromStream(_netStream);
						HandleMessage(obj);
					}
				}
				catch (Exception e)
				{
					Log($"Error while handling client '{_tcpClient.Client.RemoteEndPoint}': {e}");
				}
			});
		}

		void HandleMessage(object obj)
		{
			if (obj is msg.login login)
			{
				Log($"login: {login.player_id}");
				_clientName = clientHello.Name;
				Send($"Login ok, your name is now '{_clientName}'");
				return;
			}

			if (obj is msg.Person p)
			{
				Log($"Got a person: {p.Name} ({p.Friends.Count} friends)");
				return;
			}

			if (obj is List<msg.ISpell> spells)
			{
				Log($"We have received {spells.Count} spells:");
				foreach (var spell in spells)
					Log($"  {spell.GetType().Name}: {spell.Cast()}");
				
				return;
			}

			// If we have no clue how to handle something, we
			// just print it out to the console
			Log($"Received a '{obj.GetType().Name}': {obj}");
		}

		void Log(string text) => Console.WriteLine("[Server] " + text);

		void Send(object obj) => _sendCeras.WriteToStream(_netStream, obj);
	}


	static class Server
	{
		public static void Start()
		{
			new Thread(AcceptClients).Start();
		}

		static void AcceptClients()
		{
			var listener = new TcpListener(IPAddress.Any, 43210);
			listener.Start();

			while (true)
			{
				var tcpClient = listener.AcceptTcpClient();

				var serverClientHandler = new ServerClient(tcpClient);
			}
		}


	}
}
