using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;

using WindowsInput;
using WindowsInput.Native;

using UnityEngine;

namespace space_with_friends {

	[KSPAddon( KSPAddon.Startup.Flight, once: true )]
	public class ControlBridge : MonoBehaviour {

		static string[] MONITORED_BINDINGS = {
			"BRAKES",
			"LANDING_GEAR",
			"LAUNCH_STAGES",
			"PAUSE",
			"PRECISION_CTRL",
			"RCS_TOGGLE",
			"SAS_HOLD",
			"SAS_TOGGLE",
			"TIME_WARP_DECREASE",
			"TIME_WARP_INCREASE",
			"TIME_WARP_STOP"
		};

		static Dictionary< string, string > UNITY_TO_VK = new Dictionary< string, string > {
			{ "LEFTSHIFT", "LSHIFT" },
			{ "RIGHTSHIFT", "RSHIFT" }
		};

		static bool initialized = false;

		static GameSettings game_settings;
		static Type game_settings_type;

		static InputSimulator input_sim;

		static ConcurrentDictionary< string, bool > simulating = new ConcurrentDictionary< string, bool >();
		static ConcurrentDictionary< string, bool > old_state = new ConcurrentDictionary< string, bool >();

		private static Queue<Action> run_in_main = new Queue<Action>();

		public void Start() {
			if ( space_with_friends_settings.instance.host == "" ) {
				return;
			}

			if ( initialized ) {
				return;
			}

			utils.Log( "control bridge init" );

			DontDestroyOnLoad( this );

			game_settings = new GameSettings();
			game_settings_type = game_settings.GetType();

			foreach ( string key in MONITORED_BINDINGS ) {
				old_state.TryAdd( key, false );
			}

			input_sim = new InputSimulator();

			Core.client.on_message += on_message;
			initialized = true;
		}

		void Update() {
			if ( !HighLogic.LoadedSceneIsFlight ) {
				return;
			}

			if ( FlightGlobals.ActiveVessel == null ) {
				return;
			}

			foreach ( string key in MONITORED_BINDINGS ) {
				if ( simulating.ContainsKey( key ) ) {
					continue;
				}

				KeyBinding keybinding = (KeyBinding)game_settings_type.GetField( key )?.GetValue( game_settings ) ?? null;
				if ( keybinding == null ) {
					utils.Log( $"WARN: could not get keybinding for { key }" );
					continue;
				}

				bool key_was_down = false;
				old_state.TryGetValue( key, out key_was_down );
				bool key_is_down = keybinding.GetKey( true );
				if ( key_is_down != key_was_down ) {
					Core.client?.send( new msg {
						world_id = space_with_friends.Core.world_id,
						source = space_with_friends.Core.player_id,
						world_time = Planetarium.GetUniversalTime(),
						vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
						type = key_is_down ? "keydown" : "keyup",
						message = key
					} );

					old_state.TryUpdate( key, key_is_down, key_was_down );
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
				return;
			}

			switch( msg.type ) {
				case "keydown":
				case "keyup":
					break;
				default:
					return;
			}

			if ( FlightGlobals.ActiveVessel?.id != msg.vessel_id ) {
				return;
			}

			KeyBinding keybinding = (KeyBinding)game_settings.GetType().GetField( msg.message ).GetValue( game_settings );
			if ( keybinding == null ) {
				utils.Log( $"WARN: could not find keybinding for { msg.message }" );
				return;
			}

			utils.Log( $"vessel: { msg.vessel_id.ToString() } type: { msg.type } keybinding: { msg.message }" );

			string keycode = keybinding.primary.code.ToString().ToUpper();
			if ( UNITY_TO_VK.ContainsKey( keycode ) ) {
				keycode = UNITY_TO_VK[ keycode ];
			}

			bool found_virtual_keycode = false;
			VirtualKeyCode code;
			found_virtual_keycode = Enum.TryParse<VirtualKeyCode>( $"VK_{ keycode }", out code ) || Enum.TryParse<VirtualKeyCode>( keycode, out code );

			if ( !found_virtual_keycode ) {
				utils.Log( $"WARN: could not locate virtual keycode for { keycode }" );
				return;
			}

			switch ( msg.type ) {
				case "keydown":
				{
					run_in_main.Enqueue( () => {
						simulating.TryAdd( msg.message, true );
						utils.Log( $"simulating: keydown / { code.ToString() }" );
						input_sim.Keyboard.KeyDown( code );
					} );
				}
					break;
				case "keyup":
				{
					run_in_main.Enqueue( () => {
						utils.Log( $"simulating: keyup / { code.ToString() }" );
						input_sim.Keyboard.KeyUp( code );
						simulating.TryRemove( msg.message, out _ );
					} );
				}
					break;
			}
		}

		public void OnDestroy() {
			utils.Log( "control bridge stopping" );

			Core.client.on_message -= on_message;
			game_settings = null;
			input_sim = null;
			initialized = false;
		}
	}
}
