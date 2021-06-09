using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace space_with_friends {

	[KSPAddon( KSPAddon.Startup.FlightEditorAndKSC, once: true )]
	public class Core : MonoBehaviour {

		public static space_with_friends.Client client;
		public static string player_id;

		public void Start() {
			DontDestroyOnLoad( this );

			utils.Log( "starting" );

			if (space_with_friends_settings.instance.host != "") {
				if (client != null) {
					utils.Log( "client is not null" );
					return;
				}

				client = new space_with_friends.Client();

				utils.Log( "connecting" );
				utils.Log( "host: " + space_with_friends_settings.instance.host + " port: " + space_with_friends_settings.instance.port );
				client.connect( space_with_friends_settings.instance.host, space_with_friends_settings.instance.port );

				client.startReceiving();

				// default to the device id
				player_id = SystemInfo.deviceUniqueIdentifier;

				string player_id_file = Path.GetFullPath( Path.Combine( KSPUtil.ApplicationRootPath, "space_with_friends_player_id.txt" ) );
				if (File.Exists( player_id_file )) {
					foreach ( string line in File.ReadLines( player_id_file ) )
					{
						player_id = line.TrimStart().TrimEnd();
						utils.Log( "player_id loaded from file" );
						break;
					}
				}
				utils.Log( "player_id: " + player_id );

				client.broadcast( new msg.login { player_id = player_id } );
			}
			else {
				utils.Log( "no host name" );
			}
		}

		public void OnDestroy() {
			utils.Log( "stopping" );

			if (client != null) {
				utils.Log( "disconnecting" );
				client.broadcast( new msg.logout { player_id = player_id } );
				client.disconnect();
				client = null;
				utils.Log( "disconnected" );
			}
		}
	}
}
