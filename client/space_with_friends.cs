using System;
using System.Collections.Generic;
using System.IO;

using WindowsInput;
using WindowsInput.Native;

using UnityEngine;

namespace space_with_friends {

	[KSPAddon( KSPAddon.Startup.FlightEditorAndKSC, once: true )]
	public class Core : MonoBehaviour {

		public static space_with_friends.Client client;
		public static string player_id;
		public static Guid world_id;

		private static Queue<Action> run_in_main = new Queue<Action>();

		static GameSettings game_settings;

		static InputSimulator input_sim;

		static int simulating = 0;

		public void Start() {
			DontDestroyOnLoad( this );

			utils.Log( "starting" );

			if ( space_with_friends_settings.instance.host != "" ) {
				if (client != null) {
					utils.Log( "client is not null" );
					return;
				}

				game_settings = new GameSettings();
				input_sim = new InputSimulator();

				world_id = space_with_friends_settings.instance.world_id;

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

				client.send( new msg {
					world_id = world_id,
					source = player_id,
					world_time = Planetarium.GetUniversalTime(),
					type = "login"
				} );

				client.on_message += on_message;
			}
			else {
				utils.Log( "no host name" );
			}
		}

		bool was_pitch_down = false;
		bool was_pitch_up = false;
		bool was_yaw_left = false;
		bool was_yaw_right = false;
		bool was_roll_left = false;
		bool was_roll_right = false;

		void Update() {
			if ( !HighLogic.LoadedSceneIsFlight ) {
				return;
			}

			if ( simulating == 0 ) {
				bool is_pitch_down = GameSettings.PITCH_DOWN.GetKey( true );
				if ( was_pitch_down != is_pitch_down ) {
					client.send( new msg {
							world_id = space_with_friends.Core.world_id,
							source = space_with_friends.Core.player_id,
							world_time = Planetarium.GetUniversalTime(),
							vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
							type = is_pitch_down ? "keydown" : "keyup",
							message = "PITCH_DOWN"
						} );
					was_pitch_down = is_pitch_down;
				}

				bool is_pitch_up = GameSettings.PITCH_UP.GetKey( true );
				if ( was_pitch_up != is_pitch_up ) {
					client.send( new msg {
						world_id = space_with_friends.Core.world_id,
						source = space_with_friends.Core.player_id,
						world_time = Planetarium.GetUniversalTime(),
						vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
						type = is_pitch_up ? "keydown" : "keyup",
						message = "PITCH_UP"
					} );
					was_pitch_up = is_pitch_up;
				}

				bool is_roll_left = GameSettings.ROLL_LEFT.GetKey( true );
				if ( was_roll_left != is_roll_left ) {
					client.send( new msg {
						world_id = space_with_friends.Core.world_id,
						source = space_with_friends.Core.player_id,
						world_time = Planetarium.GetUniversalTime(),
						vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
						type = is_roll_left ? "keydown" : "keyup",
						message = "ROLL_LEFT"
					} );
					was_roll_left = is_roll_left;
				}

				bool is_roll_right = GameSettings.ROLL_RIGHT.GetKey( true );
				if ( was_roll_right != is_roll_right ) {
					client.send( new msg {
						world_id = space_with_friends.Core.world_id,
						source = space_with_friends.Core.player_id,
						world_time = Planetarium.GetUniversalTime(),
						vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
						type = is_roll_right ? "keydown" : "keyup",
						message = "ROLL_RIGHT"
					} );
					was_roll_right = is_roll_right;
				}

				bool is_yaw_left = GameSettings.YAW_LEFT.GetKey( true );
				if ( was_yaw_left != is_yaw_left ) {
					client.send( new msg {
						world_id = space_with_friends.Core.world_id,
						source = space_with_friends.Core.player_id,
						world_time = Planetarium.GetUniversalTime(),
						vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
						type = is_yaw_left ? "keydown" : "keyup",
						message = "YAW_LEFT"
					} );
					was_yaw_left = is_yaw_left;
				}

				bool is_yaw_right = GameSettings.YAW_RIGHT.GetKey( true );
				if ( was_yaw_right != is_yaw_right ) {
					client.send( new msg {
						world_id = space_with_friends.Core.world_id,
						source = space_with_friends.Core.player_id,
						world_time = Planetarium.GetUniversalTime(),
						vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
						type = is_yaw_right ? "keydown" : "keyup",
						message = "YAW_RIGHT"
					} );
					was_yaw_right = is_yaw_right;
				}
			}

			while( run_in_main.Count > 0 )
			{
				Action action = null;
				lock( run_in_main )
				{
					action = run_in_main.Dequeue();
				}
				action?.Invoke();
			}
		}

		public void on_message( space_with_friends.msg msg ) {
			if ( !HighLogic.LoadedSceneIsFlight ) {
				utils.Log( "not flight, skipping message" );
				return;
			}

			if ( FlightGlobals.ActiveVessel?.id != msg.vessel_id ) {
				utils.Log( $"{ ( FlightGlobals.ActiveVessel?.id ?? Guid.Empty ).ToString() } != { msg.vessel_id.ToString() }, skipping" );
				return;
			}

			switch ( msg.type ) {
				case "keydown":
				{
					KeyBinding keybinding = (KeyBinding)game_settings.GetType().GetField( msg.message ).GetValue( game_settings );
					utils.Log( $"keydown / keybinding: { keybinding.ToString() }" );
					if ( keybinding != null ) {
						VirtualKeyCode code;
						utils.Log( $"looking for VK_{ keybinding.primary.code.ToString().ToUpper() }" );
						if ( Enum.TryParse<VirtualKeyCode>( $"VK_{ keybinding.primary.code.ToString().ToUpper() }", out code ) ) {
							run_in_main.Enqueue( () => {
								simulating++;
								utils.Log( $"simulating: keydown / { code.ToString() }" );
								input_sim.Keyboard.KeyDown( code );
							} );
						}
					}
				}
					break;
				case "keyup":
				{
					KeyBinding keybinding = (KeyBinding)game_settings.GetType().GetField( msg.message ).GetValue( game_settings );
					utils.Log( $"keyup / keybinding: { keybinding.ToString() }" );
					if ( keybinding != null ) {
						VirtualKeyCode code;
						utils.Log( $"looking for VK_{ keybinding.primary.code.ToString().ToUpper() }" );
						if ( Enum.TryParse<VirtualKeyCode>( $"VK_{ keybinding.primary.code.ToString().ToUpper() }", out code ) ) {
							run_in_main.Enqueue( () => {
								utils.Log( $"simulating: keyup / { code.ToString() }" );
								input_sim.Keyboard.KeyUp( code );
								simulating--;
							} );
						}
					}
				}
					break;
			}
		}

		public void OnDestroy() {
			utils.Log( "stopping" );

			if (client != null) {
				utils.Log( "disconnecting" );
				client.send( new msg {
					world_id = space_with_friends.Core.world_id,
					source = space_with_friends.Core.player_id,
					world_time = Planetarium.GetUniversalTime(),
					type = "logout"
				} );
				client.disconnect();
				client = null;
				utils.Log( "disconnected" );

				game_settings = null;
				input_sim = null;
			}
		}
	}
}
